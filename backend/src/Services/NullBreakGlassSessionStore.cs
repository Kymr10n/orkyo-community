using Api.Services;

namespace Orkyo.Community.Services;

/// <summary>
/// No-op implementation of <see cref="IBreakGlassSessionStore"/> for community.
/// Break-glass is a SaaS concept (admin crossing tenant boundaries). In the
/// single-tenant community edition there are no tenant boundaries to cross,
/// so all session operations are no-ops.
/// </summary>
public sealed class NullBreakGlassSessionStore : IBreakGlassSessionStore
{
    public BreakGlassSession? Create(Guid adminId, string tenantSlug, string reason, TimeSpan? duration = null)
        => null;

    public bool HasActiveSession(Guid adminId, string tenantSlug)
        => false;

    public BreakGlassSession? GetActiveSession(Guid adminId, string tenantSlug)
        => null;

    public RenewResult TryRenew(string sessionId, Guid adminId, TimeSpan? extension = null)
        => RenewResult.NotFound();

    public BreakGlassSession? TryRevoke(string sessionId, Guid adminId)
        => null;
}
