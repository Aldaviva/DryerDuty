using DryerDuty;
using Pager.Duty;
using RuntimeUpgrade.Notifier;
using RuntimeUpgrade.Notifier.Data;

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

using RuntimeUpgradeNotifier upgradeNotifier = new() {
    LoggerFactory   = host.Services.GetRequiredService<ILoggerFactory>(),
    RestartStrategy = RestartStrategy.AutoRestartService,
    ExitStrategy    = new HostedLifetimeExit(host)
};

await host.RunAsync();