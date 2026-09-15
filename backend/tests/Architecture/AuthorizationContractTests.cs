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
}
