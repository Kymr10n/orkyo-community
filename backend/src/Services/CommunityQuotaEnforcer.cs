using Api.Security.Quotas;

namespace Orkyo.Community.Services;

/// <summary>
/// Community quota enforcer — all resources are unlimited.
/// Community runs on dedicated infrastructure so there are no tier-based caps.
/// </summary>
public sealed class CommunityQuotaEnforcer : IQuotaEnforcer
{
    public void EnforceLimit(string resourceType, int currentCount) { }

    public int GetLimit(string resourceType) => -1;
}
