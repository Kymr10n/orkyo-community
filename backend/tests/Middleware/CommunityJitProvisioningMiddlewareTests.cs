using System.Security.Claims;
using Api.Integrations.Keycloak;
using Api.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Orkyo.Community.Middleware;

namespace Orkyo.Community.Tests.Middleware;

public class CommunityJitProvisioningMiddlewareTests
{
    private const string Subject = "kc-sub-001";
    private const string Email = "user@example.com";

    public CommunityJitProvisioningMiddlewareTests()
    {
        // The known-provisioned cache is static; clear it so tests don't leak
        // cached subjects into each other.
        CommunityJitProvisioningMiddleware.ClearCache();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>A principal carrying an email and, unless null, a Keycloak subject.</summary>
    private static ClaimsPrincipal PrincipalWithSubject(string? sub, bool authenticated = true)
    {
        var claims = new List<Claim> { new(KeycloakClaims.Email, Email) };
        if (sub is not null)
        {
            claims.Add(new Claim(KeycloakClaims.Subject, sub));
            claims.Add(new Claim(ClaimTypes.NameIdentifier, sub));
        }

        // No authentication type → IsAuthenticated == false, claims or not.
        return new ClaimsPrincipal(authenticated
            ? new ClaimsIdentity(claims, authenticationType: TestConstants.AuthScheme)
            : new ClaimsIdentity(claims));
    }

    private static PrincipalContext ExistingUser(string sub = Subject) => new()
    {
        UserId = Guid.NewGuid(),
        Email = Email,
        DisplayName = "User",
        AuthProvider = AuthProvider.Keycloak,
        ExternalSubject = sub,
    };

    private static CommunityJitProvisioningMiddleware Sut(IIdentityLinkService linkSvc)
        => new(linkSvc, NullLogger<CommunityJitProvisioningMiddleware>.Instance);

    /// <summary>Runs the middleware for <paramref name="principal"/> (null = anonymous) and reports whether next ran.</summary>
    private static async Task<bool> InvokeAsync(CommunityJitProvisioningMiddleware sut, ClaimsPrincipal? principal)
    {
        var nextCalled = false;
        var ctx = new DefaultHttpContext();
        if (principal is not null) ctx.User = principal;

        await sut.InvokeAsync(ctx, _ => { nextCalled = true; return Task.CompletedTask; });

        return nextCalled;
    }

    // ── Requests that never reach the database ───────────────────────────────

    [Theory]
    [InlineData(false)] // anonymous
    [InlineData(true)]  // a subject claim on an identity that is not authenticated
    public async Task InvokeAsync_NotAuthenticated_SkipsProvisioningAndCallsNext(bool withSubjectClaim)
    {
        var linkSvc = new SpyIdentityLinkService();
        var principal = withSubjectClaim ? PrincipalWithSubject(Subject, authenticated: false) : null;

        var nextCalled = await InvokeAsync(Sut(linkSvc), principal);

        nextCalled.Should().BeTrue();
        linkSvc.FindCallCount.Should().Be(0);
        linkSvc.LinkCallCount.Should().Be(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task InvokeAsync_NoOrEmptySubjectClaim_SkipsProvisioningAndCallsNext(string? sub)
    {
        var linkSvc = new SpyIdentityLinkService();

        var nextCalled = await InvokeAsync(Sut(linkSvc), PrincipalWithSubject(sub));

        nextCalled.Should().BeTrue();
        linkSvc.FindCallCount.Should().Be(0);
        linkSvc.LinkCallCount.Should().Be(0);
    }

    // ── Authenticated requests ───────────────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_UserAlreadyExists_DoesNotLink_AndCallsNext()
    {
        var linkSvc = new SpyIdentityLinkService(existingPrincipal: ExistingUser());

        var nextCalled = await InvokeAsync(Sut(linkSvc), PrincipalWithSubject(Subject));

        nextCalled.Should().BeTrue();
        linkSvc.LastFind.Should().Be((AuthProvider.Keycloak, Subject));
        linkSvc.LinkCallCount.Should().Be(0);
    }

    [Fact]
    public async Task InvokeAsync_UserNotInDb_LinksTheTokenIdentity_AndCallsNext()
    {
        var linkSvc = new SpyIdentityLinkService(existingPrincipal: null);

        var nextCalled = await InvokeAsync(Sut(linkSvc), PrincipalWithSubject(Subject));

        nextCalled.Should().BeTrue();
        linkSvc.LastFind.Should().Be((AuthProvider.Keycloak, Subject));
        var token = linkSvc.LastLinkedToken;
        token.Should().NotBeNull();
        token!.Provider.Should().Be(AuthProvider.Keycloak);
        token.Subject.Should().Be(Subject);
        token.Email.Should().Be(Email);
    }

    [Fact]
    public async Task InvokeAsync_WhenTheLookupThrows_PropagatesAndDoesNotCallNext()
    {
        var linkSvc = new SpyIdentityLinkService(findThrows: new InvalidOperationException("db down"));
        var sut = Sut(linkSvc);

        var act = () => InvokeAsync(sut, PrincipalWithSubject(Subject));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("db down");
        linkSvc.LinkCallCount.Should().Be(0);
        // Nothing was cached: the next request looks the subject up again.
        await act.Should().ThrowAsync<InvalidOperationException>();
        linkSvc.FindCallCount.Should().Be(2);
    }

    // ── Known-provisioned cache (positive existence only) ────────────────────

    [Fact]
    public async Task InvokeAsync_SubjectKnownToExist_SecondRequestSkipsDbLookup()
    {
        var linkSvc = new SpyIdentityLinkService(existingPrincipal: ExistingUser());
        var sut = Sut(linkSvc);

        await InvokeAsync(sut, PrincipalWithSubject(Subject));
        await InvokeAsync(sut, PrincipalWithSubject(Subject));

        linkSvc.FindCallCount.Should().Be(1);
        linkSvc.LinkCallCount.Should().Be(0);
    }

    [Fact]
    public async Task InvokeAsync_AfterSuccessfulProvisioning_SecondRequestSkipsDbLookup()
    {
        var linkSvc = new SpyIdentityLinkService(existingPrincipal: null);
        var sut = Sut(linkSvc);

        await InvokeAsync(sut, PrincipalWithSubject(Subject));
        await InvokeAsync(sut, PrincipalWithSubject(Subject));

        linkSvc.FindCallCount.Should().Be(1);
        linkSvc.LinkCallCount.Should().Be(1);
    }

    [Fact]
    public async Task InvokeAsync_FailedProvisioning_IsNotCached_RetriesNextRequest()
    {
        var linkSvc = new SpyIdentityLinkService(existingPrincipal: null, linkSucceeds: false);
        var sut = Sut(linkSvc);

        await InvokeAsync(sut, PrincipalWithSubject(Subject));
        await InvokeAsync(sut, PrincipalWithSubject(Subject));

        linkSvc.FindCallCount.Should().Be(2);
        linkSvc.LinkCallCount.Should().Be(2);
    }

    [Fact]
    public async Task InvokeAsync_CacheIsPerSubject_DifferentSubjectStillLookedUp()
    {
        var linkSvc = new SpyIdentityLinkService(existingPrincipal: ExistingUser());
        var sut = Sut(linkSvc);

        await InvokeAsync(sut, PrincipalWithSubject("kc-sub-001"));
        await InvokeAsync(sut, PrincipalWithSubject("kc-sub-002"));

        linkSvc.FindCallCount.Should().Be(2);
    }
}

/// <summary>
/// Spy for <see cref="IIdentityLinkService"/> — records calls and returns
/// canned responses so tests can assert provisioning behaviour.
/// </summary>
file sealed class SpyIdentityLinkService(
    PrincipalContext? existingPrincipal = null,
    bool linkSucceeds = true,
    Exception? findThrows = null) : IIdentityLinkService
{
    public int FindCallCount { get; private set; }
    public int LinkCallCount { get; private set; }
    public (AuthProvider Provider, string Subject)? LastFind { get; private set; }
    public ExternalIdentityToken? LastLinkedToken { get; private set; }

    public Task<PrincipalContext?> FindByExternalIdentityAsync(
        AuthProvider provider, string externalSubject, CancellationToken ct = default)
    {
        FindCallCount++;
        LastFind = (provider, externalSubject);
        return findThrows is null ? Task.FromResult(existingPrincipal) : Task.FromException<PrincipalContext?>(findThrows);
    }

    public Task<IdentityLinkResult> LinkIdentityAsync(
        ExternalIdentityToken token, CancellationToken ct = default)
    {
        LinkCallCount++;
        LastLinkedToken = token;
        return Task.FromResult(new IdentityLinkResult
        {
            Success = linkSucceeds,
            UserId = Guid.NewGuid(),
            Email = token.Email,
            DisplayName = token.DisplayName,
        });
    }

    // Remaining interface member — not exercised by these tests.
    public Task<TenantRole> GetUserTenantRoleAsync(Guid userId, Guid tenantId, CancellationToken ct = default)
        => Task.FromResult(TenantRole.None);
}
