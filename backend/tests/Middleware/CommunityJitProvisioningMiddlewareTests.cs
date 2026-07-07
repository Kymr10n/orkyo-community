using System.Security.Claims;
using Api.Integrations.Keycloak;
using Api.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Orkyo.Community.Middleware;

namespace Orkyo.Community.Tests.Middleware;

public class CommunityJitProvisioningMiddlewareTests
{
    public CommunityJitProvisioningMiddlewareTests()
    {
        // The known-provisioned cache is static; clear it so tests don't leak
        // cached subjects into each other.
        CommunityJitProvisioningMiddleware.ClearCache();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ClaimsPrincipal AuthenticatedPrincipal(string subject = "kc-sub-001",
        string? email = "user@example.com")
    {
        var claims = new List<Claim>
        {
            new(KeycloakClaims.Subject, subject),
            new(ClaimTypes.NameIdentifier, subject),
        };
        if (email != null) claims.Add(new Claim(KeycloakClaims.Email, email));

        var identity = new ClaimsIdentity(claims, authenticationType: "TestScheme");
        return new ClaimsPrincipal(identity);
    }

    private static DefaultHttpContext MakeContext(ClaimsPrincipal? principal = null)
    {
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();
        if (principal != null) ctx.User = principal;
        return ctx;
    }

    // ── Unauthenticated request ───────────────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_UnauthenticatedRequest_SkipsProvisioningAndCallsNext()
    {
        var nextCalled = false;
        var linkSvc = new SpyIdentityLinkService();
        var sut = new CommunityJitProvisioningMiddleware(
            linkSvc, NullLogger<CommunityJitProvisioningMiddleware>.Instance);

        await sut.InvokeAsync(MakeContext(), _ => { nextCalled = true; return Task.CompletedTask; });

        nextCalled.Should().BeTrue();
        linkSvc.FindCallCount.Should().Be(0);
        linkSvc.LinkCallCount.Should().Be(0);
    }

    // ── Authenticated, user already in DB ────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_AuthenticatedUserAlreadyExists_DoesNotProvision()
    {
        var linkSvc = new SpyIdentityLinkService(existingPrincipal: new PrincipalContext
        {
            UserId = Guid.NewGuid(),
            Email = "user@example.com",
            DisplayName = "User",
            AuthProvider = AuthProvider.Keycloak,
            ExternalSubject = "kc-sub-001",
        });
        var sut = new CommunityJitProvisioningMiddleware(
            linkSvc, NullLogger<CommunityJitProvisioningMiddleware>.Instance);

        await sut.InvokeAsync(MakeContext(AuthenticatedPrincipal()), _ => Task.CompletedTask);

        linkSvc.LinkCallCount.Should().Be(0);
    }

    [Fact]
    public async Task InvokeAsync_AuthenticatedUserAlreadyExists_CallsNext()
    {
        var nextCalled = false;
        var linkSvc = new SpyIdentityLinkService(existingPrincipal: new PrincipalContext
        {
            UserId = Guid.NewGuid(),
            Email = "user@example.com",
            DisplayName = "User",
            AuthProvider = AuthProvider.Keycloak,
            ExternalSubject = "kc-sub-001",
        });
        var sut = new CommunityJitProvisioningMiddleware(
            linkSvc, NullLogger<CommunityJitProvisioningMiddleware>.Instance);

        await sut.InvokeAsync(MakeContext(AuthenticatedPrincipal()), _ => { nextCalled = true; return Task.CompletedTask; });

        nextCalled.Should().BeTrue();
    }

    // ── Authenticated, user NOT in DB → JIT provision ────────────────────────

    [Fact]
    public async Task InvokeAsync_AuthenticatedUserNotInDb_CallsLinkIdentityAsync()
    {
        var linkSvc = new SpyIdentityLinkService(existingPrincipal: null);
        var sut = new CommunityJitProvisioningMiddleware(
            linkSvc, NullLogger<CommunityJitProvisioningMiddleware>.Instance);

        await sut.InvokeAsync(MakeContext(AuthenticatedPrincipal()), _ => Task.CompletedTask);

        linkSvc.LinkCallCount.Should().Be(1);
    }

    [Fact]
    public async Task InvokeAsync_AfterProvisioning_StillCallsNext()
    {
        var nextCalled = false;
        var linkSvc = new SpyIdentityLinkService(existingPrincipal: null);
        var sut = new CommunityJitProvisioningMiddleware(
            linkSvc, NullLogger<CommunityJitProvisioningMiddleware>.Instance);

        await sut.InvokeAsync(
            MakeContext(AuthenticatedPrincipal()),
            _ => { nextCalled = true; return Task.CompletedTask; });

        nextCalled.Should().BeTrue();
    }

    // ── Authenticated but token has no Subject (invalid token) ───────────────

    [Fact]
    public async Task InvokeAsync_AuthenticatedButNoSubjectClaim_SkipsProvisioning()
    {
        var identity = new ClaimsIdentity(
            new[] { new Claim(KeycloakClaims.Email, "x@x.com") },
            authenticationType: "TestScheme");
        var principal = new ClaimsPrincipal(identity);

        var linkSvc = new SpyIdentityLinkService();
        var sut = new CommunityJitProvisioningMiddleware(
            linkSvc, NullLogger<CommunityJitProvisioningMiddleware>.Instance);

        await sut.InvokeAsync(MakeContext(principal), _ => Task.CompletedTask);

        linkSvc.FindCallCount.Should().Be(0);
        linkSvc.LinkCallCount.Should().Be(0);
    }

    // ── Known-provisioned cache (positive existence only) ────────────────────

    [Fact]
    public async Task InvokeAsync_SubjectKnownToExist_SecondRequestSkipsDbLookup()
    {
        var linkSvc = new SpyIdentityLinkService(existingPrincipal: new PrincipalContext
        {
            UserId = Guid.NewGuid(),
            Email = "user@example.com",
            DisplayName = "User",
            AuthProvider = AuthProvider.Keycloak,
            ExternalSubject = "kc-sub-001",
        });
        var sut = new CommunityJitProvisioningMiddleware(
            linkSvc, NullLogger<CommunityJitProvisioningMiddleware>.Instance);

        await sut.InvokeAsync(MakeContext(AuthenticatedPrincipal()), _ => Task.CompletedTask);
        await sut.InvokeAsync(MakeContext(AuthenticatedPrincipal()), _ => Task.CompletedTask);

        linkSvc.FindCallCount.Should().Be(1);
        linkSvc.LinkCallCount.Should().Be(0);
    }

    [Fact]
    public async Task InvokeAsync_AfterSuccessfulProvisioning_SecondRequestSkipsDbLookup()
    {
        var linkSvc = new SpyIdentityLinkService(existingPrincipal: null);
        var sut = new CommunityJitProvisioningMiddleware(
            linkSvc, NullLogger<CommunityJitProvisioningMiddleware>.Instance);

        await sut.InvokeAsync(MakeContext(AuthenticatedPrincipal()), _ => Task.CompletedTask);
        await sut.InvokeAsync(MakeContext(AuthenticatedPrincipal()), _ => Task.CompletedTask);

        linkSvc.FindCallCount.Should().Be(1);
        linkSvc.LinkCallCount.Should().Be(1);
    }

    [Fact]
    public async Task InvokeAsync_FailedProvisioning_IsNotCached_RetriesNextRequest()
    {
        var linkSvc = new SpyIdentityLinkService(existingPrincipal: null, linkSucceeds: false);
        var sut = new CommunityJitProvisioningMiddleware(
            linkSvc, NullLogger<CommunityJitProvisioningMiddleware>.Instance);

        await sut.InvokeAsync(MakeContext(AuthenticatedPrincipal()), _ => Task.CompletedTask);
        await sut.InvokeAsync(MakeContext(AuthenticatedPrincipal()), _ => Task.CompletedTask);

        linkSvc.FindCallCount.Should().Be(2);
        linkSvc.LinkCallCount.Should().Be(2);
    }

    [Fact]
    public async Task InvokeAsync_CacheIsPerSubject_DifferentSubjectStillLookedUp()
    {
        var linkSvc = new SpyIdentityLinkService(existingPrincipal: new PrincipalContext
        {
            UserId = Guid.NewGuid(),
            Email = "user@example.com",
            DisplayName = "User",
            AuthProvider = AuthProvider.Keycloak,
            ExternalSubject = "kc-sub-001",
        });
        var sut = new CommunityJitProvisioningMiddleware(
            linkSvc, NullLogger<CommunityJitProvisioningMiddleware>.Instance);

        await sut.InvokeAsync(MakeContext(AuthenticatedPrincipal("kc-sub-001")), _ => Task.CompletedTask);
        await sut.InvokeAsync(MakeContext(AuthenticatedPrincipal("kc-sub-002")), _ => Task.CompletedTask);

        linkSvc.FindCallCount.Should().Be(2);
    }
}

/// <summary>
/// Spy for <see cref="IIdentityLinkService"/> — records calls and returns
/// canned responses so tests can assert provisioning behaviour.
/// </summary>
file sealed class SpyIdentityLinkService(
    PrincipalContext? existingPrincipal = null,
    bool linkSucceeds = true) : IIdentityLinkService
{
    public int FindCallCount { get; private set; }
    public int LinkCallCount { get; private set; }

    public Task<PrincipalContext?> FindByExternalIdentityAsync(
        AuthProvider provider, string externalSubject, CancellationToken ct = default)
    {
        FindCallCount++;
        return Task.FromResult(existingPrincipal);
    }

    public Task<IdentityLinkResult> LinkIdentityAsync(
        ExternalIdentityToken token, CancellationToken ct = default)
    {
        LinkCallCount++;
        return Task.FromResult(new IdentityLinkResult
        {
            Success = linkSucceeds,
            UserId = Guid.NewGuid(),
            Email = token.Email,
            DisplayName = token.DisplayName,
        });
    }

    // Remaining interface members — not exercised by these tests.
    public Task<IReadOnlyList<TenantMembership>> GetUserMembershipsAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TenantMembership>>([]);

    public Task<TenantRole> GetUserTenantRoleAsync(Guid userId, Guid tenantId, CancellationToken ct = default)
        => Task.FromResult(TenantRole.None);
}
