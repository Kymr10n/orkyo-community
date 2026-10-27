using Api.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Orkyo.Community.Tests.Migrations;

/// <summary>
/// Community is invitation-only: nothing grants a membership implicitly, the shipped admin
/// account is the only pre-created user, and the API rejects identities nobody invited.
/// Each fact failed on main before migration 3030.
/// </summary>
[Trait(TestCategories.Name, TestCategories.Database)]
[Collection("Database collection")]
public sealed class InvitationOnlyTests
{
    private readonly DatabaseFixture _fixture;

    public InvitationOnlyTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ACreatedUser_GetsNoMembership()
    {
        await using var conn = new NpgsqlConnection(_fixture.TenantConnectionString);
        await conn.OpenAsync();
        var userId = Guid.NewGuid();
        try
        {
            await using var insert = new NpgsqlCommand(
                @"INSERT INTO users (id, email, display_name, status, created_at, updated_at)
                  VALUES (@id, @email, 'Nobody Invited', 'active', NOW(), NOW())", conn);
            insert.Parameters.AddWithValue("id", userId);
            insert.Parameters.AddWithValue("email", $"{userId:N}@orkyo.example");
            await insert.ExecuteNonQueryAsync();

            await using var count = new NpgsqlCommand(
                "SELECT COUNT(*) FROM tenant_memberships WHERE user_id = @id", conn);
            count.Parameters.AddWithValue("id", userId);
            (await count.ExecuteScalarAsync()).Should().Be(0L, "the auto-grant trigger is gone");
        }
        finally
        {
            await using var delete = new NpgsqlCommand("DELETE FROM users WHERE id = @id", conn);
            delete.Parameters.AddWithValue("id", userId);
            await delete.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task TheShippedAdmin_IsSeededWithAnAdminMembership()
    {
        await using var conn = new NpgsqlConnection(_fixture.TenantConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            @"SELECT m.role FROM users u
              JOIN tenant_memberships m ON m.user_id = u.id
              WHERE u.email = 'admin@example.com' AND u.status = 'active'", conn);
        (await cmd.ExecuteScalarAsync()).Should().Be("admin", "the first sign-in links to this row");
    }

    [Fact]
    public void SelfRegistration_IsOff()
    {
        var options = _fixture.Factory.Services.GetRequiredService<IOptions<IdentityProvisioningOptions>>();
        options.Value.AllowSelfRegistration.Should().BeFalse("an identity nobody invited gets no account");
    }
}
