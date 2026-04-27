using Api.Integrations.Keycloak;
using Api.Security;
using Api.Services;

namespace Orkyo.Community.Middleware;

/// <summary>
/// Just-in-time user provisioning for the community edition.
///
/// In the BFF pattern, <see cref="IIdentityLinkService.LinkIdentityAsync"/> is called
/// from the auth callback, which creates the user in <c>public.users</c> and
/// <c>public.user_identities</c>. However, if the BFF session is lost (API restart,
/// cache clear) a user with a valid Keycloak token can reach the API before the
/// callback runs again, leaving them absent from the database.
///
/// This middleware closes that gap by provisioning the user on their first API request
/// if they are authenticated but not yet in the database — the standard
/// JIT provisioning pattern used in enterprise identity management.
///
/// Must run AFTER <c>UseAuthentication</c> (so <c>context.User</c> is populated)
/// and BEFORE <c>ContextEnrichmentMiddleware</c> (so the user exists in the DB
/// when membership is resolved).
/// </summary>
public sealed class CommunityJitProvisioningMiddleware(
    IIdentityLinkService identityLinkService,
    ILogger<CommunityJitProvisioningMiddleware> logger) : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var tokenProfile = KeycloakTokenProfile.FromPrincipal(context.User);

            if (tokenProfile.IsValid && tokenProfile.Subject is not null)
            {
                var existing = await identityLinkService.FindByExternalIdentityAsync(
                    AuthProvider.Keycloak, tokenProfile.Subject);

                if (existing is null)
                {
                    var externalToken = tokenProfile.ToExternalIdentityToken();
                    if (externalToken is not null)
                    {
                        logger.LogInformation(
                            "JIT provisioning: creating user record for subject {Subject}",
                            tokenProfile.Subject);
                        await identityLinkService.LinkIdentityAsync(externalToken);
                    }
                }
            }
        }

        await next(context);
    }
}
