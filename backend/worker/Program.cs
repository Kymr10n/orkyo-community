using Api.Repositories;
using Api.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
            // Single-tenant DB factory: all connection types map to the single community database.
            services.AddSingleton<IDbConnectionFactory>(
                _ => SingleTenantDbConnectionFactory.FromConfiguration(context.Configuration));
            services.AddSingleton<IOrgDbConnectionFactory>(sp =>
                sp.GetRequiredService<IDbConnectionFactory>());
            services.AddSingleton<UserLifecycleService>();
            // Email broadcast for announcements (worker has no tenant context → default branding).
            services.AddSingleton<ITenantSettingsService, WorkerTenantSettingsService>();
            services.AddSingleton<IEmailService, EmailService>();
            services.AddSingleton<IAnnouncementRepository, AnnouncementRepository>();
            services.AddSingleton<IAnnouncementBroadcastService, AnnouncementBroadcastService>();
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
    private readonly IAnnouncementBroadcastService _announcementBroadcast;
    private DateTime _lastDailyCheck = DateTime.MinValue;

    public CommunityWorkerService(
        ILogger<CommunityWorkerService> logger,
        UserLifecycleService userLifecycle,
        IAnnouncementBroadcastService announcementBroadcast)
    {
        _logger = logger;
        _userLifecycle = userLifecycle;
        _announcementBroadcast = announcementBroadcast;
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

                // Announcement email broadcasts are time-sensitive — process every loop.
                await _announcementBroadcast.ProcessPendingBroadcastsAsync(stoppingToken);

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
