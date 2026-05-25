using System.Globalization;
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
        sql.Parameters.AddWithValue("embedding", ToVectorLiteral(command.Values));

        await sql.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string ToVectorLiteral(IReadOnlyList<float> values)
    {
        foreach (var value in values)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentException("Embedding values must be finite.", nameof(values));
            }
        }

        return "[" + string.Join(
            ",",
            values.Select(value => value.ToString("G9", CultureInfo.InvariantCulture))) + "]";
    }
}
