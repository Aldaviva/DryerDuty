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

public class PagerDutyManagerImpl: PagerDutyManager {

    private readonly IPagerDuty                    pagerDuty;
    private readonly ILogger<PagerDutyManagerImpl> logger;
    private readonly Retrier.Options               retryOptions;

    public PagerDutyManagerImpl(IPagerDuty pagerDuty, ILogger<PagerDutyManagerImpl> logger) {
        this.pagerDuty = pagerDuty;
        this.logger    = logger;

        retryOptions = new Retrier.Options {
            MaxAttempts    = 18, // https://dotnetfiddle.net/H7VD8k
            Delay          = Retrier.Delays.Exponential(TimeSpan.FromSeconds(0.25)),
            IsRetryAllowed = e => e is PagerDutyException { RetryAllowedAfterDelay: true },
            BeforeRetry    = (retryCount, e) => logger.LogWarning(e, "Retrying failed PagerDuty request (#{retry:N0}/{max:N0})", retryCount, 18)
        };
    }

    public async Task createChange() {
        try {
            await Retrier.Attempt(async _ => await pagerDuty.Send(new Change("The dryer is starting a load of laundry.")), retryOptions);
        } catch (PagerDutyException e) {
            logger.LogError(e, "Failed to create Change event in PagerDuty");
        }
    }

    public async Task<string?> createIncident(Severity severity, string summary, string component) {
        try {
            AlertResponse alertResponse = await Retrier.Attempt(async _ => await pagerDuty.Send(new TriggerAlert(severity, summary) {
                Class     = "laundry",
                Component = component,
                Group     = "garage-00"
            }), retryOptions);

            return alertResponse.DedupKey;
        } catch (PagerDutyException e) {
            logger.LogError(e, "Failed to trigger Alert in PagerDuty");
            return null;
        }
    }

    public async Task resolveIncident(string dedupKey) {
        try {
            await Retrier.Attempt(async _ => await pagerDuty.Send(new ResolveAlert(dedupKey)), retryOptions);
        } catch (PagerDutyException e) {
            logger.LogError(e, "Failed to resolve Alert {dedupKey} in PagerDuty", dedupKey);
        }
    }

}