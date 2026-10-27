namespace Orkyo.Community.Tests.Endpoints;

/// <summary>
/// Wiring test for GET /metrics (foundation's <c>MapOrkyoMetricsEndpoint</c> helper,
/// called from Program.cs).
///
/// Access model:
/// - No METRICS_TOKEN configured → the endpoint is not mapped at all (fail-secure, 404).
/// - Token configured → Authorization: Basic base64(prometheus:{token}), fixed-time compare.
/// The full gate matrix is covered by foundation's OrkyoMetricsEndpointTests; this file
/// only proves Community's Program.cs plumbs the config key into the helper.
/// </summary>
[Trait(TestCategories.Name, TestCategories.Database)]
[Collection("Database collection")]
public class MetricsEndpointTests
{
    private readonly DatabaseFixture _fixture;

    public MetricsEndpointTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task MetricsEndpoint_IsGatedByMetricsToken()
    {
        var failures = await MetricsEndpointGateSmoke.RunAsync(_fixture.Factory);

        failures.Should().BeEmpty();
    }
}
