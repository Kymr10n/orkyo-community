using Api.Configuration;
using Api.Middleware;
using Api.Security.Quotas;
using Api.Services;
using Microsoft.Extensions.Options;
using Orkyo.Community;
using Orkyo.Community.Api.Endpoints;
using Orkyo.Community.Middleware;
using Orkyo.Community.Migrations;
using Orkyo.Community.Services;
using Orkyo.Community.Tenant;
using Orkyo.Foundation.Migrations;
using Orkyo.Foundation.Observability;
using Orkyo.Migrator;
using Orkyo.Shared;
using Serilog;
using StackExchange.Redis;

OrkyoObservability.InitBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = 10 * 1024 * 1024);

    // Community uses DefaultConnection for all DB access. Alias it to the names
    // foundation's ConfigurationValidator and DeploymentConfig expect so they work
    // without requiring operators to configure the same string twice.
    var defaultConn = builder.Configuration.GetConnectionString(CommunityConfigKeys.DefaultConnection);
    if (!string.IsNullOrEmpty(defaultConn))
    {
        builder.Configuration["ConnectionStrings:Postgres"] = defaultConn;
        builder.Configuration["ConnectionStrings:ControlPlane"] = defaultConn;
    }

    if (args.Contains("--validate"))
        return ValidateMode.Run(builder.Configuration, builder.Environment.EnvironmentName);

    ConfigurationValidator.ValidateOrThrow(builder.Configuration, builder.Environment.EnvironmentName);

    builder.UseOrkyoLogging("orkyo-community-api");

    // ── Foundation services ───────────────────────────────────────────────────
    builder.Services.AddFoundationServices(builder.Configuration);
    builder.Services.AddResponseCompression();

    // ── CORS ──────────────────────────────────────────────────────────────────
    builder.Services.AddOrkyoApiCors(builder.Configuration, builder.Environment);

    // ── Community infrastructure ──────────────────────────────────────────────
    builder.Services.AddSingleton(DeploymentConfig.FromConfiguration(builder.Configuration));

    // Single-tenant DB factory — all connections map to the one community database
    builder.Services.AddSingleton<IDbConnectionFactory>(
        _ => SingleTenantDbConnectionFactory.FromConfiguration(builder.Configuration));
    builder.Services.AddSingleton<IOrgDbConnectionFactory>(
        sp => sp.GetRequiredService<IDbConnectionFactory>());

    // Single-tenant options + resolver (no subdomain/header lookup needed)
    builder.Services.Configure<SingleTenantOptions>(
        builder.Configuration.GetSection(SingleTenantOptions.SectionKey));
    builder.Services.AddSingleton<ITenantResolver, SingleTenantResolver>();

    // Community quota: all resources unlimited
    // Foundation provides the no-op enforcer — community has no tier-based limits.
    builder.Services.AddScoped<IQuotaEnforcer, NoOpQuotaEnforcer>();

    builder.Services.AddScoped<IAdminAuditService, CommunityAuditService>();

    var valkeyCs = builder.Configuration[ConfigKeys.ValkeyConnection]
        ?? throw new InvalidOperationException($"Valkey connection string is required. Set {ConfigKeys.ValkeyConnection}.");
    builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(valkeyCs));
    builder.Services.AddSingleton<IBreakGlassSessionStore, NullBreakGlassSessionStore>();

    builder.Services.AddScoped<CommunityJitProvisioningMiddleware>();

    // Tenant + org contexts — community always has exactly one fixed tenant.
    // Read directly from SingleTenantOptions so resolution is never affected by
    // middleware ordering (avoids caching Guid.Empty sentinel when something before
    // SingleTenantMiddleware triggers the scoped factory for the first time).
    builder.Services.AddScoped<TenantContext>(sp =>
    {
        var opts = sp.GetRequiredService<IOptions<SingleTenantOptions>>().Value;
        var connStr = sp.GetRequiredService<IConfiguration>().GetConnectionString(CommunityConfigKeys.DefaultConnection)
            ?? throw new InvalidOperationException("ConnectionStrings__DefaultConnection is required");
        return new TenantContext
        {
            TenantId = opts.TenantId,
            TenantSlug = opts.TenantSlug,
            TenantDbConnectionString = connStr,
            Status = TenantStatusConstants.Active,
        };
    });
    builder.Services.AddScoped<OrgContext>(sp =>
    {
        var opts = sp.GetRequiredService<IOptions<SingleTenantOptions>>().Value;
        var connStr = sp.GetRequiredService<IConfiguration>().GetConnectionString(CommunityConfigKeys.DefaultConnection)
            ?? throw new InvalidOperationException("ConnectionStrings__DefaultConnection is required");
        return new OrgContext
        {
            OrgId = opts.TenantId,
            OrgSlug = opts.TenantSlug,
            DbConnectionString = connStr,
        };
    });

    // ── OpenAPI / Swagger (reporting-v1 document) ────────────────────────────
    builder.Services.AddOrkyoReportingSwagger();

    // ── Rate limiting ─────────────────────────────────────────────────────────
    // Registers every named policy the mapped foundation endpoints require (admin-operations,
    // password-change, session-bootstrap, contact-form, bff-auth, reporting-api). Owned by
    // foundation so a new foundation endpoint can't 500 here on a policy Community forgot to add.
    // Community has no edition-specific policies to layer on top.
    builder.Services.AddFoundationRateLimiting();

    // ── Migration platform ────────────────────────────────────────────────────
    builder.Services.AddOrkyoMigrationPlatform();
    builder.Services.AddCommunityFoundationMigrations();
    builder.Services.AddCommunityMigrations();

    // ── Health checks ─────────────────────────────────────────────────────────
    var dbCs = builder.Configuration.GetConnectionString(CommunityConfigKeys.DefaultConnection)
        ?? throw new InvalidOperationException("ConnectionStrings__DefaultConnection is required");
    builder.Services.AddHealthChecks()
        .AddNpgSql(dbCs, name: "postgres", tags: ["db", "ready"]);

    var app = builder.Build();

    // ── Middleware pipeline ───────────────────────────────────────────────────
    app.UseFoundationMiddleware();
    app.UseResponseCompression();
    app.UseCors();
    app.UseMiddleware<SecurityHeadersMiddleware>();
    app.UseRouting();
    app.UseAuthentication();
    app.UseMiddleware<CsrfMiddleware>();
    app.UseAuthorization();
    if (!builder.Configuration.GetValue<bool>("DISABLE_RATE_LIMITING"))
        app.UseRateLimiter();
    app.UseMiddleware<CommunityJitProvisioningMiddleware>();
    app.UseMiddleware<SingleTenantMiddleware>();
    app.UseMiddleware<ContextEnrichmentMiddleware>();

    // ── Endpoints ─────────────────────────────────────────────────────────────
    app.MapOrkyoHealthEndpoints();
    app.UseOrkyoReportingSwaggerUI();

    // Foundation endpoints
    app.MapFoundationEndpoints();

    // Community-specific endpoints
    app.MapCommunityTenantEndpoints();

    app.Run();
    return 0;
}
catch (Exception ex) when (ex is not OperationCanceledException)
{
    Log.Fatal(ex, "Community API terminated unexpectedly");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
