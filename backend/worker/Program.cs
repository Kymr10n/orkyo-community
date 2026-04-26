using Serilog;

// TODO (Phase 8): Port UserLifecycleService from orkyo-saas/backend/worker to
// orkyo-foundation, then register it here. Decision: UserLifecycleService is
// product-agnostic (GDPR inactivity management) → move to foundation, consume
// in both saas and community workers.
//
// Until then, community worker is a stub that starts and idles cleanly.
// The GDPR user lifecycle will NOT run until Phase 8 is complete.

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Starting Orkyo Community Worker (stub — user lifecycle not yet implemented)");

    using var host = Host.CreateDefaultBuilder(args)
        .UseSerilog()
        .ConfigureServices((_, services) =>
        {
            services.AddHostedService<StubWorkerService>();
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

/// <summary>
/// Placeholder service — idles until UserLifecycleService is ported from saas to foundation.
/// </summary>
internal sealed class StubWorkerService : BackgroundService
{
    private readonly ILogger<StubWorkerService> _logger;

    public StubWorkerService(ILogger<StubWorkerService> logger) => _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogWarning(
            "Community worker is a stub. UserLifecycleService must be moved to " +
            "orkyo-foundation before GDPR user lifecycle runs. See docs/community-setup-spec.md Phase 8.");

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
