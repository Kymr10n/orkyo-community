using Api.Models;
using Api.Services;
using Microsoft.Extensions.Options;

namespace Orkyo.Community.Tenant;

/// <summary>
/// Single-tenant implementation of <see cref="ITenantResolver"/>.
/// Always returns the one configured community tenant regardless of request.
/// The control-plane/tenant split collapses to one database in community.
/// </summary>
public sealed class SingleTenantResolver : ITenantResolver
{
    private readonly TenantContext _fixed;

    public SingleTenantResolver(IOptions<SingleTenantOptions> options, IConfiguration configuration)
    {
        var opts = options.Value;
        var connStr = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings__DefaultConnection is required");

        _fixed = new TenantContext
        {
            TenantId = opts.TenantId,
            TenantSlug = opts.TenantSlug,
            TenantDbConnectionString = connStr,
            Tier = ServiceTier.Enterprise,
            Status = "active",
        };
    }

    public Task<TenantContext?> ResolveTenantAsync(string? subdomain, string? tenantHeader)
        => Task.FromResult<TenantContext?>(_fixed);

    public void InvalidateCache(string slug) { }
}
