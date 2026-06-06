using Api.Services;
using Microsoft.Extensions.Options;
using Orkyo.Shared;

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
        var connStr = configuration.GetConnectionString(CommunityConfigKeys.DefaultConnection)
            ?? throw new InvalidOperationException("ConnectionStrings__DefaultConnection is required");

        _fixed = new TenantContext
        {
            TenantId = opts.TenantId,
            TenantSlug = opts.TenantSlug,
            TenantDbConnectionString = connStr,
            Status = TenantStatusConstants.Active,
        };
    }

    public Task<TenantContext?> ResolveTenantAsync(string? subdomain, string? tenantHeader, CancellationToken ct = default)
        => Task.FromResult<TenantContext?>(_fixed);

    public void InvalidateCache(string slug) { }
}
