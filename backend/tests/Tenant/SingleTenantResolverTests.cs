using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Orkyo.Community.Tenant;

namespace Orkyo.Community.Tests.Tenant;

public class SingleTenantResolverTests
{
    private static readonly SingleTenantOptions DefaultOptions = new()
    {
        TenantId = new Guid("00000000-0000-0000-0000-000000000001"),
        TenantSlug = "community",
        TenantName = "Test Community",
    };

    private static IConfiguration ConfigWith(string? connectionString)
    {
        var values = new Dictionary<string, string?>();
        if (connectionString != null)
            values[$"ConnectionStrings:{Orkyo.Community.CommunityConfigKeys.DefaultConnection}"] = connectionString;
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    // ── Constructor ───────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_WhenConnectionStringPresent_DoesNotThrow()
    {
        var act = () => new SingleTenantResolver(
            Options.Create(DefaultOptions),
            ConfigWith("Host=localhost;Database=community"));

        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WhenConnectionStringMissing_ThrowsInvalidOperationException()
    {
        var act = () => new SingleTenantResolver(
            Options.Create(DefaultOptions),
            ConfigWith(connectionString: null));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ConnectionStrings__DefaultConnection*");
    }

    // ── ResolveTenantAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task ResolveTenantAsync_ReturnsConfiguredTenantId()
    {
        var sut = new SingleTenantResolver(
            Options.Create(DefaultOptions),
            ConfigWith("Host=localhost;Database=community"));

        var result = await sut.ResolveTenantAsync(subdomain: null, tenantHeader: null);

        result.Should().NotBeNull();
        result!.TenantId.Should().Be(DefaultOptions.TenantId);
    }

    [Fact]
    public async Task ResolveTenantAsync_ReturnsConfiguredSlug()
    {
        var sut = new SingleTenantResolver(
            Options.Create(DefaultOptions),
            ConfigWith("Host=localhost;Database=community"));

        var result = await sut.ResolveTenantAsync(subdomain: null, tenantHeader: null);

        result!.TenantSlug.Should().Be(DefaultOptions.TenantSlug);
    }

    [Fact]
    public async Task ResolveTenantAsync_IgnoresSubdomainArgument()
    {
        var sut = new SingleTenantResolver(
            Options.Create(DefaultOptions),
            ConfigWith("Host=localhost;Database=community"));

        var r1 = await sut.ResolveTenantAsync(subdomain: "acme", tenantHeader: null);
        var r2 = await sut.ResolveTenantAsync(subdomain: null, tenantHeader: "other");

        r1!.TenantId.Should().Be(r2!.TenantId);
    }

    [Fact]
    public async Task ResolveTenantAsync_ReturnsSameInstanceOnEveryCall()
    {
        var sut = new SingleTenantResolver(
            Options.Create(DefaultOptions),
            ConfigWith("Host=localhost;Database=community"));

        var r1 = await sut.ResolveTenantAsync(null, null);
        var r2 = await sut.ResolveTenantAsync(null, null);

        r1.Should().BeSameAs(r2);
    }

    // ── InvalidateCache ───────────────────────────────────────────────────────

    [Fact]
    public void InvalidateCache_DoesNotThrow()
    {
        var sut = new SingleTenantResolver(
            Options.Create(DefaultOptions),
            ConfigWith("Host=localhost;Database=community"));

        var act = () => sut.InvalidateCache("community");
        act.Should().NotThrow();
    }
}
