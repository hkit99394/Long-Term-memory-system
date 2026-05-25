using MemorySystem.Application.MemoryEmbeddings;
using MemorySystem.Infrastructure.Outbox;
using Npgsql;

namespace MemorySystem.Worker;

public sealed class MemoryIndexOutboxJobHandler(
    NpgsqlDataSource dataSource,
    IMemoryEmbeddingProvider embeddingProvider,
    IMemoryChunkEmbeddingStore embeddingStore) : IOutboxJobHandler
{
    public bool CanHandle(string jobType)
    {
        return string.Equals(jobType, MemoryIndexOutboxJobContract.JobType, StringComparison.Ordinal);
    }

    public async Task ProcessAsync(OutboxJob job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);

        if (!CanHandle(job.JobType))
        {
            throw new InvalidOperationException($"Unsupported outbox job type '{job.JobType}'.");
        }

        if (!MemoryIndexOutboxJobContract.IsSupportedAggregateType(job.AggregateType))
        {
            throw new InvalidOperationException(
                $"Outbox job '{MemoryIndexOutboxJobContract.JobType}' has unsupported aggregate type '{job.AggregateType}'.");
        }

        var payload = MemoryIndexOutboxJobContract.ParsePayload(job.PayloadJson);

        if (!string.Equals(payload.AggregateType, job.AggregateType, StringComparison.Ordinal)
            || payload.AggregateId != job.AggregateId)
        {
            throw new InvalidOperationException("Memory index payload does not match the outbox aggregate id.");
        }

        var input = await ReadSearchableChunkInputAsync(payload, cancellationToken);
        var embedding = await embeddingProvider.EmbedAsync(new MemoryEmbeddingRequest(input), cancellationToken);

        await embeddingStore.StoreAsync(
            new MemoryChunkEmbeddingWriteCommand(
                payload.ChunkId,
                embedding.Model,
                embedding.Dimension,
                embedding.Values),
            cancellationToken);
    }

    private async Task<string> ReadSearchableChunkInputAsync(
        MemoryIndexOutboxPayload payload,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            SELECT concat_ws(' ', chunk.title, chunk.content)
            FROM memory_chunks AS chunk
            WHERE chunk.source_type = @aggregate_type
                AND chunk.source_id = @aggregate_id
                AND chunk.id = @chunk_id
                AND chunk.source_event_id = @source_event_id
                AND chunk.search_vector IS NOT NULL
                AND (
                    (
                        @aggregate_type = 'memory_fact'
                        AND EXISTS (
                            SELECT 1
                            FROM memory_facts AS fact
                            WHERE fact.id = @aggregate_id
                                AND fact.source_event_id = @source_event_id
                        )
                    )
                    OR (
                        @aggregate_type = 'role_memory_lens'
                        AND EXISTS (
                            SELECT 1
                            FROM role_memory_lenses AS lens
                            WHERE lens.id = @aggregate_id
                                AND lens.source_event_id = @source_event_id
                        )
                    )
                )
            LIMIT 1;
            """,
            connection);
        command.Parameters.AddWithValue("aggregate_type", payload.AggregateType);
        command.Parameters.AddWithValue("aggregate_id", payload.AggregateId);
        command.Parameters.AddWithValue("chunk_id", payload.ChunkId);
        command.Parameters.AddWithValue("source_event_id", payload.SourceEventId);

        var input = await command.ExecuteScalarAsync(cancellationToken);

        if (input is not string chunkInput || string.IsNullOrWhiteSpace(chunkInput))
        {
            throw new InvalidOperationException("Memory index payload does not reference a searchable memory chunk.");
        }

        return chunkInput;
    }
}
