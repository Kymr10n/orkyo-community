namespace Orkyo.Community.Tenant;

/// <summary>
/// Configuration for the single community tenant.
/// Bound from environment variables at startup.
/// </summary>
public sealed class SingleTenantOptions
{
    public const string SectionKey = "Community";

    public Guid TenantId { get; init; } = new Guid("00000000-0000-0000-0000-000000000001");
    public string TenantSlug { get; init; } = "community";
    public string TenantName { get; init; } = "Orkyo Community";
}
