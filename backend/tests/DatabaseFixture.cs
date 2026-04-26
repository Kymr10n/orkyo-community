using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Orkyo.Foundation.Migrations;
using Orkyo.Migrations.Abstractions;
using Orkyo.Migrator;
using Orkyo.Community.Migrations;
using Testcontainers.PostgreSql;

namespace Orkyo.Community.Tests;

public class DatabaseFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _postgresContainer;

    public int DatabasePort { get; private set; }
    public string AdminConnectionString { get; private set; } = "";
    public ApiWebApplicationFactory Factory { get; private set; } = null!;

    private bool UseCiDatabase =>
        Environment.GetEnvironmentVariable("CI") == "true"
        && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ConnectionStrings__Postgres"));

    public HttpClient CreateAuthorizedClient(string tenantSlug = TestConstants.TenantSlug)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Slug", tenantSlug);
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {TestConstants.TestBearerToken}");
        return client;
    }

    public async Task InitializeAsync()
    {
        if (UseCiDatabase)
        {
            DatabasePort = 5432;
            AdminConnectionString = "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres";
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
        Factory = new ApiWebApplicationFactory(this);
    }

    private string BuildConnectionString(string database) =>
        new NpgsqlConnectionStringBuilder(AdminConnectionString) { Database = database }.ConnectionString;

    private async Task CreateAndMigrateDatabasesAsync()
    {
        await CreateDatabaseAsync("control_plane");
        await CreateDatabaseAsync(TestConstants.TenantDatabase);

        var runner = BuildRunner();
        await runner.RunAsync(BuildConnectionString("control_plane"),
            MigrationTargetDatabase.ControlPlane, "orkyo:control-plane");
        await runner.RunAsync(BuildConnectionString(TestConstants.TenantDatabase),
            MigrationTargetDatabase.Tenant, $"orkyo:tenant:{TestConstants.TenantDatabase}");

        await SeedTestDataAsync();
    }

    private async Task SeedTestDataAsync()
    {
        var cpConn = BuildConnectionString("control_plane");
        await using var seedConn = new NpgsqlConnection(cpConn);
        await seedConn.OpenAsync();

        // Seed test tenant at Enterprise tier
        await using var seedCmd = new NpgsqlCommand(
            @"INSERT INTO tenants (slug, display_name, status, db_identifier, tier, created_at, updated_at)
              VALUES (@slug, 'Test Organization', 'active', @db, 2, NOW(), NOW())
              ON CONFLICT (slug) DO UPDATE SET tier = 2, db_identifier = @db", seedConn);
        seedCmd.Parameters.AddWithValue("slug", TestConstants.TenantSlug);
        seedCmd.Parameters.AddWithValue("db", TestConstants.TenantDatabase);
        await seedCmd.ExecuteNonQueryAsync();

        // Seed test user
        await using var userCmd = new NpgsqlCommand(
            @"INSERT INTO users (id, email, display_name, status, created_at, updated_at)
              VALUES (@id, @email, @name, 'active', NOW(), NOW())
              ON CONFLICT (id) DO NOTHING", seedConn);
        userCmd.Parameters.AddWithValue("id", new Guid("11111111-1111-1111-1111-111111111111"));
        userCmd.Parameters.AddWithValue("email", "test@orkyo.example");
        userCmd.Parameters.AddWithValue("name", "Test User");
        await userCmd.ExecuteNonQueryAsync();

        // Seed test user as admin of test tenant
        await using var memberCmd = new NpgsqlCommand(
            @"INSERT INTO tenant_memberships (user_id, tenant_id, role, status, created_at, updated_at)
              SELECT @userId, t.id, 'admin', 'active', NOW(), NOW()
              FROM tenants t WHERE t.slug = @slug
              ON CONFLICT DO NOTHING", seedConn);
        memberCmd.Parameters.AddWithValue("userId", new Guid("11111111-1111-1111-1111-111111111111"));
        memberCmd.Parameters.AddWithValue("slug", TestConstants.TenantSlug);
        await memberCmd.ExecuteNonQueryAsync();

        // Seed one criterion of each data type for stable test data
        var tenantConn = BuildConnectionString(TestConstants.TenantDatabase);
        await using var tenantSeedConn = new NpgsqlConnection(tenantConn);
        await tenantSeedConn.OpenAsync();
        await using var criteriaCmd = new NpgsqlCommand(@"
            INSERT INTO criteria (name, description, data_type, enum_values, created_at, updated_at)
            VALUES
                ('seed_boolean', 'Seed Boolean criterion', 'Boolean', NULL,                              NOW(), NOW()),
                ('seed_number',  'Seed Number criterion',  'Number',  NULL,                              NOW(), NOW()),
                ('seed_string',  'Seed String criterion',  'String',  NULL,                              NOW(), NOW()),
                ('seed_enum',    'Seed Enum criterion',    'Enum',    '[""Option A"",""Option B""]'::jsonb, NOW(), NOW())
            ON CONFLICT (name) DO NOTHING", tenantSeedConn);
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
            .AddFoundationMigrations()
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
