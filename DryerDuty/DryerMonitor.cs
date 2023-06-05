using System.Device.Spi;
using System.Timers;
using Iot.Device.Adc;
using Pager.Duty;
using Timer = System.Timers.Timer;

namespace DryerDuty;

public class DryerMonitor: IHostedService, IDisposable {

    private const int    SAMPLES_PER_WINDOW      = 120; // 120Hz, Nyquist limit for 60Hz AC sine wave
    private const double SAMPLING_WINDOW_SECONDS = 1.0;
    private const int    VOLTAGE_DIVIDER_OFFSET  = 1024 / 2; // MCP3008 outputs 10-bit unsigned integers, and voltage divider shifts signal up by 512 to preserve negative values
    private const int    POWER_BUTTON_CHANNEL    = 0;
    private const int    DOOR_LIGHT_CHANNEL      = 1;

    private readonly ILogger<DryerMonitor> logger;
    private readonly PagerDutyManager      pagerDutyManager;
    private readonly Configuration         config;
    private readonly SpiDevice             spi = SpiDevice.Create(new SpiConnectionSettings(0, 0) { ClockFrequency = 1_000_000 });
    private readonly Mcp3xxx               adc;
    private readonly Timer                 samplingTimer    = new(TimeSpan.FromSeconds(SAMPLING_WINDOW_SECONDS / SAMPLES_PER_WINDOW)) { AutoReset = true };
    private readonly Timer                 aggregatingTimer = new(TimeSpan.FromSeconds(SAMPLING_WINDOW_SECONDS)) { AutoReset                      = true };

    internal         LaundryMachineState? state;
    internal         string?              pagerDutyLaundryDoneDedupKey;
    private readonly int[][]              samplesByChannel          = new int[2][];
    private readonly int[]                sampleWriteIndexByChannel = { 0, 0 };

    public DryerMonitor(ILogger<DryerMonitor> logger, PagerDutyManager pagerDutyManager, Configuration config) {
        this.logger           = logger;
        this.pagerDutyManager = pagerDutyManager;
        this.config           = config;

        for (int i = 0; i < samplesByChannel.Length; i++) {
            samplesByChannel[i] = new int[SAMPLES_PER_WINDOW];
        }

        adc = new Mcp3008(spi);

        samplingTimer.Elapsed    += onSample;
        aggregatingTimer.Elapsed += aggregateSamplesInWindow;
    }

    private int activeAdcChannel => state == LaundryMachineState.COMPLETE ? DOOR_LIGHT_CHANNEL : POWER_BUTTON_CHANNEL;

    public Task StartAsync(CancellationToken cancellationToken) {
        samplingTimer.Start();
        aggregatingTimer.Start();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) {
        samplingTimer.Stop();
        aggregatingTimer.Stop();
        return Task.CompletedTask;
    }

    private void onSample(object? sender, ElapsedEventArgs e) {
        int channel          = activeAdcChannel;
        int sampleWriteIndex = sampleWriteIndexByChannel[channel];

        samplesByChannel[channel][sampleWriteIndex] = adc.Read(channel); // sample is in the range [0, 1024)

        sampleWriteIndexByChannel[channel] = (sampleWriteIndex + 1) % SAMPLES_PER_WINDOW;
    }

    private void aggregateSamplesInWindow(object? sender, ElapsedEventArgs e) {
        double rmsVolts = samplesByChannel[activeAdcChannel].Aggregate(0.0, sumSquares, rootOfMean);

        logger.LogTrace("ch{adcChannel}: {volts:N6}", activeAdcChannel, rmsVolts);

        LaundryMachineState newState = state switch {
            LaundryMachineState.IDLE or null when rmsVolts >= config.powerButtonMinimumActiveVolts => LaundryMachineState.ACTIVE,
            LaundryMachineState.ACTIVE when rmsVolts < config.powerButtonMinimumActiveVolts        => LaundryMachineState.COMPLETE,
            LaundryMachineState.COMPLETE when rmsVolts >= config.doorLightMinimumIlluminatedVolts  => LaundryMachineState.IDLE,
            _                                                                                      => state ?? LaundryMachineState.IDLE
        };

        logger.LogDebug("Dryer is {state}, using {power:N0} VAC RMS on ADC channel {adcChannel}", state, rmsVolts, activeAdcChannel);

        if (state != null && state != newState) {
#pragma warning disable CS4014 // don't need to wait for a PagerDuty API response before storing the new state and polling again
            onStateChange(newState);
#pragma warning restore CS4014
        }

        state = newState;
    }

    private static double sumSquares(double sum, int sample) {
        double signedSampleVoltage = (sample - VOLTAGE_DIVIDER_OFFSET) / (double) VOLTAGE_DIVIDER_OFFSET; // signedSampleVoltage is in the range [-1.0, 1.0) volts
        return sum + signedSampleVoltage * signedSampleVoltage;
    }

    private static double rootOfMean(double sumSquared) => Math.Sqrt(sumSquared / SAMPLES_PER_WINDOW);

    private async Task onStateChange(LaundryMachineState newState) {
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

    public void Dispose() {
        spi.Dispose();
        adc.Dispose();
        samplingTimer.Dispose();
        aggregatingTimer.Dispose();
    }

}