using Orkyo.Migrations.Abstractions;

namespace Orkyo.Community.Migrations;

/// <summary>
/// Single-tenant registry for the community migrator.
/// Returns the one community database as the only migration target.
/// The connection string is passed in by the foundation runner from the
/// configured control-plane connection — in community that IS the deployment DB.
/// </summary>
public sealed class CommunityTenantRegistry : ITenantRegistry
{
    public Task<IReadOnlyList<TenantDatabase>> ListActiveTenantsAsync(
        string controlPlaneConnectionString,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<TenantDatabase> targets =
        [
            new TenantDatabase("community", "community", controlPlaneConnectionString)
        ];
        return Task.FromResult(targets);
    }
}
