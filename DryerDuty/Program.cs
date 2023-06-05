using DryerDuty;
using Pager.Duty;

using IHost host = Host.CreateDefaultBuilder(args)
    .UseSystemd()
    .ConfigureServices((context, services) => {
        services.AddHostedService<DryerMonitor>();
        services.AddSingleton<PagerDutyManager, PagerDutyManagerImpl>();
        services.AddSingleton(_ => context.Configuration.Get<Configuration>()!);
        services.AddSingleton<IPagerDuty>(s => new PagerDuty(s.GetRequiredService<Configuration>().pagerDutyIntegrationKey));
    })
    .Build();

await host.RunAsync();