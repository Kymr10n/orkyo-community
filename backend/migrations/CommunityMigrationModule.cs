using Orkyo.Migrations.Abstractions;
using Orkyo.Migrator;

namespace Orkyo.Community.Migrations;

/// <summary>
/// Community migrations targeting the single deployment database.
/// SQL files live under <c>sql/tenant/</c> and run against the default database.
/// Order 3000 — runs after foundation (1000) migrations.
/// </summary>
public sealed class CommunityMigrationModule : IMigrationModule
{
    public string ModuleName => "community";
    public int Order => 3000;

    public IReadOnlyCollection<MigrationScript> GetMigrations() =>
        EmbeddedSqlLoader.LoadFromAssembly(typeof(CommunityMigrationModule).Assembly, ModuleName);
}
