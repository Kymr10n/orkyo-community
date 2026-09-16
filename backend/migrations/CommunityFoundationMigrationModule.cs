using Orkyo.Foundation.Migrations;
using Orkyo.Migrations.Abstractions;

namespace Orkyo.Community.Migrations;

/// <summary>
/// Foundation migrations as consumed by Community, minus the tenant-phase feedback migrations.
///
/// Community collapses the control-plane and the single tenant into ONE database (same
/// <c>public</c> schema). Feedback lives in the control-plane table (migration 1170); the
/// tenant-phase feedback migrations would operate on that same shared-schema table — the
/// create (1240) collides with it (<c>42P07 relation "feedback" already exists</c>) and the
/// drop (1630) would destroy it. Community has no feedback feature, so those tenant-phase
/// migrations must never run here. In SaaS (separate control-plane and tenant databases) they
/// run normally and don't collide.
/// </summary>
public sealed class CommunityFoundationMigrationModule : IMigrationModule
{
    private readonly FoundationMigrationModule _inner = new();

    public string ModuleName => _inner.ModuleName; // "foundation"
    public int Order => _inner.Order;              // 1000

    // Foundation declares which tenant-phase migrations assume their own database
    // (MigrationScope.TenantDatabaseOnly: the @scope directive, or
    // FoundationMigrationModule.TenantDatabaseOnlyIds for the two legacy feedback files).
    // Filtering on the scope, not on a filename substring, means a future migration with
    // "feedback" in its name runs here unless foundation says otherwise.
    public IReadOnlyCollection<MigrationScript> GetMigrations() =>
        _inner.GetMigrations()
            .Where(s => s.Scope != MigrationScope.TenantDatabaseOnly)
            .ToList();
}
