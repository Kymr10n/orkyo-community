using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Orkyo.Community.Tests;

public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("Authorization"))
            return Task.FromResult(AuthenticateResult.NoResult());

        var authHeader = Request.Headers["Authorization"].ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            return Task.FromResult(AuthenticateResult.NoResult());

        var token = authHeader.Substring("Bearer ".Length).Trim();

        try
        {
            var jsonBytes = Convert.FromBase64String(token);
            var json = System.Text.Encoding.UTF8.GetString(jsonBytes);
            var tokenData = JsonSerializer.Deserialize<TestTokenData>(json);

            if (tokenData == null)
                return Task.FromResult(AuthenticateResult.Fail("Invalid token data"));

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, tokenData.UserId),
                new(ClaimTypes.Email, tokenData.Email),
                new(ClaimTypes.Name, tokenData.DisplayName),
                new("user_id", tokenData.UserId),
                new("tenant_id", tokenData.TenantId),
                new("tenant_slug", tokenData.TenantSlug),
                new("is_tenant_admin", tokenData.IsTenantAdmin.ToString()),
                new("role", tokenData.Role)
            };

            if (!string.IsNullOrEmpty(tokenData.Sub))
                claims.Add(new Claim("sub", tokenData.Sub));
            if (!string.IsNullOrEmpty(tokenData.Sid))
                claims.Add(new Claim("sid", tokenData.Sid));

            if (tokenData.RealmRoles != null && tokenData.RealmRoles.Length > 0)
            {
                var rolesJson = JsonSerializer.Serialize(new { roles = tokenData.RealmRoles });
                claims.Add(new Claim("realm_access", rolesJson));
            }

            var identity = new ClaimsIdentity(claims, "TestScheme");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "TestScheme");

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
        catch (Exception ex)
        {
            return Task.FromResult(AuthenticateResult.Fail($"Token decode failed: {ex.Message}"));
        }
    }

    private class TestTokenData
    {
        public string UserId { get; set; } = "";
        public string Email { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string TenantId { get; set; } = "";
        public string TenantSlug { get; set; } = "";
        public bool IsTenantAdmin { get; set; }
        public string Role { get; set; } = "";
        public string? Sub { get; set; }
        public string? Sid { get; set; }
        public string[]? RealmRoles { get; set; }
    }
}
