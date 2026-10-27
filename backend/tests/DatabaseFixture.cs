using Npgsql;
using Orkyo.Community.Migrations;
using Orkyo.Foundation.Migrations;
using Orkyo.Migrations.Abstractions;
using Testcontainers.PostgreSql;

namespace Orkyo.Community.Tests;

/// <summary>
/// The shared database fixture: foundation's base resolves the server (the `./dev.sh check`
/// server on port 5442 under <c>CI=true</c>, or the Testcontainers one started here), and this
/// class creates and migrates Community's single database, seeds it and boots the host.
/// </summary>
public class DatabaseFixture : ProductDatabaseFixtureBase<Program, ApiWebApplicationFactory>, IAsyncLifetime
{
    private PostgreSqlContainer? _postgresContainer;

    public string TenantConnectionString => BuildConnectionString(TestConstants.TenantDatabase);

    protected override async Task<(int Port, string AdminConnectionString)> StartLocalServerAsync()
    {
        _postgresContainer = new PostgreSqlBuilder("postgres:16-alpine")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .WithDatabase("postgres")
            .Build();

        await _postgresContainer.StartAsync();
        return (_postgresContainer.GetMappedPublicPort(5432), _postgresContainer.GetConnectionString());
    }

    protected override async Task StopLocalServerAsync()
    {
        if (_postgresContainer is not null)
            await _postgresContainer.DisposeAsync();
    }

    protected override async Task CreateAndMigrateDatabasesAsync()
    {
        // Community uses a single database for everything — no separate control_plane.
        await CreateDatabaseAsync(TestConstants.TenantDatabase);

        var runner = BuildMigrationRunner(s => s.AddCommunityFoundationMigrations().AddCommunityMigrations());
        await runner.RunAsync(TenantConnectionString, MigrationTargetDatabase.ControlPlane, "orkyo:community:cp");
        await runner.RunAsync(TenantConnectionString, MigrationTargetDatabase.Tenant, "orkyo:community:tenant");

        await SeedTestDataAsync();
    }

    // Program.cs requires VALKEY_CONNECTION at startup and reads it before the factory's
    // configuration callback applies, so only the process environment reaches it. The
    // Singleton ConnectionMultiplexer factory only resolves when something actually uses
    // Valkey — health/live does not, and the integration tests in this fixture don't exercise
    // Valkey-backed code paths. A non-functional placeholder is enough to pass the presence check.
    protected override void BeforeFactoryStart()
        => Environment.SetEnvironmentVariable("VALKEY_CONNECTION", "localhost:6379,abortConnect=false");

    protected override ApiWebApplicationFactory CreateFactory() => new(this);

    private async Task SeedTestDataAsync()
    {
        // The test user and its admin membership of the single tenant, as an invitation
        // would create them. Nothing grants a membership implicitly any more (migration 3030).
        await using var conn = new NpgsqlConnection(TenantConnectionString);
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
}

[CollectionDefinition("Database collection")]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
    // Defines the shared collection — no implementation needed.
}
