using Api.Reporting;
using Api.Services;
using Microsoft.Extensions.Options;
using Npgsql;
using Orkyo.Community.Tenant;

namespace Orkyo.Community.Api;

/// <summary>
/// Runs once at startup to ensure the single community tenant's Superset
/// datasource and dashboard bindings are provisioned.
///
/// If reporting is disabled (Reporting:Enabled=false) or Superset has not yet
/// been configured (no BaseUrl), the task logs a debug message and exits.
/// Provisioning failures are logged but never crash the API — the scheduler
/// shell must remain available even when reporting is unavailable.
/// </summary>
internal sealed class ReportingProvisioningStartupTask : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<ReportingProvisioningStartupTask> _logger;

    public ReportingProvisioningStartupTask(
        IServiceProvider services,
        ILogger<ReportingProvisioningStartupTask> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait until the host has fully started so that all middleware and health
        // checks are ready before we make outbound Superset calls.
        await Task.Yield();

        await using var scope = _services.CreateAsyncScope();
        var opts = scope.ServiceProvider.GetRequiredService<IOptions<ReportingOptions>>().Value;

        if (!opts.Enabled || string.IsNullOrWhiteSpace(opts.BaseUrl))
        {
            _logger.LogDebug("Reporting is not enabled or Superset base URL is not configured — skipping startup provisioning");
            return;
        }

        var tenantOpts = scope.ServiceProvider.GetRequiredService<IOptions<SingleTenantOptions>>().Value;
        var tenantId = tenantOpts.TenantId;

        // Derive the db_identifier from the connection string (the Postgres database name).
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var connStr = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings__DefaultConnection is required");
        var builder = new NpgsqlConnectionStringBuilder(connStr);
        var dbIdentifier = builder.Database
            ?? throw new InvalidOperationException("DefaultConnection must specify a Database");

        // Check whether provisioning has already been completed so we skip the
        // Superset API call on every restart when already provisioned.
        var db = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
        var isProvisioned = await IsAlreadyProvisionedAsync(db, tenantId, stoppingToken);
        if (isProvisioned)
        {
            _logger.LogDebug("Reporting already provisioned for community tenant {TenantId} — skipping", tenantId);
            return;
        }

        _logger.LogInformation(
            "Provisioning reporting for community tenant {TenantId} (db_identifier={DbIdentifier})",
            tenantId, dbIdentifier);

        var provisioner = scope.ServiceProvider.GetRequiredService<ITenantReportingProvisioner>();

        const int maxAttempts = 10;
        const int retryDelaySeconds = 30;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await provisioner.ProvisionAsync(tenantId, dbIdentifier, stoppingToken);
                _logger.LogInformation("Reporting provisioned successfully for community tenant {TenantId}", tenantId);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                _logger.LogWarning(ex,
                    "Reporting provisioning attempt {Attempt}/{MaxAttempts} failed; " +
                    "retrying in {Delay}s (Superset may not be ready yet)",
                    attempt, maxAttempts, retryDelaySeconds);
                await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds), stoppingToken);
            }
            catch (Exception ex)
            {
                // Non-fatal: the scheduler shell must remain up even if Superset is unreachable.
                _logger.LogWarning(ex,
                    "Reporting provisioning failed after {MaxAttempts} attempts for community tenant {TenantId}; " +
                    "reports will be unavailable until the next restart or manual reprovision",
                    maxAttempts, tenantId);
            }
        }
    }

    private static async Task<bool> IsAlreadyProvisionedAsync(
        IDbConnectionFactory db, Guid tenantId, CancellationToken ct)
    {
        await using var conn = db.CreateControlPlaneConnection();
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT status FROM public.tenant_reporting_state WHERE tenant_id = @id", conn);
        cmd.Parameters.AddWithValue("id", tenantId);
        var status = await cmd.ExecuteScalarAsync(ct) as string;
        return status == "provisioned";
    }
}
