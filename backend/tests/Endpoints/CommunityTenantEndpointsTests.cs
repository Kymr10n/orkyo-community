using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Orkyo.Community.Tests.Endpoints;

/// <summary>
/// Integration tests for foundation's GET /api/tenants/memberships as Community answers it
/// (<see cref="Orkyo.Community.Tenant.SingleTenantMembershipQuery"/>).
/// The unauthenticated 401 is covered by <c>AuthorizationContractTests</c>, which proves every
/// API route declares its authentication.
/// </summary>
[Trait(TestCategories.Name, TestCategories.Database)]
[Collection("Database collection")]
public class CommunityTenantEndpointsTests
{
    private readonly HttpClient _client;

    public CommunityTenantEndpointsTests(DatabaseFixture fixture)
    {
        _client = fixture.Factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestConstants.TestBearerToken);
    }

    [Fact]
    public async Task GetMemberships_AuthenticatedUser_ReturnsTheSingleCommunityMembership()
    {
        var response = await _client.GetAsync("/api/tenants/memberships");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var memberships = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        var membership = memberships.Should().ContainSingle().Subject;
        membership.GetProperty("tenantId").GetGuid().Should().Be(TestConstants.TenantId);
        // Community:TenantSlug from appsettings.Test.json — pins that the file still reaches the host.
        membership.GetProperty("tenantSlug").GetString().Should().Be("community");
        membership.GetProperty("tenantStatus").GetString().Should().Be("active");
        // In community (single-tenant), isOwner reflects the user's admin status in the
        // security context. The exact value depends on IAuthorizationContext resolution;
        // pin the shape, not the resolution.
        membership.GetProperty("isOwner").ValueKind.Should().BeOneOf(JsonValueKind.True, JsonValueKind.False);
    }
}
