using Api.Configuration;
using Api.Integrations.Keycloak;
using Api.Repositories;
using Api.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orkyo.Shared;
using Orkyo.Shared.Keycloak;
using Serilog;

// ── Worker logging contract (kept in lockstep with the other edition's worker) ──
// Shared invariants: Information default with Microsoft→Warning; a fatal crash
// does Log.Fatal + Environment.ExitCode = 1, and finally CloseAndFlush. The SINKS
// deliberately differ per edition (saas: console + rolling file + enrichers for
// prod ops; community: console only — the self-host container captures stdout).
// Do not extract a shared helper: the common config is ~2 lines and foundation
// core would gain a Serilog dependency for it (optimization plan W4.8).
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Starting Orkyo Community Worker");

    // Fail fast if the runtime image lacks tzdata: auto-scheduling resolves each site's
    // IANA time zone, and a missing database must kill the container at boot rather than
    // fail per tenant in the middle of a run. Same probe the API runs via
    // ConfigurationValidator, which the workers have no --validate path for.
    if (ConfigurationValidator.TimeZoneDataError() is { } timeZoneError)
        throw new InvalidOperationException(timeZoneError);

    using var host = Host.CreateDefaultBuilder(args)
        .UseSerilog()
        .ConfigureServices((context, services) =>
        {
            // Edition-specific: single-tenant DB factory (all connection types map to the one community DB).
            services.AddSingleton<IDbConnectionFactory>(
                _ => SingleTenantDbConnectionFactory.FromConfiguration(context.Configuration));
            // Shared worker graph (HTTP client, Keycloak, email, announcements, user lifecycle).
            services.AddFoundationWorkerServices(context.Configuration);
            // Community-only hosted service.
            services.AddHostedService<CommunityWorkerService>();
        })
        .Build();

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Worker terminated unexpectedly");
    // Signal failure so container restart policies see the crash (was exiting 0).
    Environment.ExitCode = 1;
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
    private readonly IWorkerJobCoordinator _jobs;

    public CommunityWorkerService(
        ILogger<CommunityWorkerService> logger,
        UserLifecycleService userLifecycle,
        IAnnouncementBroadcastService announcementBroadcast,
        IWorkerJobCoordinator jobs)
    {
        _logger = logger;
        _userLifecycle = userLifecycle;
        _announcementBroadcast = announcementBroadcast;
        _jobs = jobs;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Community worker started");

        // Cadence state lives in the worker_job_runs journal (via IWorkerJobCoordinator):
        // a restart resumes the schedule instead of immediately re-running the daily GDPR
        // pass, and a second worker instance skips instead of double-running.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Run GDPR user lifecycle once per day
                await _jobs.RunIfDueAsync(
                    WorkerJobNames.UserLifecycle,
                    WorkerSchedulePolicy.ShouldRunUserLifecycle,
                    async ct =>
                    {
                        _logger.LogInformation("Running user lifecycle check");
                        await _userLifecycle.ProcessAsync(ct);
                    },
                    stoppingToken);

                // Announcement email broadcasts are time-sensitive — attempt every loop,
                // but single-flight across instances so replicas cannot double-send.
                await _jobs.RunIfDueAsync(
                    WorkerJobNames.AnnouncementBroadcast,
                    (_, _) => true,
                    ct => _announcementBroadcast.ProcessPendingBroadcastsAsync(ct),
                    stoppingToken);

                var jitter = TimeSpan.FromSeconds(Random.Shared.Next(0, 15));
                await Task.Delay(WorkerSchedulePolicy.GetLoopDelay(jitter), stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in worker");
                await Task.Delay(WorkerSchedulePolicy.GetErrorRetryDelay(), stoppingToken);
            }
        }

        _logger.LogInformation("Community worker stopped");
    }
}
