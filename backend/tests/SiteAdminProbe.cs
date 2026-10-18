namespace Orkyo.Community.Tests;

/// <summary>
/// The site-admin principal the rate-limit wiring probes use. The fixture seeds it as a linked
/// user: access is by invitation only, so a Keycloak subject nobody provisioned gets no principal.
/// </summary>
public static class SiteAdminProbe
{
    public static readonly Guid UserId = new("22222222-2222-2222-2222-222222222222");
    public const string Email = "siteadmin@orkyo.example";
    public const string Subject = "kc-siteadmin-ratelimit";
}
