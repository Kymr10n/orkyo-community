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
        // tenants and tenant_memberships are compat views in community — nothing to seed for those.
        await using var conn = new NpgsqlConnection(BuildConnectionString(TestConstants.TenantDatabase));
        await conn.OpenAsync();

        await using var userCmd = new NpgsqlCommand(
            @"INSERT INTO users (id, email, display_name, status, created_at, updated_at)
              VALUES (@id, @email, @name, 'active', NOW(), NOW())
              ON CONFLICT (id) DO NOTHING", conn);
        userCmd.Parameters.AddWithValue("id", new Guid("11111111-1111-1111-1111-111111111111"));
        userCmd.Parameters.AddWithValue("email", "test@orkyo.example");
        userCmd.Parameters.AddWithValue("name", "Test User");
        await userCmd.ExecuteNonQueryAsync();

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
