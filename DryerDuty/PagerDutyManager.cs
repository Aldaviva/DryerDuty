using Pager.Duty;
using Pager.Duty.Exceptions;
using Pager.Duty.Requests;
using Pager.Duty.Responses;
using ThrottleDebounce;

namespace DryerDuty;

public interface PagerDutyManager {

    Task<string?> createIncident(Severity severity, string summary, string component);

    Task resolveIncident(string dedupKey);

    Task createChange();

}

public class PagerDutyManagerImpl(IPagerDuty pagerDuty, ILogger<PagerDutyManagerImpl> logger): PagerDutyManager {

    private const int MAX_ATTEMPTS = 18; // https://dotnetfiddle.net/H7VD8k

    private static readonly Func<int, TimeSpan>    DELAY            = Retrier.Delays.Exponential(TimeSpan.FromSeconds(0.25));
    private static readonly Func<Exception, bool>? IS_RETRY_ALLOWED = e => e is PagerDutyException { RetryAllowedAfterDelay: true };

    public async Task createChange() {
        try {
            await attempt(async () => await pagerDuty.Send(new Change("The dryer is starting a load of laundry.")));
        } catch (PagerDutyException e) {
            logger.LogError(e, "Failed to create Change event in PagerDuty");
        }
    }

    public async Task<string?> createIncident(Severity severity, string summary, string component) {
        try {
            AlertResponse alertResponse = await attempt(async () => await pagerDuty.Send(new TriggerAlert(severity, summary) {
                Class     = "laundry",
                Component = component,
                Group     = "garage-00"
            }));

            return alertResponse.DedupKey;
        } catch (PagerDutyException e) {
            logger.LogError(e, "Failed to trigger Alert in PagerDuty");
            return null;
        }
    }

    public async Task resolveIncident(string dedupKey) {
        try {
            await attempt(async () => await pagerDuty.Send(new ResolveAlert(dedupKey)));
        } catch (PagerDutyException e) {
            logger.LogError(e, "Failed to resolve Alert {dedupKey} in PagerDuty", dedupKey);
        }
    }

    private async Task<T> attempt<T>(Func<Task<T>> operation) {
        return await Retrier.Attempt(async _ => await operation(), MAX_ATTEMPTS, DELAY, IS_RETRY_ALLOWED, beforeRetry);
    }

    private Task beforeRetry(int retryCount, Exception e) {
        logger.LogWarning(e, "Retrying failed PagerDuty request (#{retry:N0}/{max:N0})", retryCount, MAX_ATTEMPTS);
        return Task.CompletedTask;
    }

}