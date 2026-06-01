using Api.Services;
using Orkyo.Community.Services;

namespace Orkyo.Community.Tests.Services;

/// <summary>
/// Unit tests for <see cref="NullBreakGlassSessionStore"/>.
/// All methods are no-ops by design (break-glass is a SaaS-only concept).
/// Tests document and cover the no-op contract.
/// </summary>
public class NullBreakGlassSessionStoreTests
{
    private static readonly NullBreakGlassSessionStore Sut = new();
    private static readonly Guid AdminId = Guid.NewGuid();

    [Fact]
    public void Create_AlwaysReturnsNull()
    {
        var result = Sut.Create(AdminId, "acme", "testing", TimeSpan.FromHours(1));
        result.Should().BeNull();
    }

    [Fact]
    public void HasActiveSession_AlwaysReturnsFalse()
    {
        var result = Sut.HasActiveSession(AdminId, "acme");
        result.Should().BeFalse();
    }

    [Fact]
    public void GetActiveSession_AlwaysReturnsNull()
    {
        var result = Sut.GetActiveSession(AdminId, "acme");
        result.Should().BeNull();
    }

    [Fact]
    public void TryRenew_AlwaysReturnsNotFound()
    {
        var result = Sut.TryRenew("session-id", AdminId);
        result.Session.Should().BeNull();
        result.Reason.Should().Be(RenewFailureReason.NotFound);
    }

    [Fact]
    public void TryRevoke_AlwaysReturnsNull()
    {
        var result = Sut.TryRevoke("session-id", AdminId);
        result.Should().BeNull();
    }
}
