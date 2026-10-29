using Api.Constants;
using Api.Models;
using Api.Security;
using Api.Services;
using Microsoft.Extensions.Options;
using Orkyo.Shared;

namespace Orkyo.Community.Tenant;

/// <summary>
/// Single-tenant <see cref="ITenantMembershipQuery"/> behind foundation's
/// <c>GET /api/tenants/memberships</c>. There is exactly one workspace and every authenticated
/// user is a member; it has no control-plane membership row, so the role comes from the
/// authorization context and <c>joinedAt</c> is null.
/// </summary>
public sealed class SingleTenantMembershipQuery(IAuthorizationContext authContext, IOptions<SingleTenantOptions> options)
    : ITenantMembershipQuery
{
    public Task<IReadOnlyList<TenantMembershipResponse>> GetCurrentUserMembershipsAsync(CancellationToken ct = default)
    {
        var tenant = options.Value;
        IReadOnlyList<TenantMembershipResponse> memberships =
        [
            new TenantMembershipResponse(
                tenant.TenantId,
                tenant.TenantSlug,
                tenant.TenantName,
                TenantStatusConstants.Active,
                authContext.Role.ToString().ToLowerInvariant(),
                MembershipStatusConstants.Active,
                IsOwner: authContext.IsAdmin,
                JoinedAt: null),
        ];
        return Task.FromResult(memberships);
    }
}
