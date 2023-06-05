﻿using System.Device.Spi;
using System.Timers;
using Iot.Device.Adc;
using Pager.Duty;
using Timer = System.Timers.Timer;

namespace DryerDuty;

public class DryerMonitor: IHostedService, IDisposable {

    private const int    SAMPLES_PER_WINDOW                   = 120; // 120Hz, Nyquist limit for 60Hz AC sine wave
    private const int    MOTOR_CHANNEL                        = 0;
    private const int    LIGHT_CHANNEL                        = 1;
    private const int    MAX_ADC_READING                      = (2 << 9) - 1;
    private const double REFERENCE_VOLTS                      = 3.3;
    private const double MAX_CURRENT_TRANSFORMER_OUTPUT_VOLTS = 1;

    private readonly ILogger<DryerMonitor> logger;
    private readonly PagerDutyManager      pagerDutyManager;
    private readonly Configuration         config;
    private readonly SpiDevice             spi = SpiDevice.Create(new SpiConnectionSettings(0, 0) { ClockFrequency = 1_000_000 });
    private readonly Mcp3xxx               adc;
    private readonly TimeSpan              samplingWindow = TimeSpan.FromSeconds(1);
    private readonly Timer                 samplingTimer;
    private readonly Timer                 aggregatingTimer;
    private readonly int[][]               samplesByChannel          = new int[2][];
    private readonly int[]                 maxCurrentTransformerAmps = { 60, 5 };

    private LaundryMachineState? state;
    private string?              pagerDutyLaundryDoneDedupKey;
    private int                  sampleWriteIndex;

    public DryerMonitor(ILogger<DryerMonitor> logger, PagerDutyManager pagerDutyManager, Configuration config) {
        this.logger           = logger;
        this.pagerDutyManager = pagerDutyManager;
        this.config           = config;

        for (int i = 0; i < samplesByChannel.Length; i++) {
            samplesByChannel[i] = new int[SAMPLES_PER_WINDOW];
        }

        adc              = new Mcp3008(spi);
        aggregatingTimer = new Timer(samplingWindow) { AutoReset                            = true };
        samplingTimer    = new Timer(samplingWindow.Divide(SAMPLES_PER_WINDOW)) { AutoReset = true };

        samplingTimer.Elapsed    += onSample;
        aggregatingTimer.Elapsed += aggregateSamplesInWindow;
    }

    public async Task StartAsync(CancellationToken cancellationToken) {
        samplingTimer.Start();
        await Task.Delay(samplingWindow, cancellationToken);
        aggregatingTimer.Start();
        logger.LogInformation("Timers started");
    }

    public Task StopAsync(CancellationToken cancellationToken) {
        samplingTimer.Stop();
        aggregatingTimer.Stop();
        return Task.CompletedTask;
    }

    private void onSample(object? sender, ElapsedEventArgs e) {
        for (int channel = 0; channel < samplesByChannel.Length; channel++) {
            samplesByChannel[channel][sampleWriteIndex] = adc.Read(channel); // sample is in the range [0, 1024)
        }

        sampleWriteIndex = (sampleWriteIndex + 1) % SAMPLES_PER_WINDOW;
    }

    private double getRmsAmps(int channel) =>
        samplesByChannel[channel].Aggregate(0.0,
            (sum, sample) => sum + Math.Pow(
                (((sample - MAX_ADC_READING / 2.0) / (REFERENCE_VOLTS / 2) + MAX_ADC_READING / 2.0) / MAX_ADC_READING * REFERENCE_VOLTS - REFERENCE_VOLTS / 2) /
                MAX_CURRENT_TRANSFORMER_OUTPUT_VOLTS * maxCurrentTransformerAmps[channel], 2),
            sumOfSquares => Math.Sqrt(sumOfSquares / SAMPLES_PER_WINDOW));

    private void aggregateSamplesInWindow(object? sender, ElapsedEventArgs e) {
        double motorAmps = getRmsAmps(MOTOR_CHANNEL) * config.motorGain;
        double lightAmps = getRmsAmps(LIGHT_CHANNEL) * config.lightGain;

        LaundryMachineState newState = state switch {
            LaundryMachineState.IDLE or null when motorAmps >= config.motorMinimumActiveAmps => LaundryMachineState.ACTIVE,
            LaundryMachineState.ACTIVE when motorAmps < config.motorMinimumActiveAmps        => LaundryMachineState.COMPLETE,
            LaundryMachineState.COMPLETE when lightAmps >= config.lightMinimumActiveAmps     => LaundryMachineState.IDLE,
            _                                                                                => state ?? LaundryMachineState.IDLE
        };

        logger.LogTrace("Dryer is {state}, using {motorAmps:N3} amps for the motor and {lightAmps:N3} amps for the light", newState, motorAmps, lightAmps);

        if (state != null && state != newState) {
#pragma warning disable CS4014 // don't need to wait for a PagerDuty API response before storing the new state and polling again
            onStateChange(newState);
#pragma warning restore CS4014
        }

        state = newState;
    }

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
        aggregatingTimer.Dispose();
        samplingTimer.Dispose();
        adc.Dispose();
        spi.Dispose();
        GC.SuppressFinalize(this);
    }

}