namespace Orkyo.Community;

/// <summary>
/// Community-specific configuration keys. Community collapses the control-plane/tenant
/// database split into a single connection named <c>DefaultConnection</c>; the foundation
/// <c>ConfigKeys</c> only defines the multi-database <c>Postgres</c>/<c>ControlPlane</c> keys.
/// </summary>
public static class CommunityConfigKeys
{
    public const string DefaultConnection = "DefaultConnection";
}
