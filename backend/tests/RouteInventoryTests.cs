using System.Net;

namespace Orkyo.Community.Tests;

/// <summary>
/// Asserts that the resource-model and people-resource endpoint groups are
/// actually registered via <c>app.Map*Endpoints()</c> in Program.cs.
///
/// Failure mode this catches: someone refactors Foundation, deletes or renames
/// an endpoint Map, and the corresponding line in Community's Program.cs is
/// silently missed — the API still boots (no DI dep), but every route under
/// the removed map silently 404s.
///
/// Assertion strategy: an unauthenticated GET to a registered endpoint returns
/// 401 (auth middleware intercepts). An unregistered endpoint returns 404 from
/// the routing fallback. So "anything-but-404" proves the route is mapped.
///
/// One [InlineData] per top-level Map* call in Foundation's endpoint set.
/// Probe paths are chosen to be unique to that Map* call so a single missing
/// Map produces exactly one failing case. Source of truth: the list mirrors
/// FoundationWebApplicationFactory.cs lines ~417-452.
/// </summary>
[Collection("Database collection")]
public class RouteInventoryTests
{
    private readonly HttpClient _client;

    public RouteInventoryTests(DatabaseFixture fixture)
    {
        _client = fixture.Factory.CreateClient();
    }

    [Theory]
    // Resource model
    [InlineData("/api/resource-types", "MapResourceTypeEndpoints")]
    [InlineData("/api/resources", "MapResourceEndpoints")]
    [InlineData("/api/resource-assignments", "MapResourceAssignmentEndpoints")]
    [InlineData("/api/utilization", "MapUtilizationEndpoints")]
    // Resource-model sub-paths under shared prefixes: each probes one unique Map*
    [InlineData("/api/criteria/00000000-0000-0000-0000-000000000000/applicability", "MapCriterionApplicabilityEndpoints")]
    [InlineData("/api/resource-groups/00000000-0000-0000-0000-000000000000/members", "MapResourceGroupMemberEndpoints")]
    // People
    [InlineData("/api/person-profiles/00000000-0000-0000-0000-000000000000", "MapPersonProfileEndpoints")]
    // Departments + Job Titles (reference data)
    [InlineData("/api/job-titles", "MapJobTitleEndpoints")]
    [InlineData("/api/departments", "MapDepartmentEndpoints")]
    [InlineData("/api/departments/tree", "MapDepartmentEndpoints")]
    // Sanity: routes that were already wired
    [InlineData("/api/sites", "MapSiteEndpoints")]
    [InlineData("/api/criteria", "MapCriteriaEndpoints")]
    [InlineData("/api/requests", "MapRequestEndpoints")]
    [InlineData("/api/resource-groups", "MapResourceGroupEndpoints")]
    public async Task ExpectedRoute_IsRegistered(string path, string expectedMapCall)
    {
        var response = await _client.GetAsync(path);

        Assert.False(
            response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.InternalServerError,
            $"Route '{path}' returned {(int)response.StatusCode}. 404 = likely missing '{expectedMapCall}()' " +
            $"call in Program.cs; 500 = the route is mapped but a service or rate-limit policy behind it is not " +
            $"registered. Cross-reference with FoundationWebApplicationFactory.cs (lines ~417-452).");
    }
}
