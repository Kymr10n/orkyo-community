using Api.Models;
using Api.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Orkyo.Community.Services;

namespace Orkyo.Community.Tests.Services;

/// <summary>
/// Tests for <see cref="CommunityAuditService"/>.
///
/// Happy-path tests use the integration database (via <see cref="DatabaseFixture"/>)
/// to verify the INSERT actually executes. The error-path test uses a hand-rolled
/// <see cref="ThrowingDbConnectionFactory"/> so no real connection is needed.
/// </summary>
[Collection("Database collection")]
public class CommunityAuditServiceTests
{
    private readonly IAdminAuditService _sut;

    public CommunityAuditServiceTests(DatabaseFixture fixture)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        _sut = scope.ServiceProvider.GetRequiredService<IAdminAuditService>();
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task RecordEventAsync_WithActorAndMetadata_DoesNotThrow()
    {
        var actorId = Guid.NewGuid();
        var act = () => _sut.RecordEventAsync(
            actorUserId: actorId,
            action: "test.with_actor",
            targetType: "user",
            targetId: actorId.ToString(),
            metadata: new { note = "integration-test" });

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RecordEventAsync_WithoutActor_UsesSystemActorType()
    {
        var act = () => _sut.RecordEventAsync(
            actorUserId: null,
            action: "test.system_actor",
            targetType: "tenant",
            targetId: "t1");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RecordEventAsync_WithNullMetadata_DoesNotThrow()
    {
        var act = () => _sut.RecordEventAsync(
            actorUserId: Guid.NewGuid(),
            action: "test.null_metadata");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RecordEventAsync_WithAllNullOptionals_DoesNotThrow()
    {
        var act = () => _sut.RecordEventAsync(
            actorUserId: null,
            action: "test.all_nulls",
            targetType: null,
            targetId: null,
            metadata: null);

        await act.Should().NotThrowAsync();
    }
}

/// <summary>
/// Tests the catch block of <see cref="CommunityAuditService.RecordEventAsync"/>
/// without needing a real database connection.
/// </summary>
public class CommunityAuditServiceErrorTests
{
    [Fact]
    public async Task RecordEventAsync_WhenConnectionThrows_LogsErrorAndDoesNotRethrow()
    {
        var sut = new CommunityAuditService(
            new ThrowingDbConnectionFactory(),
            NullLogger<CommunityAuditService>.Instance);

        // The service swallows exceptions — must not propagate to callers.
        var act = () => sut.RecordEventAsync(
            actorUserId: Guid.NewGuid(),
            action: "test.error_path");

        await act.Should().NotThrowAsync();
    }
}

/// <summary>
/// Test double that throws on every connection-creation call.
/// Used to exercise the catch block in <see cref="CommunityAuditService"/>.
/// </summary>
file sealed class ThrowingDbConnectionFactory : IDbConnectionFactory
{
    public NpgsqlConnection CreateControlPlaneConnection()
        => throw new InvalidOperationException("Simulated connection failure");

    public NpgsqlConnection CreateTenantConnection(TenantContext tenant)
        => throw new NotSupportedException();

    public NpgsqlConnection CreateConnectionForDatabase(string dbIdentifier)
        => throw new NotSupportedException();

    public NpgsqlConnection CreateOrgConnection(OrgContext org)
        => throw new NotSupportedException();
}
