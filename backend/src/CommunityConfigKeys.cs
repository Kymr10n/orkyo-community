using System.Diagnostics.CodeAnalysis;

namespace Orkyo.Community;

/// <summary>
/// Community-specific configuration keys. Community collapses the control-plane/tenant
/// database split into a single connection named <c>DefaultConnection</c>; the foundation
/// <c>ConfigKeys</c> only defines the multi-database <c>Postgres</c>/<c>ControlPlane</c> keys.
/// </summary>
[ExcludeFromCodeCoverage]
public static class CommunityConfigKeys
{
    public const string DefaultConnection = "DefaultConnection";
    /// <summary>Environment-variable form of <see cref="DefaultConnection"/> (ASP.NET <c>__</c> convention).</summary>
    public const string DefaultConnectionEnvVar = "ConnectionStrings__DefaultConnection";

    /// <summary>
    /// Optional one-time starter setup applied at first start (<c>manufacturing</c> or
    /// <c>office</c>); unset keeps the installation empty. Written by the bundle's
    /// <c>generate-env.sh --starter</c>.
    /// </summary>
    public const string StarterTemplate = "ORKYO_STARTER_TEMPLATE";
}
