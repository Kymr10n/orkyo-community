using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Orkyo.Community.Tests.Endpoints;

/// <summary>
/// Wiring tests for GET /metrics (foundation's <c>MapOrkyoMetricsEndpoint</c> helper,
/// adopted in Program.cs — the Prometheus parity gap with SaaS is closed).
///
/// Access model:
/// - No METRICS_TOKEN configured → the endpoint is not mapped at all (fail-secure, 404).
/// - Token configured → Authorization: Basic base64(prometheus:{token}), fixed-time compare.
/// The full gate matrix is covered by foundation's OrkyoMetricsEndpointTests; this file
/// only proves Community's Program.cs plumbs the config key into the helper.
/// </summary>
[Collection("Database collection")]
public class MetricsEndpointTests
{
    private readonly DatabaseFixture _fixture;

    public MetricsEndpointTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetMetrics_NoTokenConfigured_Returns404()
    {
        var response = await _fixture.Factory.CreateClient().GetAsync("/metrics");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetMetrics_TokenConfigured_CorrectCredentials_Returns200()
    {
        const string token = "supersecrettoken";
        using var factory = _fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["METRICS_TOKEN"] = token
                })));
        var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/metrics");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"prometheus:{token}")));

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetMetrics_TokenConfigured_MissingHeader_Returns401()
    {
        const string token = "supersecrettoken";
        using var factory = _fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["METRICS_TOKEN"] = token
                })));

        var response = await factory.CreateClient().GetAsync("/metrics");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
