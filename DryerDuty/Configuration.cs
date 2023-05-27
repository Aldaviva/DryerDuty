namespace DryerDuty;

public class Configuration {

    public int pollingIntervalMilliseconds { get; set; } = 15_000;
    public string pagerDutyIntegrationKey { get; set; } = null!;

}