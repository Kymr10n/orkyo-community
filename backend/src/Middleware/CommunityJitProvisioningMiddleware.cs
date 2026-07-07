using Api.Integrations.Keycloak;
using Api.Security;
using Api.Services;
using Microsoft.Extensions.Caching.Memory;
using Orkyo.Shared;

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
    // Cache: externalSubject → known-provisioned marker (5-min absolute expiry, 5000 entry
    // limit — mirrors ContextEnrichmentMiddleware's principal cache). Only POSITIVE
    // existence is cached: once a user exists, JIT provisioning never applies to them
    // again, so a cached hit safely skips the per-request DB lookup. Negatives are never
    // cached so a user missing from the DB is always provisioned on their next request.
    private static readonly MemoryCache _provisionedSubjects = new(new MemoryCacheOptions
    {
        SizeLimit = 5_000
    });

    private static readonly TimeSpan CacheTtl = TimePolicyConstants.CacheTtl;

    /// <summary>Clear the cache (for integration tests).</summary>
    public static void ClearCache() => _provisionedSubjects.Compact(1.0);

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var tokenProfile = KeycloakTokenProfile.FromPrincipal(context.User);

            if (tokenProfile.IsValid && tokenProfile.Subject is not null
                && !_provisionedSubjects.TryGetValue(tokenProfile.Subject, out _))
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
                        var result = await identityLinkService.LinkIdentityAsync(externalToken);
                        if (result.Success)
                        {
                            CacheProvisioned(tokenProfile.Subject);
                        }
                    }
                }
                else
                {
                    CacheProvisioned(tokenProfile.Subject);
                }
            }
        }

        await next(context);
    }

    private static void CacheProvisioned(string subject)
        => _provisionedSubjects.Set(subject, true, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheTtl,
            Size = 1
        });
}
