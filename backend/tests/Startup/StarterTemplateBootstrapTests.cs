using Api.Models.Preset;
using Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orkyo.Community.Startup;
using Orkyo.Community.Tenant;

namespace Orkyo.Community.Tests.Startup;

/// <summary>
/// The trigger side of the starter setup: what ORKYO_STARTER_TEMPLATE does at start, without a
/// database. The preset and its once-only rule are foundation's and are tested there.
/// </summary>
public class StarterTemplateBootstrapTests
{
    private sealed class RecordingStarterTemplates : IStarterTemplateService
    {
        public List<(Guid TenantId, string Db, Guid UserId, string Key)> Calls { get; } = [];
        public Exception? Throws { get; init; }

        public Task ApplyStarterTemplateAsync(Guid tenantId, string dbIdentifier, Guid userId, string templateKey, CancellationToken ct = default)
        {
            Calls.Add((tenantId, dbIdentifier, userId, templateKey));
            return Throws is null ? Task.CompletedTask : Task.FromException(Throws);
        }

        public IReadOnlyList<StarterTemplateInfo> GetAvailableTemplates() => StarterTemplateCatalog.All;
    }

    private static readonly SingleTenantOptions Tenant = new();

    private static StarterTemplateBootstrap Create(string? value, RecordingStarterTemplates service)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [CommunityConfigKeys.StarterTemplate] = value })
            .Build();
        // Registered scoped, as foundation registers it, behind a validating provider: a
        // bootstrap that took the service directly would fail here, as it did at dev start-up.
        var provider = new ServiceCollection()
            .AddScoped<IStarterTemplateService>(_ => service)
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        return new StarterTemplateBootstrap(
            configuration, Options.Create(Tenant), provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<StarterTemplateBootstrap>.Instance);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("empty")]
    public async Task UnsetOrEmpty_AppliesNothing(string? value)
    {
        // Empty counts as unset: the deploy pipeline writes KEY= for every unset key.
        var service = new RecordingStarterTemplates();

        await Create(value, service).StartAsync(CancellationToken.None);

        service.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task AKnownKey_AppliesItToTheSingleTenant_Once()
    {
        var service = new RecordingStarterTemplates();

        await Create(StarterTemplateCatalog.Manufacturing, service).StartAsync(CancellationToken.None);

        service.Calls.Should().ContainSingle()
            .Which.Should().Be((Tenant.TenantId, Tenant.TenantSlug, Guid.Empty, StarterTemplateCatalog.Manufacturing));
    }

    [Fact]
    public async Task AnUnknownKey_RefusesToStart()
    {
        // A typo silently ignored is an operator who expected data and got an empty install.
        var service = new RecordingStarterTemplates();

        var act = () => Create("manufacutring", service).StartAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ORKYO_STARTER_TEMPLATE must be one of: manufacturing, office*manufacutring*");
        service.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task AFailedApply_IsLogged_AndDoesNotBlockStartup()
    {
        // Nothing is recorded on failure, so the next start retries; the API must still come up.
        var service = new RecordingStarterTemplates { Throws = new InvalidOperationException("db down") };

        var act = () => Create(StarterTemplateCatalog.Office, service).StartAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
        service.Calls.Should().ContainSingle();
    }

    [Fact]
    public async Task StopAsync_IsANoOp()
    {
        await Create(null, new RecordingStarterTemplates()).StopAsync(CancellationToken.None);
    }
}
