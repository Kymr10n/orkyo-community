using Api.Configuration;
using Api.Models.Preset;
using Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orkyo.Community.Tenant;

namespace Orkyo.Community.Startup;

/// <summary>
/// Applies the starter setup named by <c>ORKYO_STARTER_TEMPLATE</c> to the single Community
/// tenant when the API starts. The setup itself — the preset, its once-only rule, the applier —
/// is foundation's; this is only the trigger, the Community equivalent of the starter-template
/// picker a SaaS workspace gets at creation.
///
/// Hosted services start before Kestrel accepts requests, so <c>/health</c> answers only after
/// the apply. The schema is there because compose gates the API on the migrator having
/// completed. Foundation records the preset in <c>preset_applications</c> and skips it on every
/// later start, so this can run unconditionally.
///
/// Foundation registers <see cref="IStarterTemplateService"/> as scoped, so it is resolved from a
/// scope created here: a hosted service is a singleton, and taking the service in the constructor
/// would hold one instance for the life of the process.
/// </summary>
public sealed class StarterTemplateBootstrap(
    IConfiguration configuration,
    IOptions<SingleTenantOptions> options,
    IServiceScopeFactory scopeFactory,
    ILogger<StarterTemplateBootstrap> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var key = configuration.GetOptionalString(CommunityConfigKeys.StarterTemplate);
        if (string.IsNullOrEmpty(key) || key == StarterTemplateCatalog.Empty)
            return;

        // A typo here is an operator who expected data and would get an empty install with
        // nothing in any log to say why. Fail fast like every other Community config error.
        if (!StarterTemplateCatalog.IsPresetTemplate(key))
            throw new InvalidOperationException(
                $"{CommunityConfigKeys.StarterTemplate} must be one of: "
                + $"{StarterTemplateCatalog.Manufacturing}, {StarterTemplateCatalog.Office} (or unset); got '{key}'.");

        logger.LogInformation("{Key}={Template}: applying starter setup", CommunityConfigKeys.StarterTemplate, key);
        try
        {
            // The user id is unread by foundation; the database identifier is ignored by the
            // single-tenant connection factory and appears only in foundation's log line.
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IStarterTemplateService>().ApplyStarterTemplateAsync(
                options.Value.TenantId, options.Value.TenantSlug, Guid.Empty, key, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A database hiccup must not keep the API down. Nothing was recorded, so the next
            // start retries; the operator sees the reason here and a usable, empty install.
            logger.LogError(ex,
                "Starter template {Template} could not be applied; the installation starts empty. It is retried at the next start.",
                key);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
