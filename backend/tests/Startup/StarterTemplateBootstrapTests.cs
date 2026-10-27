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

    // Exactly the keys StarterTemplateCatalog.IsPresetTemplate accepts, in catalog order.
    private const string AcceptedKeys = "camping-site, construction-site, manufacturing, office";

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
    [InlineData(StarterTemplateCatalog.Empty)]
    public async Task UnsetOrEmpty_AppliesNothing(string? value)
    {
        // Empty counts as unset: the deploy pipeline writes KEY= for every unset key.
        var service = new RecordingStarterTemplates();

        await Create(value, service).StartAsync(CancellationToken.None);

        service.Calls.Should().BeEmpty();
    }

    [Theory]
    [InlineData(StarterTemplateCatalog.Manufacturing)]
    [InlineData(StarterTemplateCatalog.Office)]
    public async Task AShippedPresetKey_AppliesItToTheSingleTenant_Once(string key)
    {
        var service = new RecordingStarterTemplates();

        await Create(key, service).StartAsync(CancellationToken.None);

        service.Calls.Should().ContainSingle()
            .Which.Should().Be((Tenant.TenantId, Tenant.TenantSlug, Guid.Empty, key));
    }

    [Theory]
    [InlineData("manufacutring")]            // a typo
    [InlineData("Office")]                   // keys are case-sensitive
    [InlineData(" office")]                  // and not trimmed
    [InlineData("   ")]                      // whitespace is not "unset"
    [InlineData(StarterTemplateCatalog.Demo)] // foundation's demo template is not a preset
    public async Task AKeyTheGateRejects_RefusesToStart(string value)
    {
        // A typo silently ignored is an operator who expected data and got an empty install.
        var service = new RecordingStarterTemplates();

        var act = () => Create(value, service).StartAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*ORKYO_STARTER_TEMPLATE must be one of: {AcceptedKeys} (or unset); got '{value}'*");
        service.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task AFailedApply_DoesNotBlockStartup()
    {
        // Nothing is recorded on failure, so the next start retries; the API must still come up.
        var service = new RecordingStarterTemplates { Throws = new InvalidOperationException("db down") };

        var act = () => Create(StarterTemplateCatalog.Office, service).StartAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
        service.Calls.Should().ContainSingle();
    }

    [Fact]
    public async Task ACancelledApply_Propagates()
    {
        // Host shutdown during the apply is not a failure to swallow.
        var service = new RecordingStarterTemplates { Throws = new OperationCanceledException() };

        var act = () => Create(StarterTemplateCatalog.Office, service).StartAsync(CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
