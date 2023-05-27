using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pager.Duty;

namespace DryerDuty;

public class DryerMonitor: BackgroundService {

    private readonly ILogger<DryerMonitor>    logger;
    private readonly PagerDutyManager         pagerDutyManager;
    private readonly Configuration            config;
    private readonly IHostApplicationLifetime hostLifetime;

    internal LaundryMachineState? state;
    internal string?              pagerDutyLaundryDoneDedupKey;

    public DryerMonitor(ILogger<DryerMonitor> logger, PagerDutyManager pagerDutyManager, Configuration config, IHostApplicationLifetime hostLifetime) {
        this.logger           = logger;
        this.pagerDutyManager = pagerDutyManager;
        this.config           = config;
        this.hostLifetime     = hostLifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        try {
            while (!stoppingToken.IsCancellationRequested) {
                await executeOnce();
                await Task.Delay(config.pollingIntervalMilliseconds, stoppingToken);
            }
        } catch (TaskCanceledException) {
            //exit normally
        } catch (Exception e) {
            logger.LogError(e, "{message}", e.Message);
            Environment.ExitCode = 1;
            hostLifetime.StopApplication();
        }
    }

    internal async Task executeOnce() {
        int powerMilliwatts = (await outlet.EnergyMeter.GetInstantaneousPowerUsage()).Power;

        LaundryMachineState newState = getNewState(powerMilliwatts, state, config);

        if (state != null && state != newState) {
            await onStateChange(newState);
        }

        state = newState;
        logger.LogDebug("Laundry machine is {state}, using {power:N0} mW", state, powerMilliwatts);
    }

    internal static LaundryMachineState getNewState(int powerMilliwatts, LaundryMachineState? oldState, Configuration config) {
        LaundryMachineState newState;

        return newState;
    }

    internal async Task onStateChange(LaundryMachineState newState) {
        switch (newState) {
            case LaundryMachineState.ACTIVE:
                logger.LogInformation("Started a load of laundry");
                await pagerDutyManager.createChange();
                pagerDutyLaundryDoneDedupKey = null;
                break;

            case LaundryMachineState.COMPLETE:
                logger.LogInformation("Laundry is finished");
                pagerDutyLaundryDoneDedupKey = await pagerDutyManager.createIncident(Severity.Info, "The dryer has finished a load of laundry.", "dryer-00");
                break;

            case LaundryMachineState.IDLE when pagerDutyLaundryDoneDedupKey is not null:
                logger.LogInformation("Laundry is being emptied");
                await pagerDutyManager.resolveIncident(pagerDutyLaundryDoneDedupKey);
                pagerDutyLaundryDoneDedupKey = null;
                break;

            default:
                break;
        }
    }

}