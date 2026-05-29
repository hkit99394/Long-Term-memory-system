using System.Text.Json;
using MemorySystem.Application.Retention;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.Infrastructure.Retention;

public sealed class PostgresEphemeralEventRetentionStore(NpgsqlDataSource dataSource) : IEphemeralEventRetentionStore
{
    private const int CommandTimeoutSeconds = 10;

    private static readonly string MinimizedPayloadJson = JsonSerializer.Serialize(new
    {
        minimized = true,
        reason = "ephemeral_retention_expired",
        message = "Raw event payload minimized after the ephemeral retention window."
    });

    public async Task<EphemeralEventMinimizationResult> MinimizeExpiredAsync(
        EphemeralEventMinimizationCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.BatchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(command), command.BatchSize, "Retention minimization batch size must be greater than zero.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var sql = new NpgsqlCommand(
            """
            WITH candidates AS (
                SELECT event.id
                FROM events AS event
                WHERE event.retention_class = 'ephemeral'
                    AND event.redaction_status = 'none'
                    AND event.created_at <= @cutoff
                    AND NOT EXISTS (
                        SELECT 1
                        FROM memory_facts AS fact
                        WHERE fact.source_event_id = event.id
                    )
                    AND NOT EXISTS (
                        SELECT 1
                        FROM role_memory_lenses AS lens
                        WHERE lens.source_event_id = event.id
                    )
                    AND NOT EXISTS (
                        SELECT 1
                        FROM memory_chunks AS chunk
                        WHERE chunk.source_event_id = event.id
                    )
                    AND NOT EXISTS (
                        SELECT 1
                        FROM memory_reviews AS review
                        WHERE review.source_event_id = event.id
                    )
                    AND NOT EXISTS (
                        SELECT 1
                        FROM memory_redactions AS redaction
                        WHERE redaction.source_event_id = event.id
                    )
                    AND NOT EXISTS (
                        SELECT 1
                        FROM vault_exports AS export
                        WHERE export.source_event_id = event.id
                    )
                    AND NOT EXISTS (
                        SELECT 1
                        FROM events AS redacted_event
                        WHERE redacted_event.redaction_event_id = event.id
                    )
                ORDER BY event.created_at, event.id
                FOR UPDATE SKIP LOCKED
                LIMIT @batch_size
            )
            UPDATE events AS event
            SET content = @minimized_payload,
                external_payload_uri = NULL,
                redaction_status = 'redacted',
                redacted_at = now()
            FROM candidates
            WHERE event.id = candidates.id;
            """,
            connection);
        sql.CommandTimeout = CommandTimeoutSeconds;
        sql.Parameters.AddWithValue("cutoff", command.Cutoff);
        sql.Parameters.AddWithValue("batch_size", command.BatchSize);
        sql.Parameters.Add("minimized_payload", NpgsqlDbType.Jsonb).Value = MinimizedPayloadJson;

        var minimizedEvents = await sql.ExecuteNonQueryAsync(cancellationToken);

        return new EphemeralEventMinimizationResult(minimizedEvents);
    }
}
