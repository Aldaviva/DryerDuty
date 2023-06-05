using System.Device.Spi;
using Iot.Device.Adc;

/*
 * Connect MCP3008 CS/SHDN pin to RPi CE0/GPIO8/Physical pin 24
 * https://github.com/dotnet/iot/blob/main/src/devices/Mcp3xxx/README.md
 */

using SpiDevice spi = SpiDevice.Create(new SpiConnectionSettings(0, 0) { ClockFrequency = 1_000_000 });
using Mcp3008   adc = new(spi);

const int    SAMPLES_PER_WINDOW      = 120;
const double SAMPLING_WINDOW_SECONDS = 1.0;
const int    VOLTAGE_DIVIDER_OFFSET  = 1024 / 2;
const int    ADC_CHANNEL             = 1;

int[] samples          = new int[SAMPLES_PER_WINDOW];
int   sampleWriteIndex = 0;

using Timer samplingTimer = new(_ => {
    samples[sampleWriteIndex] = adc.Read(ADC_CHANNEL);
    sampleWriteIndex          = (sampleWriteIndex + 1) % SAMPLES_PER_WINDOW;
}, null, TimeSpan.Zero, TimeSpan.FromSeconds(SAMPLING_WINDOW_SECONDS / SAMPLES_PER_WINDOW));

int[] samplesCopy = new int[SAMPLES_PER_WINDOW];
using Timer outputTimer = new(_ => {
    Array.Copy(samples, samplesCopy, SAMPLES_PER_WINDOW);
    double rmsVolts = samplesCopy.Aggregate(0, sumSquares, rootOfMean);
    Console.WriteLine($"ch1: {rmsVolts,7:N3} VAC");
}, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

CancellationTokenSource cts = new();
Console.CancelKeyPress += (_, eventArgs) => {
    eventArgs.Cancel = true;
    cts.Cancel();
};
cts.Token.WaitHandle.WaitOne();

static int sumSquares(int    sum, int sample) => sum + (int) Math.Pow((sample - VOLTAGE_DIVIDER_OFFSET) / (double) VOLTAGE_DIVIDER_OFFSET, 2);
static double rootOfMean(int sumSquared) => Math.Sqrt(sumSquared / (double) SAMPLES_PER_WINDOW);