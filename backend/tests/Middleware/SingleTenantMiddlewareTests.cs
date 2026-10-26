using System.Text.Json;
using Api.Constants;
using Api.Helpers;
using Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Orkyo.Community.Middleware;

namespace Orkyo.Community.Tests.Middleware;

public class SingleTenantMiddlewareTests
{
    private static readonly TenantContext SomeTenant = new()
    {
        TenantId = Guid.NewGuid(),
        TenantSlug = "community",
        TenantDbConnectionString = "Host=localhost;Database=community",
        Status = "active",
    };

    /// <summary>Runs the middleware with a resolver that yields <paramref name="resolved"/>.</summary>
    private static async Task<(DefaultHttpContext Context, bool NextCalled)> InvokeAsync(TenantContext? resolved)
    {
        var nextCalled = false;
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new SingleTenantMiddleware(
            next: _ => { nextCalled = true; return Task.CompletedTask; },
            resolver: new StubTenantResolver(resolved),
            logger: NullLogger<SingleTenantMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        return (context, nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_WhenTenantResolved_StoresBothContextsAndCallsNext()
    {
        var (context, nextCalled) = await InvokeAsync(SomeTenant);

        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        context.Items[HttpContextItemKeys.TenantContext].Should().BeSameAs(SomeTenant);
        // Foundation endpoints read this via GetOrgContext(HttpContext) for tenant-audit writes
        // (floorplan download, site/settings mutations). Missing it → 500 "Org context not available".
        var org = context.Items[HttpContextItemKeys.OrgContext].Should().BeOfType<OrgContext>().Subject;
        org.OrgId.Should().Be(SomeTenant.TenantId);
        org.OrgSlug.Should().Be(SomeTenant.TenantSlug);
        org.DbConnectionString.Should().Be(SomeTenant.TenantDbConnectionString);
    }

    [Fact]
    public async Task InvokeAsync_WhenResolverReturnsNull_Writes503ProblemAndStops()
    {
        var (context, nextCalled) = await InvokeAsync(null);

        nextCalled.Should().BeFalse();
        context.Items.Should().NotContainKey(HttpContextItemKeys.TenantContext);
        context.Response.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        context.Response.ContentType.Should().Be("application/problem+json");

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var problem = await JsonSerializer.DeserializeAsync<OrkyoProblemDetails>(context.Response.Body, JsonSerializerOptions.Web);
        problem!.Code.Should().Be("community_tenant_unconfigured");
        problem.Status.Should().Be(StatusCodes.Status503ServiceUnavailable);
        problem.Detail.Should().Contain("not configured");
    }
}

file sealed class StubTenantResolver(TenantContext? tenant) : ITenantResolver
{
    public Task<TenantContext?> ResolveTenantAsync(string? subdomain, string? tenantHeader, CancellationToken ct = default)
        => Task.FromResult(tenant);

    public void InvalidateCache(string slug) { }
}
