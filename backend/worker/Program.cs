using Api.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orkyo.Community.Services;
using Orkyo.Shared.Keycloak;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Starting Orkyo Community Worker");

    using var host = Host.CreateDefaultBuilder(args)
        .UseSerilog()
        .ConfigureServices((context, services) =>
        {
            services.AddHttpClient();
            services.AddSingleton(KeycloakOptions.FromConfiguration(context.Configuration));
            // Community DB factory: CreateControlPlaneConnection() → single community database
            services.AddSingleton<IDbConnectionFactory, CommunityDbConnectionFactory>();
            services.AddSingleton<IOrgDbConnectionFactory>(sp =>
                sp.GetRequiredService<IDbConnectionFactory>());
            services.AddSingleton<UserLifecycleService>();
            services.AddHostedService<CommunityWorkerService>();
        })
        .Build();

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Worker terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

internal sealed class CommunityWorkerService : BackgroundService
{
    private readonly ILogger<CommunityWorkerService> _logger;
    private readonly UserLifecycleService _userLifecycle;
    private DateTime _lastDailyCheck = DateTime.MinValue;

    public CommunityWorkerService(ILogger<CommunityWorkerService> logger, UserLifecycleService userLifecycle)
    {
        _logger = logger;
        _userLifecycle = userLifecycle;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Community worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;

                // Run GDPR user lifecycle once per day
                if (now - _lastDailyCheck >= TimeSpan.FromHours(24))
                {
                    _logger.LogInformation("Running user lifecycle check");
                    await _userLifecycle.ProcessAsync(stoppingToken);
                    _lastDailyCheck = now;
                }

                await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in worker");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        _logger.LogInformation("Community worker stopped");
    }
}
