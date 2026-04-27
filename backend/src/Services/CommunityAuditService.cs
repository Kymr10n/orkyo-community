using System.Text.Json;
using Api.Services;
using Npgsql;
using NpgsqlTypes;

namespace Orkyo.Community.Services;

/// <summary>
/// Single-DB implementation of <see cref="IAdminAuditService"/> for the community edition.
/// Writes to the <c>audit_events</c> table in the single community database.
/// </summary>
public sealed class CommunityAuditService : IAdminAuditService
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<CommunityAuditService> _logger;

    public CommunityAuditService(
        IDbConnectionFactory connectionFactory,
        ILogger<CommunityAuditService> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task RecordEventAsync(
        Guid? actorUserId,
        string action,
        string? targetType = null,
        string? targetId = null,
        object? metadata = null)
    {
        try
        {
            await using var conn = _connectionFactory.CreateControlPlaneConnection();
            await conn.OpenAsync();

            using var cmd = new NpgsqlCommand("""
                INSERT INTO audit_events (id, actor_user_id, actor_type, action, target_type, target_id, metadata)
                VALUES (@id, @actor_user_id, @actor_type, @action, @target_type, @target_id, @metadata)
                """, conn);

            cmd.Parameters.AddWithValue("@id", Guid.NewGuid());
            cmd.Parameters.AddWithValue("@actor_user_id", actorUserId.HasValue ? (object)actorUserId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@actor_type", actorUserId.HasValue ? "user" : "system");
            cmd.Parameters.AddWithValue("@action", action);
            cmd.Parameters.AddWithValue("@target_type", (object?)targetType ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@target_id", (object?)targetId ?? DBNull.Value);
            cmd.Parameters.Add(new NpgsqlParameter("@metadata", NpgsqlDbType.Jsonb)
            {
                Value = metadata != null ? JsonSerializer.Serialize(metadata) : DBNull.Value
            });

            await cmd.ExecuteNonQueryAsync();

            _logger.LogDebug("Audit event recorded: {Action} by {ActorId}", action, actorUserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record audit event: {Action}", action);
        }
    }
}
