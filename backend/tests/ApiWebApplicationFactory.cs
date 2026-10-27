using Api.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
using Orkyo.Shared;

namespace Orkyo.Community.Tests;

/// <summary>
/// The Community test host: foundation's product base supplies the shared configuration, the
/// test authentication scheme and the Keycloak mock; this class adds what is Community-specific.
/// </summary>
public class ApiWebApplicationFactory : ProductWebApplicationFactoryBase<Program>
{
    private readonly DatabaseFixture _databaseFixture;
    private readonly IReadOnlyDictionary<string, string?> _extraConfiguration;

    /// <param name="extraConfiguration">
    /// Keys layered over the fixture's configuration, for a test that needs the host to start
    /// differently (for example <c>ORKYO_STARTER_TEMPLATE</c>). The shared fixture passes none.
    /// </param>
    public ApiWebApplicationFactory(
        DatabaseFixture databaseFixture,
        IReadOnlyDictionary<string, string?>? extraConfiguration = null)
    {
        _databaseFixture = databaseFixture;
        _extraConfiguration = extraConfiguration ?? new Dictionary<string, string?>();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Validate the real container. ASP.NET Core only does this in Development, and tests run
        // in their own environment — so a singleton capturing a scoped service passed here and
        // failed at the first `./dev.sh rebuild` instead. The base registers ServiceProviderOptions
        // through IOptions, which the minimal-hosting DefaultServiceProviderFactory never reads;
        // this call reaches it through UseServiceProviderFactory. SmokeTests.TestHost_ValidatesScopes
        // guards it.
        builder.UseDefaultServiceProvider(o =>
        {
            o.ValidateScopes = true;
            o.ValidateOnBuild = true;
        });

        base.ConfigureWebHost(builder);
    }

    protected override void AddProductConfiguration(IDictionary<string, string?> configuration)
    {
        // Community uses one database for everything — every connection-string key points to it.
        // SingleTenantResolver reads DefaultConnection.
        var connectionString = _databaseFixture.TenantConnectionString;
        configuration[ConfigKeys.ConnectionStringPostgresPath] = connectionString;
        configuration[ConfigKeys.ConnectionStringControlPlanePath] = connectionString;
        configuration[$"ConnectionStrings:{CommunityConfigKeys.DefaultConnection}"] = connectionString;
    }

    // Last source, so a test's keys win over the fixture's.
    protected override void ConfigureAppConfigurationSources(IConfigurationBuilder config)
        => config.AddInMemoryCollection(_extraConfiguration);

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        // Re-register the NpgSql health check with the correct test connection string.
        // Program.cs captures the connection string value before WebApplicationFactory's
        // ConfigureAppConfiguration overrides run, so the health check would otherwise
        // use the appsettings.json default (localhost:5432) instead of the test port.
        // The test host has no Valkey either: the readiness probe AddOrkyoValkey registers
        // (foundation 1.6.9) would turn /health into 503 here, so it goes too.
        services.Configure<HealthCheckServiceOptions>(opts =>
        {
            foreach (var name in new[] { "postgres", "valkey" })
            {
                var existing = opts.Registrations.FirstOrDefault(r => r.Name == name);
                if (existing is not null) opts.Registrations.Remove(existing);
            }
        });
        services.AddHealthChecks()
            .AddPostgresCheck(NpgsqlDataSource.Create(_databaseFixture.TenantConnectionString), "postgres", "db", "ready");
    }
}
