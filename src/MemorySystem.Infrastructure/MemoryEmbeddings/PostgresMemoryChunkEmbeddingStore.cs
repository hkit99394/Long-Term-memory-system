using MemorySystem.Application.MemoryEmbeddings;
using Npgsql;

namespace MemorySystem.Infrastructure.MemoryEmbeddings;

public sealed class PostgresMemoryChunkEmbeddingStore(NpgsqlDataSource dataSource) : IMemoryChunkEmbeddingStore
{
    public async Task StoreAsync(
        MemoryChunkEmbeddingWriteCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Model);

        if (command.ChunkId == Guid.Empty)
        {
            throw new ArgumentException("Chunk id is required.", nameof(command));
        }

        if (command.Dimension <= 0 || command.Values.Count != command.Dimension)
        {
            throw new ArgumentException("Embedding dimension must match the value count.", nameof(command));
        }

        if (RequiresCurrentChunkGuard(command))
        {
            await StoreIfChunkIsCurrentAsync(command, cancellationToken);
            return;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var sql = new NpgsqlCommand(
            """
            INSERT INTO memory_embeddings (
                chunk_id,
                embedding_model,
                embedding_dimension,
                embedding
            )
            VALUES (
                @chunk_id,
                @embedding_model,
                @embedding_dimension,
                @embedding::vector
            )
            ON CONFLICT (chunk_id, embedding_model)
            DO UPDATE SET
                embedding_dimension = EXCLUDED.embedding_dimension,
                embedding = EXCLUDED.embedding,
                created_at = now();
            """,
            connection);
        sql.Parameters.AddWithValue("chunk_id", command.ChunkId);
        sql.Parameters.AddWithValue("embedding_model", command.Model);
        sql.Parameters.AddWithValue("embedding_dimension", command.Dimension);
        sql.Parameters.AddWithValue("embedding", MemoryEmbeddingVectorLiteral.Format(command.Values));

        await sql.ExecuteNonQueryAsync(cancellationToken);
    }

    private static bool RequiresCurrentChunkGuard(MemoryChunkEmbeddingWriteCommand command)
    {
        var hasAnyGuard =
            command.ExpectedContentHash is not null
            || command.ExpectedSourceEventId.HasValue
            || command.ExpectedSourceType is not null
            || command.ExpectedSourceId.HasValue;

        if (!hasAnyGuard)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(command.ExpectedContentHash)
            || !command.ExpectedSourceEventId.HasValue
            || string.IsNullOrWhiteSpace(command.ExpectedSourceType)
            || !command.ExpectedSourceId.HasValue)
        {
            throw new ArgumentException(
                "Current chunk guards require expected content hash, source event id, source type, and source id.",
                nameof(command));
        }

        return true;
    }

    private async Task StoreIfChunkIsCurrentAsync(
        MemoryChunkEmbeddingWriteCommand command,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var sql = new NpgsqlCommand(
            GuardedStoreSql,
            connection);
        sql.Parameters.AddWithValue("chunk_id", command.ChunkId);
        sql.Parameters.AddWithValue("embedding_model", command.Model);
        sql.Parameters.AddWithValue("embedding_dimension", command.Dimension);
        sql.Parameters.AddWithValue("embedding", MemoryEmbeddingVectorLiteral.Format(command.Values));
        sql.Parameters.AddWithValue("expected_content_hash", command.ExpectedContentHash!);
        sql.Parameters.AddWithValue("expected_source_event_id", command.ExpectedSourceEventId!.Value);
        sql.Parameters.AddWithValue("expected_source_type", command.ExpectedSourceType!);
        sql.Parameters.AddWithValue("expected_source_id", command.ExpectedSourceId!.Value);

        await sql.ExecuteNonQueryAsync(cancellationToken);
    }

    private static readonly string GuardedStoreSql = $"""
        INSERT INTO memory_embeddings (
            chunk_id,
            embedding_model,
            embedding_dimension,
            embedding
        )
        SELECT
            @chunk_id,
            @embedding_model,
            @embedding_dimension,
            @embedding::vector
        WHERE EXISTS (
            {BuildCurrentChunkGuard("@chunk_id")}
        )
        ON CONFLICT (chunk_id, embedding_model)
        DO UPDATE SET
            embedding_dimension = EXCLUDED.embedding_dimension,
            embedding = EXCLUDED.embedding,
            created_at = now()
        WHERE EXISTS (
            {BuildCurrentChunkGuard("memory_embeddings.chunk_id")}
        );
        """;

    private static string BuildCurrentChunkGuard(string chunkIdExpression)
    {
        return $$"""
            SELECT 1
            FROM memory_chunks AS chunk
            WHERE chunk.id = {{chunkIdExpression}}
                AND chunk.source_type = @expected_source_type
                AND chunk.source_id = @expected_source_id
                AND chunk.source_event_id = @expected_source_event_id
                AND chunk.content_hash = @expected_content_hash
                AND chunk.redacted_at IS NULL
                AND (
                    (
                        @expected_source_type = 'memory_fact'
                        AND EXISTS (
                            SELECT 1
                            FROM memory_facts AS fact
                            WHERE fact.id = chunk.source_id
                                AND fact.status = 'active'
                        )
                    )
                    OR (
                        @expected_source_type = 'role_memory_lens'
                        AND EXISTS (
                            SELECT 1
                            FROM role_memory_lenses AS lens
                            WHERE lens.id = chunk.source_id
                                AND lens.status = 'active'
                        )
                    )
                )
            """;
    }
}
