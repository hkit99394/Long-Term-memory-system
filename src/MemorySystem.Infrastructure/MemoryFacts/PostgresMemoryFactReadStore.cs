using MemorySystem.Application.MemoryFacts;
using Npgsql;

namespace MemorySystem.Infrastructure.MemoryFacts;

public sealed class PostgresMemoryFactReadStore(NpgsqlDataSource dataSource) : IMemoryFactReadStore
{
    public async Task<MemoryFactRecord?> FindAsync(
        Guid memoryFactId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        await using var command = new NpgsqlCommand(
            """
            SELECT
                id,
                scope_type,
                scope_id,
                namespace,
                user_principal_id,
                project_id,
                org_id,
                role_id,
                agent_principal_id,
                memory_type,
                visibility,
                subject,
                predicate,
                object,
                confidence,
                status,
                source_event_id,
                proposed_by_principal_id
            FROM memory_facts
            WHERE id = @memory_fact_id;
            """,
            connection);
        command.Parameters.AddWithValue("memory_fact_id", memoryFactId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken)
            ? new MemoryFactRecord(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetGuid(4),
                reader.IsDBNull(5) ? null : reader.GetGuid(5),
                reader.IsDBNull(6) ? null : reader.GetGuid(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetGuid(8),
                reader.GetString(9),
                reader.GetString(10),
                reader.GetString(11),
                reader.GetString(12),
                reader.GetString(13),
                reader.GetDecimal(14),
                reader.GetString(15),
                reader.GetGuid(16),
                reader.IsDBNull(17) ? null : reader.GetGuid(17))
            : null;
    }
}
