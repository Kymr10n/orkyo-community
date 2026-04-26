namespace Orkyo.Community.Tests;

public static class TestConstants
{
    public const string TenantSlug = "test";
    public const string TenantDatabase = "tenant_test";

    public static string TestBearerToken { get; } = Convert.ToBase64String(
        System.Text.Encoding.UTF8.GetBytes(
            System.Text.Json.JsonSerializer.Serialize(new
            {
                UserId = "11111111-1111-1111-1111-111111111111",
                Email = "test@orkyo.example",
                DisplayName = "Test User",
                TenantId = "00000000-0000-0000-0000-000000000001",
                TenantSlug = "test",
                IsTenantAdmin = false,
                Role = "user"
            })));
}
