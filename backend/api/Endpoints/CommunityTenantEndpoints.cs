using Api.Security;
using Microsoft.Extensions.Options;
using Orkyo.Community.Tenant;
using Orkyo.Shared;

namespace Orkyo.Community.Api.Endpoints;

public static class CommunityTenantEndpoints
{
    public static void MapCommunityTenantEndpoints(this WebApplication app)
    {
        var tenants = app.MapGroup("/api/tenants")
            .RequireAuthorization();

        // GET /api/tenants/memberships
        // In community there is exactly one tenant and every authenticated user is a member.
        // This mirrors the SaaS multi-tenant endpoint shape so shared frontend code works unchanged.
        tenants.MapGet("/memberships", (
            ICurrentPrincipal principal,
            IAuthorizationContext authContext,
            IOptions<SingleTenantOptions> opts) =>
        {
            var tenant = opts.Value;

            return Results.Ok(new[]
            {
                new
                {
                    tenantId       = tenant.TenantId,
                    tenantSlug     = tenant.TenantSlug,
                    tenantDisplayName = tenant.TenantName,
                    tenantStatus   = TenantStatusConstants.Active,
                    role           = authContext.Role.ToString().ToLowerInvariant(),
                    status         = TenantStatusConstants.Active,
                    isOwner        = authContext.IsAdmin,
                    joinedAt       = (DateTime?)null
                }
            });
        })
        .WithName("GetCommunityMemberships")
        .WithSummary("Returns the current user's membership in the single community tenant")
        .WithTags("Tenants");
    }
}
