using Api.Integrations.Keycloak;
using Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Orkyo.Community.Tests.Mocks;

namespace Orkyo.Community.Tests;

public class ApiWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly DatabaseFixture _databaseFixture;

    public MockKeycloakAdminService MockKeycloakAdminService { get; } = new();

    public ApiWebApplicationFactory(DatabaseFixture databaseFixture)
    {
        _databaseFixture = databaseFixture;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.Sources.Clear();
            config.AddJsonFile("appsettings.json", optional: false);
            config.AddJsonFile("appsettings.Test.json", optional: true);

            var port = _databaseFixture.DatabasePort;
            // Community uses one database for everything — both connection string keys point to it.
            var testConnectionString = $"Host=localhost;Port={port};Database={TestConstants.TenantDatabase};Username=postgres;Password=postgres";

            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Test",
                ["ConnectionStrings:Postgres"] = testConnectionString,
                ["ConnectionStrings:ControlPlane"] = testConnectionString,
                ["SMTP_HOST"] = "localhost",
                ["SMTP_PORT"] = "1025",
                ["SMTP_USE_SSL"] = "false",
                ["SMTP_FROM_EMAIL"] = "test@test.local",
                ["SMTP_FROM_NAME"] = "Test",
                ["APP_BASE_URL"] = "http://localhost:5173",
                ["CORS_ALLOWED_ORIGINS"] = "http://localhost:5173",
                ["FILE_STORAGE_PATH"] = "/tmp/orkyo-test-storage",
                ["OIDC_AUTHORITY"] = "http://test-keycloak.local/realms/test",
                ["KEYCLOAK_URL"] = "http://test-keycloak.local",
                ["KEYCLOAK_REALM"] = "test",
                ["KEYCLOAK_BACKEND_CLIENT_ID"] = "test-backend",
                ["KEYCLOAK_BACKEND_CLIENT_SECRET"] = "test-backend-secret",
                ["BFF_ENABLED"] = "true",
                ["BFF_REDIRECT_URI"] = "http://localhost/api/auth/bff/callback",
                ["BFF_ALLOWED_HOSTS"] = "orkyo.com,*.orkyo.com,localhost",
                ["BFF_COOKIE_SECURE"] = "false"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "TestScheme";
                options.DefaultChallengeScheme = "TestScheme";
                options.DefaultScheme = "TestScheme";
            })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("TestScheme", options => { });

            services.RemoveAll<IKeycloakAdminService>();
            services.AddSingleton<IKeycloakAdminService>(MockKeycloakAdminService);

            // Community has no break-glass store — nothing to replace here.

            // Re-register the NpgSql health check with the correct test connection string.
            // Program.cs captures the connection string value before WebApplicationFactory's
            // ConfigureAppConfiguration overrides run, so the health check would otherwise
            // use the appsettings.json default (localhost:5432) instead of the test port.
            var testDbCs = $"Host=localhost;Port={_databaseFixture.DatabasePort};Database={TestConstants.TenantDatabase};Username=postgres;Password=postgres";
            services.Configure<HealthCheckServiceOptions>(opts =>
            {
                var existing = opts.Registrations.FirstOrDefault(r => r.Name == "postgres");
                if (existing is not null) opts.Registrations.Remove(existing);
            });
            services.AddHealthChecks()
                .AddNpgSql(testDbCs, name: "postgres", tags: ["db", "ready"]);

        });
    }
}
