using Api.Services;

namespace Orkyo.Community.Middleware;

/// <summary>
/// Community tenant middleware. Resolves the single fixed tenant from
/// <see cref="SingleTenantResolver"/> and stores it in <c>HttpContext.Items</c>
/// so that <c>ContextEnrichmentMiddleware</c> can pick it up.
/// The community edition always has exactly one tenant, so the tenant context is
/// set on every request regardless of <see cref="SkipTenantResolutionAttribute"/>.
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
        var tenant = await _resolver.ResolveTenantAsync(null, null);
        if (tenant is null)
        {
            _logger.LogError("SingleTenantResolver returned null — community tenant not configured");
            context.Response.StatusCode = 503;
            await context.Response.WriteAsJsonAsync(new { error = "Community tenant not configured" });
            return;
        }

        context.Items["TenantContext"] = tenant;
        await _next(context);
    }
}
