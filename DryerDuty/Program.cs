using DryerDuty;
using Pager.Duty;

using IHost host = Host.CreateDefaultBuilder(args)
    .UseSystemd()
    .ConfigureServices((context, services) => {
        services.AddHostedService<DryerMonitor>();
        services.AddSingleton<PagerDutyManager, PagerDutyManagerImpl>();
        services.AddSingleton(_ => context.Configuration.Get<Configuration>()!);
        services.AddSingleton<IPagerDuty>(s => {
            string? integrationKey = s.GetRequiredService<Configuration>().pagerDutyIntegrationKey;
            if (integrationKey == null || integrationKey.Contains("Add a PagerDuty Events API v2 Integration to your Service and paste its Integration Key here")) {
                s.GetRequiredService<ILogger<Program>>().LogError("During DryerDuty startup, pagerDutyIntegrationKey was found to be missing or misconfigured in appsettings.json. " +
                    "Program is exiting. See https://github.com/Aldaviva/DryerDuty#configuration for instructions.");
                throw new ArgumentException("Missing pagerDutyIntegrationKey in appsettings.json");
            }

            return new PagerDuty(integrationKey);
        });
    })
    .Build();

/*var logger = host.Services.GetRequiredService<ILogger<Program>>();
RuntimeUpgradeNotifier.RestartBehavior = RestartBehavior.AutoRestartSystemdService;
RuntimeUpgradeNotifier.RuntimeUpgraded += (_, _) => {
    logger.LogWarning(".NET Runtime {oldVersion} has been upgraded to a new version. Restarting this service to use the new runtime and avoid future crashes.", Environment.Version.ToString(3));
    return ValueTask.CompletedTask;
};*/

await host.RunAsync();