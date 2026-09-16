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
    public void RemovesExactlyWhatFoundationScopesAsTenantDatabaseOnly()
    {
        // Version-agnostic: whichever foundation version is pinned, the wrapper drops exactly the
        // scripts foundation scopes TenantDatabaseOnly (the @scope directive, or the two legacy
        // feedback ids foundation marks by name) and nothing else.
        var full = new FoundationMigrationModule().GetMigrations();
        var removed = full.Where(m => Filtered.All(f => f.Id != m.Id)).Select(m => m.Id).ToList();

        removed.Should().BeEquivalentTo(
            full.Where(m => m.Scope == MigrationScope.TenantDatabaseOnly).Select(m => m.Id));
        removed.Should().Contain("1240.foundation.feedback");
        removed.Should().BeSubsetOf(FoundationMigrationModule.TenantDatabaseOnlyIds.Concat(
            full.Where(m => m.Scope == MigrationScope.TenantDatabaseOnly).Select(m => m.Id)));
    }
}
