using Microsoft.Extensions.Configuration;
using Orkyo.Community.Tenant;
using Orkyo.Shared;

namespace Orkyo.Community.Tests.Tenant;

public class SingleTenantResolverTests
{
    // Deliberately not SingleTenantOptions' defaults: a resolver that ignored IOptions would
    // otherwise pass by accident.
    private static readonly SingleTenantOptions Options = new()
    {
        TenantId = new Guid("7b5a2a4e-2d7e-4e0c-9c3c-1f6b4a0c9d11"),
        TenantSlug = "acme-works",
        TenantName = "Acme Works",
    };

    private const string ConnectionString = "Host=db;Database=acme";

    private static SingleTenantResolver Create(string? connectionString = ConnectionString)
    {
        var values = new Dictionary<string, string?>();
        if (connectionString is not null)
            values[$"ConnectionStrings:{CommunityConfigKeys.DefaultConnection}"] = connectionString;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        return new SingleTenantResolver(Microsoft.Extensions.Options.Options.Create(Options), configuration);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")] // the deploy pipeline writes KEY= for every unset key
    public void Constructor_WhenConnectionStringMissingOrEmpty_Throws(string? connectionString)
    {
        var act = () => Create(connectionString);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{CommunityConfigKeys.DefaultConnectionEnvVar}*");
    }

    [Fact]
    public async Task ResolveTenantAsync_ReturnsTheConfiguredTenant_ForAnyArguments_AsOneInstance()
    {
        var sut = Create();

        var first = await sut.ResolveTenantAsync(subdomain: "acme", tenantHeader: "other");
        var second = await sut.ResolveTenantAsync(subdomain: null, tenantHeader: null);

        first.Should().NotBeNull();
        first!.TenantId.Should().Be(Options.TenantId);
        first.TenantSlug.Should().Be(Options.TenantSlug);
        first.TenantDbConnectionString.Should().Be(ConnectionString);
        first.Status.Should().Be(TenantStatusConstants.Active);
        second.Should().BeSameAs(first);
    }
}
