using Api.Models.Preset;
using Npgsql;

namespace Orkyo.Community.Tests.Startup;

/// <summary>
/// The whole first-start path through the real host: ORKYO_STARTER_TEMPLATE set, the API built,
/// the preset applied to the single tenant — and built again, applied no second time. Writes
/// the office preset's rows into the shared test database; no other Community test counts
/// resources, and the rows are the same on every run (foundation adopts them by code).
/// </summary>
[Collection("Database collection")]
public class StarterTemplateBootstrapIntegrationTests
{
    private readonly DatabaseFixture _databaseFixture;

    public StarterTemplateBootstrapIntegrationTests(DatabaseFixture databaseFixture)
    {
        _databaseFixture = databaseFixture;
    }

    private async Task<long> CountAsync(string sql)
    {
        var cs = $"Host=localhost;Port={_databaseFixture.DatabasePort};Database={TestConstants.TenantDatabase};Username=postgres;Password=postgres";
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    private ApiWebApplicationFactory StartWithStarter(string key) =>
        new(_databaseFixture, new Dictionary<string, string?> { [CommunityConfigKeys.StarterTemplate] = key });

    [Fact]
    public async Task FirstStart_AppliesTheStarter_SecondStart_SkipsIt()
    {
        using (var first = StartWithStarter(StarterTemplateCatalog.Office))
        {
            // Building the host runs the hosted services; touching Services forces it.
            _ = first.Services;
            var health = await first.CreateClient().GetAsync("/health/live");
            health.EnsureSuccessStatusCode();
        }

        (await CountAsync("SELECT count(*) FROM preset_applications WHERE preset_id = 'office-v1'")).Should().Be(1);
        (await CountAsync("SELECT count(*) FROM resources WHERE code IN ('MR-A','MR-B','OW-1','FB-1')")).Should().Be(4);

        using (var second = StartWithStarter(StarterTemplateCatalog.Office))
        {
            _ = second.Services;
            (await second.CreateClient().GetAsync("/health/live")).EnsureSuccessStatusCode();
        }

        (await CountAsync("SELECT count(*) FROM preset_applications WHERE preset_id = 'office-v1'")).Should().Be(1);
        (await CountAsync("SELECT count(*) FROM resources WHERE code IN ('MR-A','MR-B','OW-1','FB-1')")).Should().Be(4);
    }
}
