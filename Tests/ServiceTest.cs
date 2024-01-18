using DryerDuty;
using Iot.Device.Adc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace Tests;

public sealed class ServiceTest: IDisposable {

    private readonly IServiceHostInterceptor hostInterceptor = new ServiceHostInterceptor();

    private readonly Mcp3xxx adc = A.Fake<Mcp3xxx>();

    public ServiceTest() {
        A.CallTo(() => adc.Read(A<int>._)).Returns(512);
    }

    [Fact]
    public async Task start() {
        hostInterceptor.hostBuilding += (_, builder) => {
            builder.ConfigureServices(services => {
                services.RemoveAll<IHostedService>();
                services.AddHostedService(s => new DryerMonitor(s.GetRequiredService<ILogger<DryerMonitor>>(), s.GetRequiredService<PagerDutyManager>(), s.GetRequiredService<Configuration>(), adc));

                services.RemoveAll<Configuration>();
                services.AddSingleton(new Configuration { pagerDutyIntegrationKey = "12345" });
            });
        };

        Task mainTask = runMainMethod();
        hostInterceptor.host?.StopAsync();
        await mainTask;
    }

    [Fact]
    public async Task crashOnMissingPagerDutyIntegrationKey() {
        hostInterceptor.hostBuilding += (_, builder) => {
            builder.ConfigureServices(services => {
                services.RemoveAll<IHostedService>();
                services.AddHostedService(s => new DryerMonitor(s.GetRequiredService<ILogger<DryerMonitor>>(), s.GetRequiredService<PagerDutyManager>(), s.GetRequiredService<Configuration>(), adc));
            });
        };

        Func<Task> thrower = runMainMethod;
        await thrower.Should().ThrowWithinAsync<ArgumentException>(TimeSpan.FromSeconds(15));
    }

    private static async Task runMainMethod() =>
        await (Task) typeof(Program).GetMethod("<Main>$", BindingFlags.NonPublic | BindingFlags.Static, new[] { typeof(string[]) })!.Invoke(null, new object[] { Array.Empty<string>() })!;

    public void Dispose() {
        hostInterceptor.Dispose();
    }

}