using System.Text.Json;
using MemorySystem.Infrastructure.Outbox;
using Npgsql;

namespace MemorySystem.Worker;

public sealed class MemoryIndexOutboxJobHandler(NpgsqlDataSource dataSource) : IOutboxJobHandler
{
    private const string JobType = "memory.index";
    private const string AggregateType = "memory_fact";

    public bool CanHandle(string jobType)
    {
        return string.Equals(jobType, JobType, StringComparison.Ordinal);
    }

    public async Task ProcessAsync(OutboxJob job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);

        if (!CanHandle(job.JobType))
        {
            throw new InvalidOperationException($"Unsupported outbox job type '{job.JobType}'.");
        }

        if (!string.Equals(job.AggregateType, AggregateType, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Outbox job '{JobType}' requires aggregate type '{AggregateType}'.");
        }

        var payload = MemoryIndexPayload.Parse(job.PayloadJson);

        if (payload.MemoryFactId != job.AggregateId)
        {
            throw new InvalidOperationException("Memory index payload does not match the outbox aggregate id.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            SELECT EXISTS (
                SELECT 1
                FROM memory_facts AS fact
                JOIN memory_chunks AS chunk
                    ON chunk.source_type = 'memory_fact'
                    AND chunk.source_id = fact.id
                WHERE fact.id = @memory_fact_id
                    AND fact.source_event_id = @source_event_id
                    AND chunk.id = @chunk_id
                    AND chunk.source_event_id = @source_event_id
                    AND chunk.search_vector IS NOT NULL
            );
            """,
            connection);
        command.Parameters.AddWithValue("memory_fact_id", payload.MemoryFactId);
        command.Parameters.AddWithValue("chunk_id", payload.ChunkId);
        command.Parameters.AddWithValue("source_event_id", payload.SourceEventId);

        var indexed = await command.ExecuteScalarAsync(cancellationToken);

        if (indexed is not true)
        {
            throw new InvalidOperationException("Memory index payload does not reference a searchable memory chunk.");
        }
    }

    private sealed record MemoryIndexPayload(
        Guid MemoryFactId,
        Guid ChunkId,
        Guid SourceEventId)
    {
        public static MemoryIndexPayload Parse(string payloadJson)
        {
            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                throw new InvalidOperationException("Memory index payload is required.");
            }

            try
            {
                var payload = JsonSerializer.Deserialize<MemoryIndexPayload>(
                    payloadJson,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web));

                if (payload is null
                    || payload.MemoryFactId == Guid.Empty
                    || payload.ChunkId == Guid.Empty
                    || payload.SourceEventId == Guid.Empty)
                {
                    throw new InvalidOperationException("Memory index payload is invalid.");
                }

                return payload;
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException("Memory index payload is not valid JSON.", exception);
            }
        }
    }
}
