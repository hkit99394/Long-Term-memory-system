using MemorySystem.Application.MemoryContext;
using MemorySystem.Infrastructure.DomainMapping;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.Infrastructure.MemoryContext;

public sealed class PostgresMemoryContextPacketObservationStore(NpgsqlDataSource dataSource)
    : IMemoryContextPacketObservationStore
{
    public async Task RecordAsync(
        MemoryContextPacketObservation observation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(observation);
        Validate(observation);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO memory_context_packets (
                id,
                principal_id,
                query_hash,
                target_scope_type,
                target_scope_id,
                role_id,
                item_count
            )
            VALUES (
                @id,
                @principal_id,
                @query_hash,
                @target_scope_type,
                @target_scope_id,
                @role_id,
                @item_count
            )
            ON CONFLICT (id) DO UPDATE
            SET
                last_observed_at = now(),
                item_count = EXCLUDED.item_count,
                observation_count = memory_context_packets.observation_count + 1
            WHERE memory_context_packets.principal_id = EXCLUDED.principal_id;
            """,
            connection);

        AddObservationParameters(command, observation);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<MemoryContextPacketObservation?> FindAsync(
        Guid packetId,
        Guid principalId,
        CancellationToken cancellationToken = default)
    {
        if (packetId == Guid.Empty || principalId == Guid.Empty)
        {
            return null;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            SELECT
                id,
                principal_id,
                query_hash,
                target_scope_type,
                target_scope_id,
                role_id,
                item_count
            FROM memory_context_packets
            WHERE id = @id
                AND principal_id = @principal_id;
            """,
            connection);
        command.Parameters.AddWithValue("id", packetId);
        command.Parameters.AddWithValue("principal_id", principalId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var targetScope = reader.IsDBNull(3)
            ? null
            : PostgresDomainMapping.RequireScope(reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4));

        return new MemoryContextPacketObservation(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            targetScope?.ScopeType,
            targetScope?.ScopeId,
            PostgresDomainMapping.NormalizeOptionalRoleId(reader.IsDBNull(5) ? null : reader.GetString(5)),
            reader.GetInt32(6));
    }

    private static void AddObservationParameters(
        NpgsqlCommand command,
        MemoryContextPacketObservation observation)
    {
        var targetScope = string.IsNullOrWhiteSpace(observation.TargetScopeType)
            ? null
            : PostgresDomainMapping.RequireScope(observation.TargetScopeType, observation.TargetScopeId);
        command.Parameters.AddWithValue("id", observation.PacketId);
        command.Parameters.AddWithValue("principal_id", observation.PrincipalId);
        command.Parameters.AddWithValue("query_hash", observation.QueryHash);
        command.Parameters.Add("target_scope_type", NpgsqlDbType.Text).Value =
            targetScope is null ? DBNull.Value : targetScope.ScopeType;
        command.Parameters.Add("target_scope_id", NpgsqlDbType.Text).Value =
            targetScope is null ? DBNull.Value : targetScope.ScopeId;
        command.Parameters.Add("role_id", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(observation.RoleId) ? DBNull.Value : PostgresDomainMapping.RequireRoleId(observation.RoleId);
        command.Parameters.AddWithValue("item_count", observation.ItemCount);
    }

    private static void Validate(MemoryContextPacketObservation observation)
    {
        if (observation.PacketId == Guid.Empty)
        {
            throw new ArgumentException("Packet id is required.", nameof(observation));
        }

        if (observation.PrincipalId == Guid.Empty)
        {
            throw new ArgumentException("Principal id is required.", nameof(observation));
        }

        if (string.IsNullOrWhiteSpace(observation.QueryHash)
            || !observation.QueryHash.StartsWith("sha256:", StringComparison.Ordinal))
        {
            throw new ArgumentException("Query hash is required.", nameof(observation));
        }

        if (string.IsNullOrWhiteSpace(observation.TargetScopeType) != string.IsNullOrWhiteSpace(observation.TargetScopeId))
        {
            throw new ArgumentException("Target scope type and id must be provided together.", nameof(observation));
        }

        if (observation.ItemCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(observation), observation.ItemCount, "Item count cannot be negative.");
        }
    }
}
