using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Orkyo.Community.Tests.Endpoints;

/// <summary>
/// Integration tests for <see cref="Orkyo.Community.Api.Endpoints.CommunityTenantEndpoints"/>.
/// </summary>
[Collection("Database collection")]
public class CommunityTenantEndpointsTests
{
    private static readonly string AdminBearerToken = Convert.ToBase64String(
        System.Text.Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new
            {
                UserId = "22222222-2222-2222-2222-222222222222",
                Email = "admin@orkyo.example",
                DisplayName = "Admin User",
                TenantId = "00000000-0000-0000-0000-000000000001",
                TenantSlug = "test",
                IsTenantAdmin = true,
                Role = "admin"
            })));

    private readonly DatabaseFixture _fixture;
    private readonly HttpClient _client;
    private readonly HttpClient _adminClient;
    private readonly HttpClient _unauthedClient;

    public CommunityTenantEndpointsTests(DatabaseFixture fixture)
    {
        _fixture = fixture;

        _client = fixture.Factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestConstants.TestBearerToken);

        _adminClient = fixture.Factory.CreateClient();
        _adminClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AdminBearerToken);

        _unauthedClient = fixture.Factory.CreateClient();
        // No Authorization header — unauthenticated requests only.
    }

    // ── Auth guard ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetMemberships_Unauthenticated_Returns401()
    {
        var response = await _unauthedClient.GetAsync("/api/tenants/memberships");

        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Redirect,
            $"Expected 401/302 but got {response.StatusCode}");
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetMemberships_AuthenticatedUser_Returns200()
    {
        var response = await _client.GetAsync("/api/tenants/memberships");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetMemberships_AuthenticatedUser_ReturnsArrayWithOneMembership()
    {
        var response = await _client.GetAsync("/api/tenants/memberships");
        var memberships = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(memberships);
        Assert.Single(memberships);
    }

    [Fact]
    public async Task GetMemberships_AuthenticatedUser_ReturnsCommunityTenantId()
    {
        var response = await _client.GetAsync("/api/tenants/memberships");
        var memberships = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        var tenantId = memberships![0].GetProperty("tenantId").GetGuid();
        Assert.Equal(new Guid("00000000-0000-0000-0000-000000000001"), tenantId);
    }

    [Fact]
    public async Task GetMemberships_AuthenticatedUser_ReturnsTenantSlug()
    {
        var response = await _client.GetAsync("/api/tenants/memberships");
        var memberships = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        var slug = memberships![0].GetProperty("tenantSlug").GetString();
        Assert.Equal("community", slug);
    }

    [Fact]
    public async Task GetMemberships_AuthenticatedUser_ResponseIncludesIsOwnerField()
    {
        // In community (single-tenant), isOwner reflects the user's admin status in the
        // security context. The exact value depends on IAuthorizationContext resolution;
        // we assert the field is present and is a boolean.
        var response = await _client.GetAsync("/api/tenants/memberships");
        var memberships = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        var isOwnerElement = memberships![0].GetProperty("isOwner");
        Assert.True(
            isOwnerElement.ValueKind is System.Text.Json.JsonValueKind.True
                                     or System.Text.Json.JsonValueKind.False,
            "isOwner must be a boolean");
    }

    [Fact]
    public async Task GetMemberships_AuthenticatedUser_ReturnsTenantStatusActive()
    {
        var response = await _client.GetAsync("/api/tenants/memberships");
        var memberships = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        var status = memberships![0].GetProperty("tenantStatus").GetString();
        Assert.Equal("active", status);
    }
}
