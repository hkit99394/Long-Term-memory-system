using System.Security.Cryptography;
using System.Text;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.Infrastructure.Outbox;

internal static class MemoryIndexWriteOperations
{
    public const string RedactedChunkContent = "[redacted]";

    public static string BuildFactChunkContent(string subject, string predicate, string objectValue)
    {
        return $"{subject} {predicate} {objectValue}";
    }

    public static string ComputeContentHash(string content)
    {
        return ComputeSha256(content);
    }

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

    public static async Task UpdateMemoryChunkAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid chunkId,
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
            UPDATE memory_chunks
            SET namespace = @namespace,
                scope_type = @scope_type,
                scope_id = @scope_id,
                title = @title,
                content = @content,
                content_hash = @content_hash,
                trust_level = @trust_level,
                source_event_id = @source_event_id,
                redacted_at = NULL
            WHERE id = @chunk_id;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("chunk_id", chunkId);
        command.Parameters.AddWithValue("namespace", namespaceValue);
        command.Parameters.AddWithValue("scope_type", scopeType);
        command.Parameters.AddWithValue("scope_id", scopeId);
        command.Parameters.AddWithValue("title", title);
        command.Parameters.AddWithValue("content", content);
        command.Parameters.AddWithValue("content_hash", ComputeSha256(content));
        command.Parameters.AddWithValue("trust_level", trustLevel);
        command.Parameters.AddWithValue("source_event_id", sourceEventId);

        var updatedRows = await command.ExecuteNonQueryAsync(cancellationToken);

        if (updatedRows > 0)
        {
            await DeleteEmbeddingsForChunkAsync(connection, transaction, chunkId, cancellationToken);
        }
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

    public static async Task UpsertOutboxJobAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
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
            )
            ON CONFLICT (idempotency_key)
            DO UPDATE SET
                payload = EXCLUDED.payload,
                status = 'pending',
                attempts = 0,
                available_at = now(),
                locked_until = NULL,
                locked_by = NULL,
                last_error = NULL;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("job_type", MemoryIndexOutboxJobContract.JobType);
        command.Parameters.AddWithValue("aggregate_type", aggregateType);
        command.Parameters.AddWithValue("aggregate_id", aggregateId);
        command.Parameters.AddWithValue(
            "idempotency_key",
            MemoryIndexOutboxJobContract.CreateIdempotencyKey(aggregateType, aggregateId));
        command.Parameters.Add("payload", NpgsqlDbType.Jsonb).Value =
            MemoryIndexOutboxJobContract.SerializePayload(aggregateType, aggregateId, chunkId, sourceEventId);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public static async Task RedactMemoryFactChunksAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid memoryFactId,
        CancellationToken cancellationToken)
    {
        const string deleteEmbeddingsSql = """
            DELETE FROM memory_embeddings AS embedding
            USING memory_chunks AS chunk
            WHERE embedding.chunk_id = chunk.id
                AND chunk.source_type = @source_type
                AND chunk.source_id = @source_id;
            """;

        await using (var deleteEmbeddings = new NpgsqlCommand(deleteEmbeddingsSql, connection, transaction))
        {
            deleteEmbeddings.Parameters.AddWithValue("source_type", MemoryIndexOutboxJobContract.AggregateType);
            deleteEmbeddings.Parameters.AddWithValue("source_id", memoryFactId);

            await deleteEmbeddings.ExecuteNonQueryAsync(cancellationToken);
        }

        const string redactChunksSql = """
            UPDATE memory_chunks
            SET title = NULL,
                content = @content,
                content_hash = @content_hash,
                redacted_at = now()
            WHERE source_type = @source_type
                AND source_id = @source_id;
            """;

        await using var redactChunks = new NpgsqlCommand(redactChunksSql, connection, transaction);
        redactChunks.Parameters.AddWithValue("source_type", MemoryIndexOutboxJobContract.AggregateType);
        redactChunks.Parameters.AddWithValue("source_id", memoryFactId);
        redactChunks.Parameters.AddWithValue("content", RedactedChunkContent);
        redactChunks.Parameters.AddWithValue("content_hash", ComputeContentHash(RedactedChunkContent));

        await redactChunks.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string ComputeSha256(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));

        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static async Task DeleteEmbeddingsForChunkAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid chunkId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            DELETE FROM memory_embeddings
            WHERE chunk_id = @chunk_id;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("chunk_id", chunkId);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
