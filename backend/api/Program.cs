using Api.Configuration;
using Api.Middleware;
using Api.Security.Quotas;
using Api.Services;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
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
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            var rawOrigins = builder.Configuration[ConfigKeys.CorsAllowedOrigins] ?? "";
            var allowedOrigins = rawOrigins
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (allowedOrigins.Count > 0)
            {
                policy.WithOrigins([.. allowedOrigins])
                    .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
                    .WithHeaders("Content-Type", "Authorization",
                        Api.Constants.HeaderConstants.TenantSlug,
                        Api.Constants.HeaderConstants.CorrelationId,
                        Api.Constants.HeaderConstants.CsrfToken)
                    .AllowCredentials();
            }
            else if (!builder.Environment.IsProduction())
            {
                policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
            }
            else
            {
                throw new InvalidOperationException(
                    "CORS_ALLOWED_ORIGINS must be set in production.");
            }
        });
    });

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

    var redisCs = builder.Configuration[ConfigKeys.RedisConnection]
        ?? throw new InvalidOperationException($"Redis connection string is required. Set {ConfigKeys.RedisConnection}.");
    builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisCs));
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
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("reporting-v1", new OpenApiInfo
        {
            Title = "Orkyo Reporting API",
            Version = "v1",
            Description = "Read-only, tenant-scoped reporting endpoints for Power BI, Excel, Metabase, Superset, and custom integrations. " +
                          "Authenticate with an orkyo_rpt_* reporting token in the Authorization header. " +
                          "Tenant isolation is enforced server-side — no tenantId parameter accepted.",
        });
        c.AddSecurityDefinition("ReportingToken", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "orkyo_rpt_<prefix>_<secret>",
            In = ParameterLocation.Header,
            Description = "Reporting API token in the format: Bearer orkyo_rpt_<prefix>_<secret>",
        });
        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "ReportingToken" }
                },
                Array.Empty<string>()
            }
        });
        c.DocInclusionPredicate((docName, apiDesc) =>
            docName == "reporting-v1" &&
            (apiDesc.RelativePath?.StartsWith("api/reporting", StringComparison.OrdinalIgnoreCase) ?? false));
    });

    // ── Rate limiting (reporting endpoints only) ──────────────────────────────
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddPolicy("reporting-api", ctx =>
            System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
                ctx.User?.Identity?.Name ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                {
                    PermitLimit = 60,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                }));
    });

    // ── Migration platform ────────────────────────────────────────────────────
    builder.Services.AddOrkyoMigrationPlatform();
    builder.Services.AddFoundationMigrations();
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
    var healthCheckOptions = new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        ResponseWriter = WriteHealthCheckResponse
    };
    app.MapHealthChecks("/health", healthCheckOptions).AsInfrastructureEndpoint();
    app.MapGet("/health/live", () => Results.Ok(new { status = "ok" })).AsInfrastructureEndpoint();
    app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready")
    }).AsInfrastructureEndpoint();

    // Swagger UI (reporting-v1 document only)
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/reporting-v1/swagger.json", "Orkyo Reporting API v1");
        c.RoutePrefix = "swagger/reporting";
    });

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

static async Task WriteHealthCheckResponse(
    HttpContext context,
    Microsoft.Extensions.Diagnostics.HealthChecks.HealthReport report)
{
    context.Response.ContentType = "application/json";
    var result = System.Text.Json.JsonSerializer.Serialize(new
    {
        status = report.Status.ToString(),
        utc = DateTime.UtcNow.ToString("O"),
        checks = report.Entries.Select(e => new
        {
            name = e.Key,
            status = e.Value.Status.ToString(),
            description = e.Value.Description,
            duration = e.Value.Duration.TotalMilliseconds
        })
    });
    await context.Response.WriteAsync(result);
}

public partial class Program { }
