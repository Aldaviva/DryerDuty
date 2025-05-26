namespace DryerDuty;

public class Configuration {

    public string? pagerDutyIntegrationKey { get; set; }
    public double motorMinimumActiveAmps { get; set; }
    public double lightMinimumActiveAmps { get; set; }
    public double motorGain { get; set; } = 1.0;
    public double lightGain { get; set; } = 1.0;

}