using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Orkyo.Community.Tests.Endpoints;

/// <summary>
/// Integration tests for foundation's GET /api/tenants/memberships as Community answers it:
/// foundation's ControlPlaneTenantMembershipQuery over the seeded tenants row and the test
/// user's membership (DatabaseFixture).
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
        // The seeded tenants row (migration 3020), which Community:TenantSlug must agree with.
        membership.GetProperty("tenantSlug").GetString().Should().Be("community");
        membership.GetProperty("tenantStatus").GetString().Should().Be("active");
        // The fixture seeds an active admin membership for the test user.
        membership.GetProperty("role").GetString().Should().Be("admin");
        membership.GetProperty("status").GetString().Should().Be("active");
        membership.GetProperty("joinedAt").ValueKind.Should().Be(JsonValueKind.String);
        // The seeded tenants row has no owner, so nobody is the owner.
        membership.GetProperty("isOwner").GetBoolean().Should().BeFalse();
    }
}
