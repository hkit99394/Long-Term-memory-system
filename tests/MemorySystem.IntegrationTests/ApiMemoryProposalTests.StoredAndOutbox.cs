using System.Net;
using System.Text;
using System.Text.Json;
using MemorySystem.Infrastructure.MemoryEmbeddings;
using MemorySystem.Infrastructure.Outbox;
using MemorySystem.Worker;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace MemorySystem.IntegrationTests;

public sealed partial class ApiMemoryProposalTests
{
    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Post_memory_proposals_returns_stored_decision_for_supported_durable_candidate()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_proposal_stored_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var payload = await SendProposalAsync(client, "proposal-stored-key", CreateProposalBody());
            var memoryId = payload.GetProperty("memoryId").GetGuid();
            var idempotencyRecord = await ReadIdempotencyRecordAsync(databaseConnectionString);
            var memoryFact = await ReadMemoryFactAsync(databaseConnectionString, memoryId);
            var memoryChunk = await ReadMemoryChunkAsync(databaseConnectionString, memoryId);
            var outboxJob = await ReadOutboxJobAsync(databaseConnectionString, memoryId);

            Assert.Equal("stored", payload.GetProperty("decision").GetString());
            Assert.Equal("preference", payload.GetProperty("candidateKind").GetString());
            Assert.Equal(0.900m, payload.GetProperty("confidence").GetDecimal());
            Assert.Equal(SourceEventId, payload.GetProperty("sourceEventId").GetGuid());
            Assert.Equal("The proposal was stored as durable memory.", payload.GetProperty("reason").GetString());

            Assert.Equal("user", memoryFact.ScopeType);
            Assert.Equal(TestPrincipalId, memoryFact.ScopeId);
            Assert.Equal($"/user/{TestPrincipalId}/preferences", memoryFact.Namespace);
            Assert.Equal(Guid.Parse(TestPrincipalId), memoryFact.UserPrincipalId);
            Assert.Equal("preference", memoryFact.MemoryType);
            Assert.Equal("technical planning format", memoryFact.Subject);
            Assert.Equal("prefers", memoryFact.Predicate);
            Assert.Equal("concise decision logs", memoryFact.Object);
            Assert.Equal(0.900m, memoryFact.Confidence);
            Assert.Equal("user_scoped", memoryFact.TrustLevel);
            Assert.Equal(SourceEventId, memoryFact.SourceEventId);
            Assert.Equal(Guid.Parse(TestPrincipalId), memoryFact.ProposedByPrincipalId);

            Assert.Equal(memoryId, memoryChunk.SourceId);
            Assert.Equal(MemoryIndexOutboxJobContract.AggregateType, memoryChunk.SourceType);
            Assert.Equal(memoryFact.Namespace, memoryChunk.Namespace);
            Assert.Equal(memoryFact.ScopeType, memoryChunk.ScopeType);
            Assert.Equal(memoryFact.ScopeId, memoryChunk.ScopeId);
            Assert.Contains("technical planning format", memoryChunk.Content, StringComparison.Ordinal);
            Assert.StartsWith("sha256:", memoryChunk.ContentHash, StringComparison.Ordinal);
            Assert.Equal("user_scoped", memoryChunk.TrustLevel);
            Assert.Equal(SourceEventId, memoryChunk.SourceEventId);

            Assert.Equal(MemoryIndexOutboxJobContract.JobType, outboxJob.JobType);
            Assert.Equal(MemoryIndexOutboxJobContract.AggregateType, outboxJob.AggregateType);
            Assert.Equal(memoryId, outboxJob.AggregateId);
            Assert.Equal("pending", outboxJob.Status);

            Assert.Equal("POST /api/memory/proposals", idempotencyRecord.Endpoint);
            Assert.Equal("proposal-stored-key", idempotencyRecord.IdempotencyKey);
            Assert.Equal(200, idempotencyRecord.ResponseStatus);
            Assert.Equal("memory_fact", idempotencyRecord.ResourceType);
            Assert.Equal(memoryId, idempotencyRecord.ResourceId);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Memory_index_outbox_handler_completes_stored_proposal_job()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_proposal_index_worker_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var payload = await SendProposalAsync(client, "proposal-index-worker-key", CreateProposalBody());
            var memoryId = payload.GetProperty("memoryId").GetGuid();

            await using var dataSource = NpgsqlDataSource.Create(databaseConnectionString);
            var processor = new OutboxJobProcessor(
                new PostgresOutboxJobStore(dataSource),
                [CreateMemoryIndexHandler(dataSource)],
                Options.Create(new OutboxWorkerOptions
                {
                    WorkerId = "memory-index-test-worker",
                    BatchSize = 1,
                    MaxAttempts = 2,
                    LeaseDuration = TimeSpan.FromMinutes(1),
                    HandlerTimeout = TimeSpan.FromSeconds(10),
                    RetryDelay = TimeSpan.Zero
                }),
                NullLogger<OutboxJobProcessor>.Instance);

            Assert.Equal(1, await processor.ProcessAvailableAsync());

            var outboxJob = await ReadOutboxJobAsync(databaseConnectionString, memoryId);
            var embedding = await ReadMemoryEmbeddingAsync(databaseConnectionString, memoryId);

            Assert.Equal("completed", outboxJob.Status);
            Assert.Equal(TestEmbeddingModel, embedding.Model);
            Assert.Equal(TestEmbeddingDimension, embedding.Dimension);
            Assert.Equal(TestEmbeddingDimension, embedding.VectorDimensions);
            Assert.StartsWith("[", embedding.VectorLiteral, StringComparison.Ordinal);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Memory_index_outbox_handler_completes_stale_memory_job_without_embedding()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_proposal_index_stale_worker_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var payload = await SendProposalAsync(client, "proposal-index-stale-worker-key", CreateProposalBody());
            var memoryId = payload.GetProperty("memoryId").GetGuid();
            await SetMemoryFactStatusAsync(databaseConnectionString, memoryId, "deleted");

            await using var dataSource = NpgsqlDataSource.Create(databaseConnectionString);
            var processor = new OutboxJobProcessor(
                new PostgresOutboxJobStore(dataSource),
                [CreateMemoryIndexHandler(dataSource)],
                Options.Create(new OutboxWorkerOptions
                {
                    WorkerId = "memory-index-stale-test-worker",
                    BatchSize = 1,
                    MaxAttempts = 2,
                    LeaseDuration = TimeSpan.FromMinutes(1),
                    HandlerTimeout = TimeSpan.FromSeconds(10),
                    RetryDelay = TimeSpan.Zero
                }),
                NullLogger<OutboxJobProcessor>.Instance);

            Assert.Equal(1, await processor.ProcessAvailableAsync());

            var outboxJob = await ReadOutboxJobAsync(databaseConnectionString, memoryId);

            Assert.Equal("completed", outboxJob.Status);
            Assert.Equal(0, await CountMemoryEmbeddingsAsync(databaseConnectionString));
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Memory_index_outbox_handler_retries_job_with_missing_payload_chunk()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_proposal_index_missing_chunk_worker_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var payload = await SendProposalAsync(client, "proposal-index-missing-chunk-worker-key", CreateProposalBody());
            var memoryId = payload.GetProperty("memoryId").GetGuid();
            await ReplaceOutboxPayloadAsync(
                databaseConnectionString,
                memoryId,
                MemoryIndexOutboxJobContract.SerializePayload(memoryId, Guid.NewGuid(), SourceEventId));

            await using var dataSource = NpgsqlDataSource.Create(databaseConnectionString);
            var processor = new OutboxJobProcessor(
                new PostgresOutboxJobStore(dataSource),
                [CreateMemoryIndexHandler(dataSource)],
                Options.Create(new OutboxWorkerOptions
                {
                    WorkerId = "memory-index-missing-chunk-test-worker",
                    BatchSize = 1,
                    MaxAttempts = 2,
                    LeaseDuration = TimeSpan.FromMinutes(1),
                    HandlerTimeout = TimeSpan.FromSeconds(10),
                    RetryDelay = TimeSpan.Zero
                }),
                NullLogger<OutboxJobProcessor>.Instance);

            Assert.Equal(1, await processor.ProcessAvailableAsync());

            var outboxJob = await ReadOutboxJobAsync(databaseConnectionString, memoryId);

            Assert.Equal("pending", outboxJob.Status);
            Assert.Equal(0, await CountMemoryEmbeddingsAsync(databaseConnectionString));
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }
}
