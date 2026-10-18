using Api.Middleware;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Orkyo.Community.Tests.Architecture;

/// <summary>
/// The authorization conformance walk over this edition's real host. Community adds no
/// endpoints of its own today, so this pins that the composed graph (foundation endpoints
/// under SingleTenantMiddleware) keeps every mutating route governed, and it starts failing the
/// day a Community-only write is added without a convention.
/// </summary>
[Collection("Database collection")]
public class AuthorizationContractTests
{
    private readonly DatabaseFixture _fixture;

    public AuthorizationContractTests(DatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    public void EveryMutatingApiRoute_IsGovernedByAnAuthorizationConvention()
    {
        var dataSource = _fixture.Factory.Services.GetRequiredService<EndpointDataSource>();

        var ungoverned = AuthorizationContract.FindUngovernedMutatingRoutes<AuthorizationGoverned>(
            dataSource, AuthorizationContract.FoundationSelfServicePrefixes);

        Assert.True(ungoverned.Count == 0, AuthorizationContract.Explain(ungoverned));
    }

    [Fact]
    public void EveryAdminRoute_ReadsIncluded_IsGoverned()
    {
        var dataSource = _fixture.Factory.Services.GetRequiredService<EndpointDataSource>();

        var ungoverned = AuthorizationContract.FindUngovernedAdminRoutes<AuthorizationGoverned>(dataSource);

        Assert.True(ungoverned.Count == 0,
            "These /api/admin routes have no gate. Map them with MapSiteAdminGroup():\n  "
            + string.Join("\n  ", ungoverned));
    }

    [Fact]
    public void EveryApiRoute_DeclaresItsAuthentication()
    {
        // Community maps no anonymous routes of its own; foundation's list is the whole list.
        var dataSource = _fixture.Factory.Services.GetRequiredService<EndpointDataSource>();

        var undeclared = AuthorizationContract.FindUnauthenticatedRoutes(
            dataSource, AuthorizationContract.FoundationAnonymousRoutes);

        Assert.True(undeclared.Count == 0,
            "These /api routes answer anonymous callers. Give the group RequireAuthorization (or a "
            + "Require* convention), or allow-list a route that is anonymous on purpose:\n  "
            + string.Join("\n  ", undeclared));
    }
}
