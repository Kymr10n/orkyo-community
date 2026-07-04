using System.Net;
using System.Text;
using System.Text.Json;

namespace Orkyo.Community.Tests;

/// <summary>
/// Guards the edition wiring that broke once already: Community must register every rate-limit policy its
/// mapped foundation endpoints require (via <c>AddFoundationRateLimiting</c>). If a policy is missing, the
/// rate-limiter middleware throws and the endpoint returns 500 — but only once the request actually reaches
/// the limiter. Community runs <c>UseRateLimiter</c> after authorization, so an admin endpoint is reached
/// only by a caller that passes <c>RequireSiteAdmin</c>; an unauthenticated probe short-circuits at 401 and
/// would mask a missing policy. These probes therefore use a site-admin principal (admin-operations) and an
/// anonymous GET (bff-auth) that both land on the limiter via GET, so no CSRF token is required.
///
/// Registration of the full foundation policy set is covered by the foundation
/// RateLimitingServiceExtensionsTests; this test proves Community's call is wired and live.
/// </summary>
[Collection("Database collection")]
public class RateLimitWiringTests
{
    // Site-admin comes from the token's realm roles; a Keycloak "sub" with no linked DB user still yields a
    // partial principal with IsSiteAdmin=true (ContextEnrichmentMiddleware), which is enough to pass authz.
    private static readonly string SiteAdminToken = Convert.ToBase64String(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            UserId = "22222222-2222-2222-2222-222222222222",
            Email = "siteadmin@orkyo.example",
            DisplayName = "Site Admin",
            TenantId = "00000000-0000-0000-0000-000000000000",
            TenantSlug = "",
            IsTenantAdmin = false,
            Role = "viewer",
            Sub = "kc-siteadmin-ratelimit",
            RealmRoles = new[] { "user", "site-admin" },
        })));

    private readonly HttpClient _siteAdmin;
    private readonly HttpClient _anon;

    public RateLimitWiringTests(DatabaseFixture fixture)
    {
        _siteAdmin = fixture.Factory.CreateClient();
        _siteAdmin.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", SiteAdminToken);

        _anon = fixture.Factory.CreateClient();
    }

    // admin-operations — the exact policy whose absence 500'd /api/admin/settings. A site-admin GET passes
    // authorization and lands on the rate limiter, so a missing policy surfaces as 500 here.
    [Theory]
    [InlineData("/api/admin/audit")]
    [InlineData("/api/admin/configuration")]
    [InlineData("/api/admin/settings")]
    [InlineData("/api/admin/announcements")]
    [InlineData("/api/admin/feedback")]
    public async Task AdminEndpoint_SiteAdmin_ReachesRateLimiterWithoutError(string path)
    {
        var response = await _siteAdmin.GetAsync(path);

        Assert.False(response.StatusCode == HttpStatusCode.InternalServerError,
            $"{path} returned 500 — the 'admin-operations' rate-limit policy is likely not registered " +
            "(AddFoundationRateLimiting missing/renamed).");
        Assert.False(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            $"{path} returned {response.StatusCode} — the site-admin probe did not pass authorization, so it " +
            "never reached the rate limiter and cannot guard the policy. Fix the probe's principal.");
    }

    // bff-auth — anonymous GET, reaches the limiter without auth or CSRF.
    [Fact]
    public async Task BffLogin_Anonymous_ReachesRateLimiterWithoutError()
    {
        var response = await _anon.GetAsync("/api/auth/bff/login?returnTo=/");

        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
    }
}
