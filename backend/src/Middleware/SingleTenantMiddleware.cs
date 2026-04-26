using Api.Endpoints;
using Api.Middleware;
using Api.Services;

namespace Orkyo.Community.Middleware;

/// <summary>
/// Community tenant middleware. Resolves the single fixed tenant from
/// <see cref="SingleTenantResolver"/> and stores it in <c>HttpContext.Items</c>
/// so that <c>ContextEnrichmentMiddleware</c> can pick it up.
/// Endpoints marked with <see cref="SkipTenantResolutionAttribute"/> run without a tenant context.
/// </summary>
public sealed class SingleTenantMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ITenantResolver _resolver;
    private readonly ILogger<SingleTenantMiddleware> _logger;

    public SingleTenantMiddleware(RequestDelegate next, ITenantResolver resolver, ILogger<SingleTenantMiddleware> logger)
    {
        _next = next;
        _resolver = resolver;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        var skipTenant = endpoint?.Metadata.GetMetadata<ISkipTenantResolution>() is not null;

        if (!skipTenant)
        {
            var tenant = await _resolver.ResolveTenantAsync(null, null);
            if (tenant is null)
            {
                _logger.LogError("SingleTenantResolver returned null — community tenant not configured");
                context.Response.StatusCode = 503;
                await context.Response.WriteAsJsonAsync(new { error = "Community tenant not configured" });
                return;
            }

            context.Items["TenantContext"] = tenant;
        }

        await _next(context);
    }
}
