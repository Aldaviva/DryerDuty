using System.Reflection;
using DryerDuty;
using Iot.Device.Adc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Tests;

public class ServiceTest: IDisposable {

    private readonly IServiceHostInterceptor hostInterceptor = new ServiceHostInterceptor();

    [Fact]
    public async Task start() {
        Mcp3xxx adc = A.Fake<Mcp3xxx>();
        A.CallTo(() => adc.Read(A<int>._)).Returns(512);

        hostInterceptor.hostBuilding += (_, builder) => {
            builder.ConfigureServices(services => {
                services.RemoveAll<IHostedService>();
                services.AddHostedService(s => new DryerMonitor(s.GetRequiredService<ILogger<DryerMonitor>>(), s.GetRequiredService<PagerDutyManager>(), s.GetRequiredService<Configuration>(), adc));
            });
        };

        MethodInfo mainMethod = typeof(Program).GetMethod("<Main>$", BindingFlags.NonPublic | BindingFlags.Static, new[] { typeof(string[]) })!;

        Task mainTask = (Task) mainMethod.Invoke(null, new object[] { Array.Empty<string>() })!;

        hostInterceptor.host?.StopAsync();

        await mainTask;
    }

    public void Dispose() {
        hostInterceptor.Dispose();
    }

}