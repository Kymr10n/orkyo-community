using Api.Configuration;
using Api.Integrations.Keycloak;
using Api.Middleware;
using Api.Models;
using Api.Repositories;
using Api.Security;
using Api.Security.Quotas;
using Api.Services;
using Api.Services.AutoSchedule;
using Api.Endpoints;
using Api.Endpoints.Admin;
using Orkyo.Community.Middleware;
using Orkyo.Community.Migrations;
using Orkyo.Community.Services;
using Orkyo.Community.Tenant;
using Orkyo.Foundation.Migrations;
using Orkyo.Migrator;
using Orkyo.Shared;
using Orkyo.Shared.Keycloak;
using FluentValidation;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = 10 * 1024 * 1024);

    // Community uses DefaultConnection for all DB access. Alias it to the names
    // foundation's ConfigurationValidator and DeploymentConfig expect so they work
    // without requiring operators to configure the same string twice.
    var defaultConn = builder.Configuration.GetConnectionString("DefaultConnection");
    if (!string.IsNullOrEmpty(defaultConn))
    {
        builder.Configuration["ConnectionStrings:Postgres"] = defaultConn;
        builder.Configuration["ConnectionStrings:ControlPlane"] = defaultConn;
    }

    if (args.Contains("--validate"))
        return ValidateMode.Run(builder.Configuration, builder.Environment.EnvironmentName);

    ConfigurationValidator.ValidateOrThrow(builder.Configuration, builder.Environment.EnvironmentName);

    builder.Host.UseSerilog((ctx, services, cfg) => cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .WriteTo.Console());

    // ── Core services ─────────────────────────────────────────────────────────
    builder.Services.ConfigureHttpJsonOptions(options =>
    {
        options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.SerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter(allowIntegerValues: false));
    });
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddHttpClient();
    builder.Services.AddResponseCompression();
    builder.Services.AddValidatorsFromAssemblyContaining<Api.Validators.CreateCriterionRequestValidator>(
        ServiceLifetime.Scoped);

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
    builder.Services.AddSingleton<IDbConnectionFactory, CommunityDbConnectionFactory>();
    builder.Services.AddSingleton<IOrgDbConnectionFactory>(
        sp => sp.GetRequiredService<IDbConnectionFactory>());

    // Single-tenant options + resolver (no subdomain/header lookup needed)
    builder.Services.Configure<SingleTenantOptions>(
        builder.Configuration.GetSection(SingleTenantOptions.SectionKey));
    builder.Services.AddSingleton<ITenantResolver, SingleTenantResolver>();

    // Keycloak
    builder.Services.AddSingleton(KeycloakOptions.FromConfiguration(builder.Configuration));
    builder.Services.AddHttpClient<IKeycloakAdminService, KeycloakAdminService>();

    // Community quota: all resources unlimited
    builder.Services.AddScoped<IQuotaEnforcer, CommunityQuotaEnforcer>();

    // Tenant + org contexts (resolved per-request by ContextEnrichmentMiddleware using SingleTenantResolver)
    builder.Services.AddScoped<TenantContext>(sp =>
    {
        var httpContext = sp.GetRequiredService<IHttpContextAccessor>().HttpContext!;
        return httpContext.Items.TryGetValue("TenantContext", out var ctx) && ctx is TenantContext tc
            ? tc
            : new TenantContext
            {
                TenantId = Guid.Empty,
                TenantSlug = "",
                TenantDbConnectionString = "",
                Tier = ServiceTier.Enterprise,
                Status = "active",
            };
    });
    builder.Services.AddScoped<OrgContext>(sp =>
    {
        var httpContext = sp.GetRequiredService<IHttpContextAccessor>().HttpContext!;
        if (!httpContext.Items.TryGetValue("TenantContext", out var ctx) || ctx is not TenantContext tenant)
            return new OrgContext { OrgId = Guid.Empty, OrgSlug = "", DbConnectionString = "" };
        return new OrgContext
        {
            OrgId = tenant.TenantId,
            OrgSlug = tenant.TenantSlug,
            DbConnectionString = tenant.TenantDbConnectionString,
        };
    });

    // ── Migration platform ────────────────────────────────────────────────────
    builder.Services.AddOrkyoMigrationPlatform();
    builder.Services.AddFoundationMigrations();
    builder.Services.AddCommunityMigrations();

    // ── Auth (JWT + BFF) ──────────────────────────────────────────────────────
    builder.Services.AddOrkyoAuthentication(builder.Configuration);
    builder.Services.AddBffAuthentication(builder.Configuration);

    // ── Security context (per-request, populated by ContextEnrichmentMiddleware)
    builder.Services.AddScoped<CurrentPrincipal>();
    builder.Services.AddScoped<ICurrentPrincipal>(sp => sp.GetRequiredService<CurrentPrincipal>());
    builder.Services.AddScoped<CurrentTenant>();
    builder.Services.AddScoped<ICurrentTenant>(sp => sp.GetRequiredService<CurrentTenant>());
    builder.Services.AddScoped<CurrentAuthorizationContext>();
    builder.Services.AddScoped<IAuthorizationContext>(
        sp => sp.GetRequiredService<CurrentAuthorizationContext>());

    // ── Foundation repositories ───────────────────────────────────────────────
    builder.Services.AddScoped<ICriteriaRepository, CriteriaRepository>();
    builder.Services.AddScoped<IFeedbackRepository, FeedbackRepository>();
    builder.Services.AddScoped<IGroupCapabilityRepository, GroupCapabilityRepository>();
    builder.Services.AddScoped<IRequestRepository, RequestRepository>();
    builder.Services.AddScoped<ISchedulingRepository, SchedulingRepository>();
    builder.Services.AddScoped<ISiteRepository, SiteRepository>();
    builder.Services.AddScoped<ISiteSettingsRepository, SiteSettingsRepository>();
    builder.Services.AddScoped<ISpaceCapabilityRepository, SpaceCapabilityRepository>();
    builder.Services.AddScoped<ISpaceGroupRepository, SpaceGroupRepository>();
    builder.Services.AddScoped<ISpaceRepository, SpaceRepository>();
    builder.Services.AddScoped<ITemplateRepository, TemplateRepository>();
    builder.Services.AddScoped<ITenantSettingsRepository, TenantSettingsRepository>();
    builder.Services.AddScoped<IUserPreferencesRepository, UserPreferencesRepository>();
    builder.Services.AddScoped<IAnnouncementRepository, AnnouncementRepository>();
    builder.Services.AddScoped<ISearchRepository, SearchRepository>();

    // ── Foundation domain services ────────────────────────────────────────────
    builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
    builder.Services.AddScoped<IEmailService, EmailService>();
    builder.Services.AddScoped<IIdentityLinkService, KeycloakIdentityLinkService>();
    builder.Services.AddScoped<IUserManagementService, UserManagementService>();
    builder.Services.AddScoped<ICriteriaService, CriteriaService>();
    builder.Services.AddScoped<ISiteService, SiteService>();
    builder.Services.AddScoped<ISiteSettingsService, SiteSettingsService>();
    builder.Services.AddScoped<ISpaceService, SpaceService>();
    builder.Services.AddScoped<IRequestService, RequestService>();
    builder.Services.AddScoped<ISchedulingService, SchedulingService>();
    builder.Services.AddScoped<ITenantSettingsService, TenantSettingsService>();
    builder.Services.AddScoped<ISessionService, SessionService>();
    builder.Services.AddScoped<IAnnouncementService, AnnouncementService>();
    builder.Services.AddScoped<IExportService, ExportService>();
    builder.Services.AddScoped<IPresetService, PresetService>();
    builder.Services.AddScoped<IStarterTemplateService, StarterTemplateService>();
    builder.Services.AddScoped<ITenantUserService, TenantUserService>();
    builder.Services.AddScoped<SchedulingProblemBuilder>();
    builder.Services.AddSingleton<SchedulingFeasibilityAnalyzer>();
    builder.Services.AddSingleton<ISchedulingSolver, OrToolsSchedulingSolver>();
    builder.Services.AddSingleton<ISchedulingSolver, GreedySchedulingSolver>();
    builder.Services.AddScoped<IAutoScheduleService, AutoScheduleService>();

    // ── Health checks ─────────────────────────────────────────────────────────
    var dbCs = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("ConnectionStrings__DefaultConnection is required");
    builder.Services.AddHealthChecks()
        .AddNpgSql(dbCs, name: "postgres", tags: ["db", "ready"]);

    var app = builder.Build();

    // ── Middleware pipeline ───────────────────────────────────────────────────
    app.UseResponseCompression();
    app.UseCors();
    app.UseMiddleware<SecurityHeadersMiddleware>();
    app.UseCorrelationId();
    app.UseRequestLogging();
    app.UseMiddleware<CacheControlMiddleware>();
    app.UseRouting();
    app.UseAuthentication();
    app.UseMiddleware<CsrfMiddleware>();
    app.UseAuthorization();
    app.UseMiddleware<SingleTenantMiddleware>();
    app.UseMiddleware<ContextEnrichmentMiddleware>();

    // ── Endpoints ─────────────────────────────────────────────────────────────
    app.MapHealthChecks("/health")
        .WithMetadata(new SkipTenantResolutionAttribute());

    app.MapBffAuthEndpoints();
    app.MapSessionEndpoints();
    app.MapAccountLifecycleEndpoints();

    // Admin
    app.MapAuditEndpoints();
    app.MapDiagnosticsAdminEndpoints();
    app.MapSettingsAdminEndpoints();
    app.MapUserAdminEndpoints();

    // Domain
    app.MapAnnouncementEndpoints();
    app.MapUserAnnouncementEndpoints();
    app.MapSecurityEndpoints();
    app.MapSiteEndpoints();
    app.MapFloorplanEndpoints();
    app.MapSpaceEndpoints();
    app.MapSpaceGroupEndpoints();
    app.MapGroupCapabilityEndpoints();
    app.MapSpaceCapabilityEndpoints();
    app.MapCriteriaEndpoints();
    app.MapRequestEndpoints();
    app.MapSchedulingEndpoints();
    app.MapAutoScheduleEndpoints();
    app.MapTemplateEndpoints();
    app.MapPresetEndpoints();
    app.MapExportEndpoints();
    app.MapUserPreferencesEndpoints();
    app.MapContactEndpoints();
    app.MapFeedbackEndpoints();
    app.MapSearchEndpoints();

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
