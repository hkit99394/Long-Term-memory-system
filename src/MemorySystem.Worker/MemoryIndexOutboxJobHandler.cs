using MemorySystem.Application.MemoryEmbeddings;
using MemorySystem.Infrastructure.Outbox;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace MemorySystem.Worker;

public sealed class MemoryIndexOutboxJobHandler(
    NpgsqlDataSource dataSource,
    IMemoryEmbeddingProvider embeddingProvider,
    IMemoryChunkEmbeddingStore embeddingStore,
    ILogger<MemoryIndexOutboxJobHandler> logger) : IOutboxJobHandler
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

        var input = await ReadSearchableChunkInputAsync(job.Id, payload, cancellationToken);

        if (input is null)
        {
            return;
        }

        var embedding = await embeddingProvider.EmbedAsync(new MemoryEmbeddingRequest(input.SearchableInput), cancellationToken);

        await embeddingStore.StoreAsync(
            new MemoryChunkEmbeddingWriteCommand(
                payload.ChunkId,
                embedding.Model,
                embedding.Dimension,
                embedding.Values,
                input.ContentHash,
                input.SourceEventId,
                payload.AggregateType,
                payload.AggregateId),
            cancellationToken);
    }

    private async Task<MemoryIndexChunkInput?> ReadSearchableChunkInputAsync(
        Guid jobId,
        MemoryIndexOutboxPayload payload,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            SELECT
                chunk.source_event_id,
                chunk.redacted_at IS NOT NULL,
                chunk.search_vector IS NULL,
                concat_ws(' ', chunk.title, chunk.content),
                chunk.content_hash,
                fact.status,
                lens.status
            FROM memory_chunks AS chunk
            LEFT JOIN memory_facts AS fact
                ON chunk.source_type = 'memory_fact'
                AND fact.id = chunk.source_id
            LEFT JOIN role_memory_lenses AS lens
                ON chunk.source_type = 'role_memory_lens'
                AND lens.id = chunk.source_id
            WHERE chunk.source_type = @aggregate_type
                AND chunk.source_id = @aggregate_id
                AND chunk.id = @chunk_id
            LIMIT 1;
            """,
            connection);
        command.Parameters.AddWithValue("aggregate_type", payload.AggregateType);
        command.Parameters.AddWithValue("aggregate_id", payload.AggregateId);
        command.Parameters.AddWithValue("chunk_id", payload.ChunkId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException(
                $"Memory index outbox job {jobId} does not reference an existing memory chunk.");
        }

        var chunk = new MemoryIndexChunkState(
            reader.GetGuid(0),
            reader.GetBoolean(1),
            reader.GetBoolean(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6));

        if (chunk.SourceEventId != payload.SourceEventId)
        {
            throw new InvalidOperationException(
                $"Memory index outbox job {jobId} payload does not match the memory chunk source event.");
        }

        if (chunk.Redacted)
        {
            logger.LogInformation(
                "Skipping memory index outbox job {JobId} because chunk {ChunkId} is redacted.",
                jobId,
                payload.ChunkId);
            return null;
        }

        if (chunk.SearchVectorMissing)
        {
            throw new InvalidOperationException(
                $"Memory index outbox job {jobId} references chunk {payload.ChunkId} without a search vector.");
        }

        var sourceStatus = payload.AggregateType switch
        {
            MemoryIndexOutboxJobContract.AggregateType => chunk.MemoryFactStatus,
            MemoryIndexOutboxJobContract.RoleMemoryLensAggregateType => chunk.RoleMemoryLensStatus,
            _ => null
        };

        if (sourceStatus is null)
        {
            throw new InvalidOperationException(
                $"Memory index outbox job {jobId} references a missing {payload.AggregateType} source.");
        }

        if (!string.Equals(sourceStatus, "active", StringComparison.Ordinal))
        {
            logger.LogInformation(
                "Skipping memory index outbox job {JobId} because {AggregateType} {AggregateId} is {Status}.",
                jobId,
                payload.AggregateType,
                payload.AggregateId,
                sourceStatus);
            return null;
        }

        return string.IsNullOrWhiteSpace(chunk.SearchableInput)
            ? throw new InvalidOperationException(
                $"Memory index outbox job {jobId} references chunk {payload.ChunkId} without searchable input.")
            : new MemoryIndexChunkInput(chunk.SearchableInput, chunk.ContentHash, chunk.SourceEventId);
    }

    private sealed record MemoryIndexChunkState(
        Guid SourceEventId,
        bool Redacted,
        bool SearchVectorMissing,
        string SearchableInput,
        string ContentHash,
        string? MemoryFactStatus,
        string? RoleMemoryLensStatus);

    private sealed record MemoryIndexChunkInput(
        string SearchableInput,
        string ContentHash,
        Guid SourceEventId);
}
