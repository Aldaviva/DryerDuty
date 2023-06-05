namespace DryerDuty;

public class Configuration {

    public string pagerDutyIntegrationKey { get; set; } = null!;
    public double powerButtonMinimumActiveVolts { get; set; }
    public double doorLightMinimumIlluminatedVolts { get; set; }

}