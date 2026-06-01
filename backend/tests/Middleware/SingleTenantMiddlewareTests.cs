using Api.Models;
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
        Tier = ServiceTier.Free,
        Status = "active",
    };

    // ── Helper ────────────────────────────────────────────────────────────────

    private static (SingleTenantMiddleware middleware, DefaultHttpContext context, bool nextCalled) Build(
        TenantContext? tenantToReturn)
    {
        var nextCalled = false;
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var middleware = new SingleTenantMiddleware(
            next: _ => { nextCalled = true; return Task.CompletedTask; },
            resolver: new StubTenantResolver(tenantToReturn),
            logger: NullLogger<SingleTenantMiddleware>.Instance);

        return (middleware, context, nextCalled);
    }

    // ── Tenant resolved successfully ──────────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_WhenTenantResolved_StoresTenantInItems()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new SingleTenantMiddleware(
            next: _ => Task.CompletedTask,
            resolver: new StubTenantResolver(SomeTenant),
            logger: NullLogger<SingleTenantMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Items["TenantContext"].Should().BeSameAs(SomeTenant);
    }

    [Fact]
    public async Task InvokeAsync_WhenTenantResolved_CallsNext()
    {
        var nextCalled = false;
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new SingleTenantMiddleware(
            next: _ => { nextCalled = true; return Task.CompletedTask; },
            resolver: new StubTenantResolver(SomeTenant),
            logger: NullLogger<SingleTenantMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
    }

    // ── Tenant resolver returns null ──────────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_WhenResolverReturnsNull_Returns503()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new SingleTenantMiddleware(
            next: _ => Task.CompletedTask,
            resolver: new StubTenantResolver(null),
            logger: NullLogger<SingleTenantMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(503);
    }

    [Fact]
    public async Task InvokeAsync_WhenResolverReturnsNull_DoesNotCallNext()
    {
        var nextCalled = false;
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new SingleTenantMiddleware(
            next: _ => { nextCalled = true; return Task.CompletedTask; },
            resolver: new StubTenantResolver(null),
            logger: NullLogger<SingleTenantMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_WhenResolverReturnsNull_WritesErrorBody()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new SingleTenantMiddleware(
            next: _ => Task.CompletedTask,
            resolver: new StubTenantResolver(null),
            logger: NullLogger<SingleTenantMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();
        body.Should().Contain("not configured");
    }
}

file sealed class StubTenantResolver(TenantContext? tenant) : ITenantResolver
{
    public Task<TenantContext?> ResolveTenantAsync(string? subdomain, string? tenantHeader, CancellationToken ct = default)
        => Task.FromResult(tenant);

    public void InvalidateCache(string slug) { }
}
