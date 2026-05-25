using System.Security.Cryptography;
using System.Text;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.Infrastructure.Outbox;

internal static class MemoryIndexWriteOperations
{
    public static async Task InsertMemoryChunkAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid chunkId,
        string sourceType,
        Guid sourceId,
        string namespaceValue,
        string scopeType,
        string scopeId,
        string title,
        string content,
        string trustLevel,
        Guid sourceEventId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO memory_chunks (
                id,
                source_type,
                source_id,
                namespace,
                scope_type,
                scope_id,
                title,
                content,
                content_hash,
                trust_level,
                source_event_id
            )
            VALUES (
                @id,
                @source_type,
                @source_id,
                @namespace,
                @scope_type,
                @scope_id,
                @title,
                @content,
                @content_hash,
                @trust_level,
                @source_event_id
            );
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("id", chunkId);
        command.Parameters.AddWithValue("source_type", sourceType);
        command.Parameters.AddWithValue("source_id", sourceId);
        command.Parameters.AddWithValue("namespace", namespaceValue);
        command.Parameters.AddWithValue("scope_type", scopeType);
        command.Parameters.AddWithValue("scope_id", scopeId);
        command.Parameters.AddWithValue("title", title);
        command.Parameters.AddWithValue("content", content);
        command.Parameters.AddWithValue("content_hash", ComputeSha256(content));
        command.Parameters.AddWithValue("trust_level", trustLevel);
        command.Parameters.AddWithValue("source_event_id", sourceEventId);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public static async Task InsertOutboxJobAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid outboxJobId,
        string aggregateType,
        Guid aggregateId,
        Guid chunkId,
        Guid sourceEventId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO outbox_jobs (
                id,
                job_type,
                aggregate_type,
                aggregate_id,
                idempotency_key,
                payload,
                status
            )
            VALUES (
                @id,
                @job_type,
                @aggregate_type,
                @aggregate_id,
                @idempotency_key,
                @payload,
                'pending'
            );
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("id", outboxJobId);
        command.Parameters.AddWithValue("job_type", MemoryIndexOutboxJobContract.JobType);
        command.Parameters.AddWithValue("aggregate_type", aggregateType);
        command.Parameters.AddWithValue("aggregate_id", aggregateId);
        command.Parameters.AddWithValue("idempotency_key", MemoryIndexOutboxJobContract.CreateIdempotencyKey(aggregateType, aggregateId));
        command.Parameters.Add("payload", NpgsqlDbType.Jsonb).Value =
            MemoryIndexOutboxJobContract.SerializePayload(aggregateType, aggregateId, chunkId, sourceEventId);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string ComputeSha256(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));

        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }
}
