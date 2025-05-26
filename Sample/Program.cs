using System.Device.Spi;
using Iot.Device.Adc;

Console.WriteLine("Starting...");

/*
 * Connect MCP3008 CS/SHDN pin to RPi CE0/GPIO8/Physical pin 24
 * https://github.com/dotnet/iot/blob/main/src/devices/Mcp3xxx/README.md
 */

using SpiDevice spi = SpiDevice.Create(new SpiConnectionSettings(0, 0) { ClockFrequency = 1_000_000 });
using Mcp3008   adc = new(spi);

const int    SAMPLES_PER_WINDOW      = 120;
const double SAMPLING_WINDOW_SECONDS = 1.0;
const int    MAX_ADC_READING         = (2 << 9) - 1;
// const int    VOLTAGE_DIVIDER_OFFSET               = 1024 / 2;
const double REFERENCE_VOLTS                      = 3.3;
const double MAX_CURRENT_TRANSFORMER_OUTPUT_VOLTS = 1;

int[] maxCurrentTransformerInputAmps = { 60, 5 };

int[][] samplesByChannel      = new int[2][];
int[]   sampleWriteIndex      = new int[2];
int[,]  rangesByChannel       = new int[2, 2];
long[]  sumsByChannel         = new long[2];
int[]   sampleCountsByChannel = new int[2];

for (int i = 0; i < samplesByChannel.Length; i++) {
    samplesByChannel[i] = new int[SAMPLES_PER_WINDOW];
}

await using Timer samplingTimer = new(_ => {
    for (int channel = 0; channel < samplesByChannel.Length; channel++) {
        int sample = adc.Read(channel);
        if (sampleCountsByChannel[channel] == 0) {
            rangesByChannel[channel, 0] = sample;
            rangesByChannel[channel, 1] = sample;
        } else {
            rangesByChannel[channel, 0] = Math.Min(rangesByChannel[channel, 0], sample);
            rangesByChannel[channel, 1] = Math.Max(rangesByChannel[channel, 1], sample);
        }

        sumsByChannel[channel] += sample;
        sampleCountsByChannel[channel]++;

        samplesByChannel[channel][sampleWriteIndex[channel]] = sample;
        sampleWriteIndex[channel]                            = (sampleWriteIndex[channel] + 1) % SAMPLES_PER_WINDOW;
    }
}, null, TimeSpan.Zero, TimeSpan.FromSeconds(SAMPLING_WINDOW_SECONDS / SAMPLES_PER_WINDOW));

await using Timer outputTimer = new(_ => {
    for (int channel = 0; channel < samplesByChannel.Length; channel++) {
        // double rmsVolts = samplesByChannel[channel].Aggregate(0, sumSquares, rootOfMean);
        int maxCurrentTransformerInputAmpsForChannel = maxCurrentTransformerInputAmps[channel];
        double acAmps = samplesByChannel[channel].Aggregate(0.0,
            // (old, latest) => old + Math.Pow((latest / 1024.0 * REFERENCE_VOLTS - REFERENCE_VOLTS / 2) / MAX_CURRENT_TRANSFORMER_OUTPUT_VOLTS * maxCurrentTransformerInputAmpsForChannel, 2),
            (sum, sample) => sum +
                Math.Pow(
                    (((sample - MAX_ADC_READING / 2.0) / (REFERENCE_VOLTS / 2) + MAX_ADC_READING / 2.0) / MAX_ADC_READING * REFERENCE_VOLTS - REFERENCE_VOLTS / 2) /
                    MAX_CURRENT_TRANSFORMER_OUTPUT_VOLTS * maxCurrentTransformerInputAmpsForChannel, 2),
            sumOfSquares => Math.Sqrt(sumOfSquares / SAMPLES_PER_WINDOW));

        if (channel != 0) {
            Console.Write("  |  ");
        }

        Console.Write(
            $"ch{channel}: min = {rangesByChannel[channel, 0],3:N0}, mean = {sumsByChannel[channel] / sampleCountsByChannel[channel],3:N0}, max = {rangesByChannel[channel, 1],3:N0}, range = {rangesByChannel[channel, 1] - rangesByChannel[channel, 0],3:N0}, latest = {samplesByChannel[channel][sampleWriteIndex[channel]],3:N0}, ac amps = {acAmps,7:N3}");
        // Console.Write($"ch{channel}: {rmsVolts,7:N3} VAC");
    }

    Console.WriteLine();
}, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

ManualResetEventSlim exit = new();
Console.CancelKeyPress += (_, eventArgs) => {
    eventArgs.Cancel = true;
    exit.Set();
};
exit.Wait();

// static double sumSquares(double sum, double sample) => sum + sample * sample;

// static int sumSquares(int    sum, int sample) => sum + (int) Math.Pow(sample, 2);
// static double rootOfMean(int sumSquared) => Math.Sqrt(sumSquared / (double) SAMPLES_PER_WINDOW);