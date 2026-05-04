using System.Net;

namespace Orkyo.Community.Tests;

[Collection("Database collection")]
public class SmokeTests
{
    private readonly HttpClient _client;

    public SmokeTests(DatabaseFixture fixture)
    {
        _client = fixture.Factory.CreateClient();
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HealthLiveness_ReturnsOk()
    {
        var response = await _client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AuthorizedEndpoint_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/sites");
        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Redirect,
            $"Expected 401/302 but got {response.StatusCode}");
    }
}
