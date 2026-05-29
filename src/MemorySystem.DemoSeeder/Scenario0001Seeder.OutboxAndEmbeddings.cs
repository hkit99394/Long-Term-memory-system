using MemorySystem.Application.MemoryEmbeddings;
using MemorySystem.Infrastructure.MemoryEmbeddings;
using MemorySystem.Infrastructure.Outbox;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;

internal static partial class Scenario0001Seeder
{
    private static async Task UpsertOutboxJobsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await UpsertOutboxJobAsync(
            connection,
            transaction,
            Scenario0001.UserPreferenceOutboxJobId,
            MemoryIndexOutboxJobContract.AggregateType,
            Scenario0001.UserPreferenceMemoryFactId,
            Scenario0001.UserPreferenceChunkId,
            Scenario0001.UserPreferenceEventId,
            cancellationToken);
        await UpsertOutboxJobAsync(
            connection,
            transaction,
            Scenario0001.ProjectDecisionOutboxJobId,
            MemoryIndexOutboxJobContract.AggregateType,
            Scenario0001.ProjectDecisionMemoryFactId,
            Scenario0001.ProjectDecisionChunkId,
            Scenario0001.ProjectDecisionEventId,
            cancellationToken);
        await UpsertOutboxJobAsync(
            connection,
            transaction,
            Scenario0001.SharedCtoPrincipleFactOutboxJobId,
            MemoryIndexOutboxJobContract.AggregateType,
            Scenario0001.SharedCtoPrincipleMemoryFactId,
            Scenario0001.SharedCtoPrincipleFactChunkId,
            Scenario0001.SharedCtoPrincipleEventId,
            cancellationToken);
        await UpsertOutboxJobAsync(
            connection,
            transaction,
            Scenario0001.SharedCtoPrincipleLensOutboxJobId,
            MemoryIndexOutboxJobContract.RoleMemoryLensAggregateType,
            Scenario0001.SharedCtoPrincipleRoleMemoryLensId,
            Scenario0001.SharedCtoPrincipleLensChunkId,
            Scenario0001.SharedCtoPrincipleEventId,
            cancellationToken);
        await UpsertOutboxJobAsync(
            connection,
            transaction,
            Scenario0001.ProjectCtoLensOutboxJobId,
            MemoryIndexOutboxJobContract.RoleMemoryLensAggregateType,
            Scenario0001.ProjectCtoLensRoleMemoryLensId,
            Scenario0001.ProjectCtoLensChunkId,
            Scenario0001.ProjectCtoLensEventId,
            cancellationToken);
        await UpsertOutboxJobAsync(
            connection,
            transaction,
            Scenario0001.ProjectBDecisionOutboxJobId,
            MemoryIndexOutboxJobContract.AggregateType,
            Scenario0001.ProjectBDecisionMemoryFactId,
            Scenario0001.ProjectBDecisionChunkId,
            Scenario0001.ProjectBDecisionEventId,
            cancellationToken);
    }

    private static async Task UpsertOutboxJobAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid id,
        string aggregateType,
        Guid aggregateId,
        Guid chunkId,
        Guid sourceEventId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO outbox_jobs (
                id,
                job_type,
                aggregate_type,
                aggregate_id,
                idempotency_key,
                payload,
                status,
                attempts,
                available_at,
                locked_until,
                locked_by,
                last_error
            )
            VALUES (
                @id,
                @job_type,
                @aggregate_type,
                @aggregate_id,
                @idempotency_key,
                @payload,
                'pending',
                0,
                now(),
                NULL,
                NULL,
                NULL
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
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("id", id);
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

    private static async Task<int> SeedEmbeddingsAsync(
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        var chunks = new List<(Guid ChunkId, string Input)>();

        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        await using (var command = new NpgsqlCommand(
            """
            SELECT id, concat_ws(' ', title, content) AS input
            FROM memory_chunks
            WHERE id = ANY(@chunk_ids)
            ORDER BY id;
            """,
            connection))
        {
            command.Parameters.Add("chunk_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid).Value = Scenario0001.ChunkIds;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                chunks.Add((reader.GetGuid(0), reader.GetString(1)));
            }
        }

        var provider = new DeterministicMemoryEmbeddingProvider(Options.Create(new MemoryEmbeddingOptions()));
        var store = new PostgresMemoryChunkEmbeddingStore(dataSource);

        foreach (var chunk in chunks)
        {
            var embedding = await provider.EmbedAsync(
                new MemoryEmbeddingRequest(chunk.Input),
                cancellationToken);
            await store.StoreAsync(
                new MemoryChunkEmbeddingWriteCommand(
                    chunk.ChunkId,
                    embedding.Model,
                    embedding.Dimension,
                    embedding.Values),
                cancellationToken);
        }

        return chunks.Count;
    }
}
