using Orkyo.Community.Migrations;

namespace Orkyo.Community.Tests.Migrations;

/// <summary>
/// The forced "Demo Office" seed (3010/3030) was retired in favour of the opt-in `office`
/// starter preset: a fresh install must get nothing it did not ask for. Deleting the files is
/// the retirement mechanism (docs/migrations.md, "Retiring a Migration"), so this pins that
/// nothing demo-shaped ships in the module any more, while the bootstrap and tenant seed do.
/// </summary>
public class CommunityMigrationModuleTests
{
    private static readonly IReadOnlyList<string> Ids =
        new CommunityMigrationModule().GetMigrations().Select(m => m.Id).ToList();

    [Fact]
    public void ShipsNoDemoSeed()
    {
        Ids.Should().NotContain(id => id.Contains("demo", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void StillShipsTheBootstrapAndTheTenantSeed()
    {
        Ids.Should().Contain(id => id.StartsWith("3000."));
        Ids.Should().Contain(id => id.StartsWith("3020."));
    }
}
