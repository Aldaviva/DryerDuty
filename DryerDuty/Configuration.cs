namespace DryerDuty;

public class Configuration {

    public string pagerDutyIntegrationKey { get; set; } = null!;
    public double motorMinimumActiveAmps { get; set; }
    public double lightMinimumActiveAmps { get; set; }
    public double motorGain { get; set; }
    public double lightGain { get; set; }

}