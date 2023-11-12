using DryerDuty;
using Iot.Device.Adc;
using Microsoft.Extensions.Logging.Abstractions;
using Pager.Duty.Requests;

namespace Tests;

public class DryerMonitorTest: IDisposable {

    private readonly Configuration    config           = new() { motorMinimumActiveAmps = 2, lightMinimumActiveAmps = 0.04, motorGain = 1.0, lightGain = 1.0 };
    private readonly PagerDutyManager pagerDutyManager = A.Fake<PagerDutyManager>();
    private readonly Mcp3xxx          adc              = A.Fake<Mcp3xxx>();
    private readonly DryerMonitor     dryerMonitor;

    public DryerMonitorTest() {
        dryerMonitor = new DryerMonitor(new NullLogger<DryerMonitor>(), pagerDutyManager, config, adc);
    }

    [Fact]
    public async Task motorStarts() {
        dryerMonitor.state = LaundryMachineState.IDLE;

        A.CallTo(() => adc.Read(0)).Returns(529);
        A.CallTo(() => adc.Read(1)).Returns(512);

        fillSampleBuffer();

        await dryerMonitor.aggregateSamplesInWindow();

        dryerMonitor.state.Should().Be(LaundryMachineState.ACTIVE);
        A.CallTo(() => pagerDutyManager.createChange()).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task motorStops() {
        dryerMonitor.state = LaundryMachineState.ACTIVE;

        A.CallTo(() => adc.Read(0)).Returns(512);
        A.CallTo(() => adc.Read(1)).Returns(512);

        fillSampleBuffer();

        await dryerMonitor.aggregateSamplesInWindow();

        dryerMonitor.state.Should().Be(LaundryMachineState.COMPLETE);
        A.CallTo(() => pagerDutyManager.createIncident(A<Severity>._, A<string>._, A<string>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task doorOpened() {
        dryerMonitor.state                        = LaundryMachineState.COMPLETE;
        dryerMonitor.pagerDutyLaundryDoneDedupKey = "abc";

        A.CallTo(() => adc.Read(0)).Returns(512);
        A.CallTo(() => adc.Read(1)).Returns(519);

        fillSampleBuffer();

        await dryerMonitor.aggregateSamplesInWindow();

        dryerMonitor.state.Should().Be(LaundryMachineState.IDLE);
        A.CallTo(() => pagerDutyManager.resolveIncident("abc")).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task unknownInitialState() {
        A.CallTo(() => adc.Read(0)).Returns(512);
        A.CallTo(() => adc.Read(1)).Returns(512);

        fillSampleBuffer();

        await dryerMonitor.aggregateSamplesInWindow();

        dryerMonitor.state.Should().Be(LaundryMachineState.IDLE);
    }

    private void fillSampleBuffer() {
        for (int i = 0; i < DryerMonitor.SAMPLES_PER_WINDOW; i++) {
            dryerMonitor.onSample();
        }
    }

    [Fact]
    public async Task startAndStopService() {
        dryerMonitor.state = LaundryMachineState.IDLE;
        A.CallTo(() => adc.Read(0)).Returns(529);
        A.CallTo(() => adc.Read(1)).Returns(512);

        ManualResetEventSlim pagerDutyChangeCreated = new();
        A.CallTo(() => pagerDutyManager.createChange()).Invokes(() => pagerDutyChangeCreated.Set());

        await dryerMonitor.StartAsync(CancellationToken.None);

        pagerDutyChangeCreated.Wait(TimeSpan.FromSeconds(10));
        dryerMonitor.state.Should().Be(LaundryMachineState.ACTIVE);
        A.CallTo(() => pagerDutyManager.createChange()).MustHaveHappenedOnceExactly();

        await dryerMonitor.StopAsync(CancellationToken.None);

        A.CallTo(() => adc.Read(0)).Returns(512);

        await Task.Delay(3000);

        dryerMonitor.state.Should().NotBe(LaundryMachineState.COMPLETE);
        A.CallTo(() => pagerDutyManager.createIncident(A<Severity>._, A<string>._, A<string>._)).MustNotHaveHappened();
    }

    [Fact]
    public void publicConstructor() {
        Action thrower = () => new DryerMonitor(NullLogger<DryerMonitor>.Instance, pagerDutyManager, config);
        if (Environment.OSVersion.Platform == PlatformID.Win32NT) {
            thrower.Should().Throw<PlatformNotSupportedException>();
        } else {
            thrower.Should().NotThrow<PlatformNotSupportedException>();
        }
    }

    public void Dispose() {
        dryerMonitor.Dispose();
    }

}