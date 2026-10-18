using Api.Constants;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Orkyo.Community.Migrations;
using Orkyo.Foundation.Migrations;
using Orkyo.Migrations.Abstractions;
using Orkyo.Migrator;
using Testcontainers.PostgreSql;

namespace Orkyo.Community.Tests;

public class DatabaseFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _postgresContainer;

    public int DatabasePort { get; private set; }
    public string AdminConnectionString { get; private set; } = "";
    public ApiWebApplicationFactory Factory { get; private set; } = null!;

    /// <summary>
    /// The external-database mode: <c>CI=true</c> and a <c>ConnectionStrings__Postgres</c> value
    /// select a server on <c>localhost</c> at that value's port (user and password
    /// <c>postgres</c>) instead of Testcontainers. `./dev.sh check` uses it on port 5442.
    /// </summary>
    private static bool UseCiDatabase =>
        Environment.GetEnvironmentVariable("CI") == "true"
        && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ConnectionStrings__Postgres"));

    public HttpClient CreateAuthorizedClient(string tenantSlug = TestConstants.TenantSlug)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add(HeaderConstants.TenantSlug, tenantSlug);
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {TestConstants.TestBearerToken}");
        return client;
    }

    public async Task InitializeAsync()
    {
        if (UseCiDatabase)
        {
            DatabasePort = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")).Port;
            AdminConnectionString = $"Host=localhost;Port={DatabasePort};Database=postgres;Username=postgres;Password=postgres";
        }
        else
        {
            _postgresContainer = new PostgreSqlBuilder("postgres:16-alpine")
                .WithUsername("postgres")
                .WithPassword("postgres")
                .WithDatabase("postgres")
                .Build();

            await _postgresContainer.StartAsync();
            DatabasePort = _postgresContainer.GetMappedPublicPort(5432);
            AdminConnectionString = _postgresContainer.GetConnectionString();
        }

        DatabaseTestUtils.SetDatabasePort(DatabasePort);
        await CreateAndMigrateDatabasesAsync();

        // Program.cs reads several config values directly from builder.Configuration
        // BEFORE WebApplicationFactory's ConfigureAppConfiguration callback runs.
        // For those, the only timing-safe override is the process environment.
        // Set placeholder values for the runtime-required keys here so the host
        // builds; the actual integration-test behaviour is unaffected because
        // the corresponding services are lazy-resolved and never used by the
        // tests we run today (health checks + endpoint routing).
        Environment.SetEnvironmentVariable("VALKEY_CONNECTION",
            "localhost:6379,abortConnect=false");
        Environment.SetEnvironmentVariable("ORKYO_MASTER_ENCRYPTION_KEY",
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=");

        Factory = new ApiWebApplicationFactory(this);
        _ = Factory.Services; // Eagerly start the server; CreateClient() no longer does this lazily in MvcTesting 10.x
    }

    public string TenantConnectionString => BuildConnectionString(TestConstants.TenantDatabase);

    private string BuildConnectionString(string database) =>
        new NpgsqlConnectionStringBuilder(AdminConnectionString) { Database = database }.ConnectionString;

    private async Task CreateAndMigrateDatabasesAsync()
    {
        // Community uses a single database for everything — no separate control_plane.
        await CreateDatabaseAsync(TestConstants.TenantDatabase);

        var cs = BuildConnectionString(TestConstants.TenantDatabase);
        var runner = BuildRunner();
        await runner.RunAsync(cs, MigrationTargetDatabase.ControlPlane, "orkyo:community:cp");
        await runner.RunAsync(cs, MigrationTargetDatabase.Tenant, "orkyo:community:tenant");

        await SeedTestDataAsync();
    }

    private async Task SeedTestDataAsync()
    {
        // The test user and its admin membership of the single tenant, as an invitation
        // would create them. Nothing grants a membership implicitly any more (migration 3030).
        await using var conn = new NpgsqlConnection(BuildConnectionString(TestConstants.TenantDatabase));
        await conn.OpenAsync();

        await using var userCmd = new NpgsqlCommand(
            @"INSERT INTO users (id, email, display_name, status, created_at, updated_at)
              VALUES (@id, @email, @name, 'active', NOW(), NOW())
              ON CONFLICT (id) DO NOTHING;
              INSERT INTO tenant_memberships (user_id, tenant_id, role, status, created_at, updated_at)
              VALUES (@id, '00000000-0000-0000-0000-000000000001', 'admin', 'active', NOW(), NOW())
              ON CONFLICT (user_id, tenant_id) DO NOTHING", conn);
        userCmd.Parameters.AddWithValue("id", TestConstants.UserId);
        userCmd.Parameters.AddWithValue("email", "test@orkyo.example");
        userCmd.Parameters.AddWithValue("name", "Test User");
        await userCmd.ExecuteNonQueryAsync();

        // The site-admin probe (RateLimitWiringTests) is a known, linked user: access is by
        // invitation only, so a Keycloak subject nobody provisioned no longer yields a principal.
        await using var adminCmd = new NpgsqlCommand(
            @"INSERT INTO users (id, email, display_name, status, created_at, updated_at)
              VALUES (@id, @email, 'Site Admin', 'active', NOW(), NOW())
              ON CONFLICT (id) DO NOTHING;
              INSERT INTO user_identities (user_id, provider, provider_subject, provider_email, created_at)
              VALUES (@id, 'keycloak', @subject, @email, NOW())
              ON CONFLICT DO NOTHING", conn);
        adminCmd.Parameters.AddWithValue("id", SiteAdminProbe.UserId);
        adminCmd.Parameters.AddWithValue("email", SiteAdminProbe.Email);
        adminCmd.Parameters.AddWithValue("subject", SiteAdminProbe.Subject);
        await adminCmd.ExecuteNonQueryAsync();

        await using var criteriaCmd = new NpgsqlCommand(@"
            INSERT INTO criteria (name, description, data_type, enum_values, created_at, updated_at)
            VALUES
                ('seed_boolean', 'Seed Boolean criterion', 'Boolean', NULL,                              NOW(), NOW()),
                ('seed_number',  'Seed Number criterion',  'Number',  NULL,                              NOW(), NOW()),
                ('seed_string',  'Seed String criterion',  'String',  NULL,                              NOW(), NOW()),
                ('seed_enum',    'Seed Enum criterion',    'Enum',    '[""Option A"",""Option B""]'::jsonb, NOW(), NOW())
            ON CONFLICT (name) DO NOTHING", conn);
        await criteriaCmd.ExecuteNonQueryAsync();
    }

    private async Task CreateDatabaseAsync(string dbName)
    {
        await using var conn = new NpgsqlConnection(AdminConnectionString);
        await conn.OpenAsync();
        await using var check = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = @n", conn);
        check.Parameters.AddWithValue("n", dbName);
        if (await check.ExecuteScalarAsync() is not null) return;
        await using var create = new NpgsqlCommand($"CREATE DATABASE \"{dbName}\"", conn);
        await create.ExecuteNonQueryAsync();
    }

    private static MigrationRunner BuildRunner()
    {
        var services = new ServiceCollection()
            .AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning))
            .AddOrkyoMigrationPlatform()
            .AddCommunityFoundationMigrations()
            .AddCommunityMigrations()
            .BuildServiceProvider();
        return services.GetRequiredService<MigrationRunner>();
    }

    public async Task DisposeAsync()
    {
        Factory?.Dispose();
        if (_postgresContainer is not null)
            await _postgresContainer.DisposeAsync();
    }
}

[CollectionDefinition("Database collection")]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
    // Defines the shared collection — no implementation needed.
}
