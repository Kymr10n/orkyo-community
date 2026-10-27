using System.Net;
using Api.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Orkyo.Community.Tests;

[Trait(TestCategories.Name, TestCategories.Database)]
[Collection("Database collection")]
public class SmokeTests
{
    private readonly DatabaseFixture _fixture;
    private readonly HttpClient _client;

    public SmokeTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Factory.CreateClient();
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/health/live")]
    public async Task HealthEndpoints_ReturnOk(string path)
    {
        var response = await _client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public void TestHost_ValidatesScopes()
    {
        // TenantContext is scoped (Program.cs). Resolving it from the root provider only throws
        // when the host validates scopes — what UseDefaultServiceProvider in the factory is for.
        var act = () => _fixture.Factory.Services.GetRequiredService<TenantContext>();

        act.Should().Throw<InvalidOperationException>().WithMessage("*Cannot resolve scoped service*root provider*");
    }
}
