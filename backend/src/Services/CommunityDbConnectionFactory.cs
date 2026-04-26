using Api.Models;
using Api.Services;
using Npgsql;

namespace Orkyo.Community.Services;

/// <summary>
/// Single-tenant <see cref="IDbConnectionFactory"/>.
/// All control-plane and tenant operations map to the one community database —
/// the SaaS control-plane/tenant split collapses here by design.
/// </summary>
public sealed class CommunityDbConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public CommunityDbConnectionFactory(IConfiguration configuration)
    {
        _connectionString = ConnectionStringTimeoutPolicy.ApplyDefaultCommandTimeout(
            configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("ConnectionStrings__DefaultConnection is required"));
    }

    public NpgsqlConnection CreateControlPlaneConnection() => new(_connectionString);

    public NpgsqlConnection CreateTenantConnection(TenantContext tenant) => new(_connectionString);

    public NpgsqlConnection CreateOrgConnection(OrgContext org) => new(_connectionString);

    public NpgsqlConnection CreateConnectionForDatabase(string dbIdentifier) => new(_connectionString);
}
