using Orkyo.Community.Migrations;
using Orkyo.Foundation.Migrations;
using Orkyo.Migrations.Abstractions;

namespace Orkyo.Community.Tests.Migrations;

/// <summary>
/// Community collapses control-plane and the single tenant into one shared database, so the
/// tenant-phase feedback migrations (create 1240, drop 1630) must not run — they would collide
/// with, or drop, the control-plane feedback table (1170). See <see cref="CommunityFoundationMigrationModule"/>.
/// </summary>
public class CommunityFoundationMigrationModuleTests
{
    private static readonly IReadOnlyCollection<MigrationScript> Filtered =
        new CommunityFoundationMigrationModule().GetMigrations();

    [Fact]
    public void ExcludesEveryTenantPhaseFeedbackMigration()
    {
        Filtered.Should().NotContain(m =>
            m.TargetDatabase == MigrationTargetDatabase.Tenant && m.Id.Contains("feedback"));
    }

    [Fact]
    public void KeepsControlPlaneFeedbackTable()
    {
        Filtered.Should().Contain(m =>
            m.Id == "1170.foundation.feedback" && m.TargetDatabase == MigrationTargetDatabase.ControlPlane);
    }

    [Fact]
    public void RemovesOnlyTenantPhaseFeedbackMigrations()
    {
        // Version-agnostic: whichever foundation version is pinned, the only migrations the wrapper
        // drops are tenant-phase feedback ones (the legacy create 1240, and — once foundation ships
        // it — the drop 1630). Nothing else is ever filtered.
        var full = new FoundationMigrationModule().GetMigrations();
        var removed = full.Where(m => Filtered.All(f => f.Id != m.Id)).ToList();

        removed.Should().OnlyContain(m =>
            m.TargetDatabase == MigrationTargetDatabase.Tenant && m.Id.Contains("feedback"));
        removed.Should().Contain(m => m.Id == "1240.foundation.feedback");
    }
}
