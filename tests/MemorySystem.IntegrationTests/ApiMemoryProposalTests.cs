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

public sealed class ApiMemoryProposalTests
{
    private const string TestApiKey = "test-api-key";
    private const string TestPrincipalId = "11111111-1111-4111-8111-111111111111";
    private const string OtherPrincipalId = "22222222-2222-4222-8222-222222222222";
    private const string AgentPrincipalId = "99999999-9999-4999-8999-999999999999";
    private const string TestOrgId = "33333333-3333-4333-8333-333333333333";
    private const string TestProjectId = "44444444-4444-4444-8444-444444444444";
    private const string TestEmbeddingModel = "memory-test-deterministic-v1";
    private const int TestEmbeddingDimension = 12;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Guid SourceEventId = Guid.Parse("66666666-6666-4666-8666-666666666666");
    private static readonly Guid OtherSourceEventId = Guid.Parse("77777777-7777-4777-8777-777777777777");
    private static readonly Guid ProjectSourceEventId = Guid.Parse("88888888-8888-4888-8888-888888888888");
    private static readonly Guid LegacyAgentSourceEventId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

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

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Post_memory_proposals_derives_trust_level_from_source_event()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_proposal_source_trust_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString, sourceEventTrustLevel: "tool_output");

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var payload = await SendProposalAsync(
                client,
                "proposal-source-trust-key",
                CreateProposalBody(trustLevel: "user_scoped"));
            var memoryId = payload.GetProperty("memoryId").GetGuid();
            var memoryFact = await ReadMemoryFactAsync(databaseConnectionString, memoryId);
            var memoryChunk = await ReadMemoryChunkAsync(databaseConnectionString, memoryId);

            Assert.Equal("stored", payload.GetProperty("decision").GetString());
            Assert.Equal(0.800m, payload.GetProperty("confidence").GetDecimal());
            Assert.Equal(0.800m, memoryFact.Confidence);
            Assert.Equal("tool_output", memoryFact.TrustLevel);
            Assert.Equal("tool_output", memoryChunk.TrustLevel);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Post_memory_proposals_accepts_legacy_agent_source_event_actor()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_proposal_legacy_agent_event_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApiDatabaseTestSupport.ApplyMigrationsAsync(databaseConnectionString);
            await ApiDatabaseTestSupport.InsertPrincipalAsync(
                databaseConnectionString,
                Guid.Parse(AgentPrincipalId),
                principalType: "agent",
                displayName: "Legacy Agent Caller");
            await ApiDatabaseTestSupport.InsertLegacyAgentSourceEventAsync(
                databaseConnectionString,
                LegacyAgentSourceEventId,
                Guid.Parse(AgentPrincipalId),
                "agent",
                AgentPrincipalId);
            await ApiDatabaseTestSupport.InsertMemoryAccessGrantAsync(
                databaseConnectionString,
                $"/agent/{AgentPrincipalId}/private",
                "write",
                principalId: Guid.Parse(AgentPrincipalId));

            using var factory = MemorySystemApiTestFactory.Create(
                databaseConnectionString,
                TestApiKey,
                AgentPrincipalId,
                "Legacy Agent Caller");
            using var client = factory.CreateClient();

            var payload = await SendProposalAsync(
                client,
                "proposal-legacy-agent-source-key",
                CreateProposalBody(
                    sourceEventId: LegacyAgentSourceEventId,
                    memoryType: "agent_private",
                    scopeType: "agent",
                    scopeId: AgentPrincipalId,
                    namespaceValue: $"/agent/{AgentPrincipalId}/private",
                    subject: "indexing worker",
                    predicate: "prefers",
                    objectValue: "short leases"));
            var memoryId = payload.GetProperty("memoryId").GetGuid();
            var memoryFact = await ReadMemoryFactAsync(databaseConnectionString, memoryId);

            Assert.Equal("stored", payload.GetProperty("decision").GetString());
            Assert.Equal("agent_private", payload.GetProperty("candidateKind").GetString());
            Assert.Equal(0.850m, payload.GetProperty("confidence").GetDecimal());
            Assert.Equal("agent", memoryFact.ScopeType);
            Assert.Equal(AgentPrincipalId, memoryFact.ScopeId);
            Assert.Equal(Guid.Parse(AgentPrincipalId), memoryFact.AgentPrincipalId);
            Assert.Equal("agent_private", memoryFact.TrustLevel);
            Assert.Equal(LegacyAgentSourceEventId, memoryFact.SourceEventId);
            Assert.Equal(Guid.Parse(AgentPrincipalId), memoryFact.ProposedByPrincipalId);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Post_memory_proposals_routes_low_trust_evidence_to_review()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_proposal_low_trust_review_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString, sourceEventTrustLevel: "web_content");

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var payload = await SendProposalAsync(
                client,
                "proposal-low-trust-review-key",
                CreateProposalBody(confidence: 0.95m));

            Assert.Equal("review_required", payload.GetProperty("decision").GetString());
            Assert.Equal("preference", payload.GetProperty("candidateKind").GetString());
            Assert.Equal(0.650m, payload.GetProperty("confidence").GetDecimal());
            Assert.Contains("confidence score", payload.GetProperty("reason").GetString(), StringComparison.Ordinal);
            await AssertNoDurableProposalWritesAsync(databaseConnectionString);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Post_memory_proposals_uses_source_event_sensitivity_for_review()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_proposal_source_sensitivity_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString, sourceEventSensitivity: "secret");

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var payload = await SendProposalAsync(
                client,
                "proposal-source-sensitivity-key",
                CreateProposalBody(sensitivity: "none"));

            Assert.Equal("review_required", payload.GetProperty("decision").GetString());
            Assert.Equal("preference", payload.GetProperty("candidateKind").GetString());
            Assert.Contains("sensitive content", payload.GetProperty("reason").GetString(), StringComparison.Ordinal);
            await AssertNoDurableProposalWritesAsync(databaseConnectionString);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Post_memory_proposals_assigns_confidence_when_request_confidence_is_omitted()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_proposal_assigned_confidence_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var payload = await SendProposalAsync(
                client,
                "proposal-assigned-confidence-key",
                CreateProposalBody(includeConfidence: false));
            var memoryId = payload.GetProperty("memoryId").GetGuid();
            var memoryFact = await ReadMemoryFactAsync(databaseConnectionString, memoryId);

            Assert.Equal("stored", payload.GetProperty("decision").GetString());
            Assert.Equal(0.850m, payload.GetProperty("confidence").GetDecimal());
            Assert.Equal(0.850m, memoryFact.Confidence);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Post_memory_proposals_replays_original_decision_for_same_key_and_body()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_proposal_replay_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();
            var body = CreateProposalBody();

            var firstPayload = await SendProposalAsync(client, "proposal-replay-key", body);
            var secondPayload = await SendProposalAsync(client, "proposal-replay-key", body);

            Assert.Equal(firstPayload.GetProperty("decision").GetString(), secondPayload.GetProperty("decision").GetString());
            Assert.Equal(firstPayload.GetProperty("reason").GetString(), secondPayload.GetProperty("reason").GetString());
            Assert.Equal(firstPayload.GetProperty("memoryId").GetGuid(), secondPayload.GetProperty("memoryId").GetGuid());
            Assert.Equal(1, await CountIdempotencyRecordsAsync(databaseConnectionString));
            Assert.Equal(1, await CountMemoryFactsAsync(databaseConnectionString));
            Assert.Equal(1, await CountMemoryChunksAsync(databaseConnectionString));
            Assert.Equal(1, await CountOutboxJobsAsync(databaseConnectionString));
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Post_memory_proposals_keeps_one_off_instruction_session_only()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_proposal_one_off_instruction_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var payload = await SendProposalAsync(
                client,
                "proposal-one-off-instruction-key",
                CreateProposalBody(
                    subject: "task instruction",
                    predicate: "says",
                    objectValue: "For this answer, keep it short."));

            Assert.Equal("session_only", payload.GetProperty("decision").GetString());
            Assert.Equal("session_only_instruction", payload.GetProperty("candidateKind").GetString());
            Assert.Contains("one-off", payload.GetProperty("reason").GetString(), StringComparison.Ordinal);
            Assert.Null(payload.GetProperty("memoryId").GetString());
            Assert.Equal(SourceEventId, payload.GetProperty("sourceEventId").GetGuid());
            await AssertNoDurableProposalWritesAsync(databaseConnectionString);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Post_memory_proposals_reuses_existing_fact_for_duplicate_durable_candidate_with_new_key()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_proposal_duplicate_fact_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();
            var body = CreateProposalBody();

            var firstPayload = await SendProposalAsync(client, "proposal-duplicate-first-key", body);
            var firstMemoryId = firstPayload.GetProperty("memoryId").GetGuid();
            var secondPayload = await SendProposalAsync(client, "proposal-duplicate-second-key", body);

            Assert.Equal("stored", secondPayload.GetProperty("decision").GetString());
            Assert.Equal("preference", secondPayload.GetProperty("candidateKind").GetString());
            Assert.Equal(firstMemoryId, secondPayload.GetProperty("memoryId").GetGuid());
            Assert.Equal(SourceEventId, secondPayload.GetProperty("sourceEventId").GetGuid());
            Assert.Contains("existing durable memory", secondPayload.GetProperty("reason").GetString(), StringComparison.Ordinal);
            Assert.Equal(2, await CountIdempotencyRecordsAsync(databaseConnectionString));
            Assert.Equal(1, await CountMemoryFactsAsync(databaseConnectionString));
            Assert.Equal(1, await CountMemoryChunksAsync(databaseConnectionString));
            Assert.Equal(1, await CountOutboxJobsAsync(databaseConnectionString));
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Post_memory_proposals_reuses_existing_fact_for_normalized_duplicate_object()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_proposal_normalized_duplicate_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var firstPayload = await SendProposalAsync(
                client,
                "proposal-normalized-duplicate-first-key",
                CreateProposalBody(objectValue: "Concise Decision Logs"));
            var firstMemoryId = firstPayload.GetProperty("memoryId").GetGuid();
            var secondPayload = await SendProposalAsync(
                client,
                "proposal-normalized-duplicate-second-key",
                CreateProposalBody(objectValue: "concise decision logs"));

            Assert.Equal("stored", secondPayload.GetProperty("decision").GetString());
            Assert.Equal(firstMemoryId, secondPayload.GetProperty("memoryId").GetGuid());
            Assert.Contains("existing durable memory", secondPayload.GetProperty("reason").GetString(), StringComparison.Ordinal);
            Assert.Equal(2, await CountIdempotencyRecordsAsync(databaseConnectionString));
            Assert.Equal(1, await CountMemoryFactsAsync(databaseConnectionString));
            Assert.Equal(1, await CountMemoryChunksAsync(databaseConnectionString));
            Assert.Equal(1, await CountOutboxJobsAsync(databaseConnectionString));
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Post_memory_proposals_routes_similar_active_memory_to_review()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_proposal_similar_fact_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var firstPayload = await SendProposalAsync(
                client,
                "proposal-similar-first-key",
                CreateProposalBody(objectValue: "concise decision logs"));
            var secondPayload = await SendProposalAsync(
                client,
                "proposal-similar-second-key",
                CreateProposalBody(objectValue: "verbose decision logs with full transcripts"));

            Assert.Equal("stored", firstPayload.GetProperty("decision").GetString());
            Assert.Equal("review_required", secondPayload.GetProperty("decision").GetString());
            Assert.Equal("preference", secondPayload.GetProperty("candidateKind").GetString());
            Assert.Null(secondPayload.GetProperty("memoryId").GetString());
            Assert.Contains("similar active memory", secondPayload.GetProperty("reason").GetString(), StringComparison.Ordinal);
            Assert.Equal(2, await CountIdempotencyRecordsAsync(databaseConnectionString));
            Assert.Equal(1, await CountMemoryFactsAsync(databaseConnectionString));
            Assert.Equal(1, await CountMemoryChunksAsync(databaseConnectionString));
            Assert.Equal(1, await CountOutboxJobsAsync(databaseConnectionString));
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Post_memory_proposals_routes_conflicting_active_memory_to_review()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_proposal_conflicting_fact_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var firstPayload = await SendProposalAsync(
                client,
                "proposal-conflict-first-key",
                CreateProposalBody(
                    subject: "automatic backups",
                    predicate: "is",
                    objectValue: "enabled"));
            var secondPayload = await SendProposalAsync(
                client,
                "proposal-conflict-second-key",
                CreateProposalBody(
                    subject: "automatic backups",
                    predicate: "is",
                    objectValue: "disabled"));

            Assert.Equal("stored", firstPayload.GetProperty("decision").GetString());
            Assert.Equal("review_required", secondPayload.GetProperty("decision").GetString());
            Assert.Equal("preference", secondPayload.GetProperty("candidateKind").GetString());
            Assert.Null(secondPayload.GetProperty("memoryId").GetString());
            Assert.Contains("conflicting active memory", secondPayload.GetProperty("reason").GetString(), StringComparison.Ordinal);
            Assert.Equal(2, await CountIdempotencyRecordsAsync(databaseConnectionString));
            Assert.Equal(1, await CountMemoryFactsAsync(databaseConnectionString));
            Assert.Equal(1, await CountMemoryChunksAsync(databaseConnectionString));
            Assert.Equal(1, await CountOutboxJobsAsync(databaseConnectionString));
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Post_memory_proposals_allows_only_one_concurrent_active_subject_predicate()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_proposal_concurrent_subject_predicate_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();
            var firstTask = SendProposalAsync(
                client,
                "proposal-concurrent-first-key",
                CreateProposalBody(
                    subject: "parallel retention policy",
                    predicate: "prefers",
                    objectValue: "short retention"));
            var secondTask = SendProposalAsync(
                client,
                "proposal-concurrent-second-key",
                CreateProposalBody(
                    subject: "parallel retention policy",
                    predicate: "prefers",
                    objectValue: "long retention"));

            var payloads = await Task.WhenAll(firstTask, secondTask);
            var decisions = payloads
                .Select(payload => payload.GetProperty("decision").GetString())
                .ToArray();

            Assert.Contains("stored", decisions);
            Assert.Contains("review_required", decisions);
            Assert.Equal(2, await CountIdempotencyRecordsAsync(databaseConnectionString));
            Assert.Equal(1, await CountMemoryFactsAsync(databaseConnectionString));
            Assert.Equal(1, await CountMemoryChunksAsync(databaseConnectionString));
            Assert.Equal(1, await CountOutboxJobsAsync(databaseConnectionString));
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Post_memory_proposals_detects_conflict_beyond_subject_search_page()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_proposal_conflict_after_decoys_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            await SendProposalAsync(
                client,
                "proposal-conflict-page-original-key",
                CreateProposalBody(
                    subject: "automatic backups",
                    predicate: "is",
                    objectValue: "enabled"));

            for (var index = 0; index < 51; index++)
            {
                await SendProposalAsync(
                    client,
                    $"proposal-conflict-page-decoy-{index}",
                    CreateProposalBody(
                        subject: "automatic backups",
                        predicate: $"decoy-predicate-{index}",
                        objectValue: $"decoy value {index}"));
            }

            var payload = await SendProposalAsync(
                client,
                "proposal-conflict-page-new-key",
                CreateProposalBody(
                    subject: "automatic backups",
                    predicate: "is",
                    objectValue: "disabled"));

            Assert.Equal("review_required", payload.GetProperty("decision").GetString());
            Assert.Contains("conflicting active memory", payload.GetProperty("reason").GetString(), StringComparison.Ordinal);
            Assert.Equal(53, await CountIdempotencyRecordsAsync(databaseConnectionString));
            Assert.Equal(52, await CountMemoryFactsAsync(databaseConnectionString));
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Post_memory_proposals_stores_project_proposal_with_membership_and_grant()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_proposal_project_access_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);
            await PrepareProjectScopeAsync(databaseConnectionString, includeMembershipAndGrant: true);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var payload = await SendProposalAsync(
                client,
                "proposal-project-access-key",
                CreateProjectProposalBody());
            var memoryId = payload.GetProperty("memoryId").GetGuid();
            var memoryFact = await ReadMemoryFactAsync(databaseConnectionString, memoryId);

            Assert.Equal("stored", payload.GetProperty("decision").GetString());
            Assert.Equal("decision", payload.GetProperty("candidateKind").GetString());
            Assert.Equal("project", memoryFact.ScopeType);
            Assert.Equal(TestProjectId, memoryFact.ScopeId);
            Assert.Equal($"/project/{TestProjectId}/decisions", memoryFact.Namespace);
            Assert.Equal(Guid.Parse(TestOrgId), memoryFact.OrgId);
            Assert.Equal(Guid.Parse(TestProjectId), memoryFact.ProjectId);
            Assert.Null(memoryFact.UserPrincipalId);
            Assert.Equal("decision", memoryFact.MemoryType);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Post_memory_proposals_stores_project_role_lens_and_worker_indexes_chunk()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_proposal_project_role_lens_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);
            await PrepareProjectScopeAsync(databaseConnectionString, includeMembershipAndGrant: true);
            await ApiDatabaseTestSupport.InsertMemoryAccessGrantAsync(
                databaseConnectionString,
                $"/project/{TestProjectId}/role/cto/lens",
                "write",
                roleId: "cto");

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var basePayload = await SendProposalAsync(
                client,
                "proposal-role-lens-base-key",
                CreateProjectProposalBody());
            var baseMemoryFactId = basePayload.GetProperty("memoryId").GetGuid();

            var lensPayload = await SendProposalAsync(
                client,
                "proposal-role-lens-key",
                CreateProjectRoleLensProposalBody(baseMemoryFactId));
            var roleMemoryLensId = lensPayload.GetProperty("memoryId").GetGuid();
            var roleMemoryLens = await ReadRoleMemoryLensAsync(databaseConnectionString, roleMemoryLensId);
            var memoryChunk = await ReadMemoryChunkAsync(
                databaseConnectionString,
                roleMemoryLensId,
                MemoryIndexOutboxJobContract.RoleMemoryLensAggregateType);
            var outboxJob = await ReadOutboxJobAsync(
                databaseConnectionString,
                roleMemoryLensId,
                MemoryIndexOutboxJobContract.RoleMemoryLensAggregateType);
            var idempotencyRecord = await ReadIdempotencyRecordByKeyAsync(
                databaseConnectionString,
                "proposal-role-lens-key");

            Assert.Equal("stored", lensPayload.GetProperty("decision").GetString());
            Assert.Equal("role_lens", lensPayload.GetProperty("candidateKind").GetString());
            Assert.Equal(ProjectSourceEventId, lensPayload.GetProperty("sourceEventId").GetGuid());
            Assert.Equal("The proposal was stored as a role memory lens.", lensPayload.GetProperty("reason").GetString());

            Assert.Equal("cto", roleMemoryLens.RoleId);
            Assert.Equal("project", roleMemoryLens.ScopeType);
            Assert.Equal(TestProjectId, roleMemoryLens.ScopeId);
            Assert.Equal(Guid.Parse(TestOrgId), roleMemoryLens.OrgId);
            Assert.Equal(Guid.Parse(TestProjectId), roleMemoryLens.ProjectId);
            Assert.Equal(baseMemoryFactId, roleMemoryLens.BaseMemoryFactId);
            Assert.Equal("prioritize reversible rollout checkpoints", roleMemoryLens.Interpretation);
            Assert.Equal("active", roleMemoryLens.Status);
            Assert.Equal(ProjectSourceEventId, roleMemoryLens.SourceEventId);
            Assert.Equal(Guid.Parse(TestPrincipalId), roleMemoryLens.ProposedByPrincipalId);

            Assert.Equal(MemoryIndexOutboxJobContract.RoleMemoryLensAggregateType, memoryChunk.SourceType);
            Assert.Equal(roleMemoryLensId, memoryChunk.SourceId);
            Assert.Equal($"/project/{TestProjectId}/role/cto/lens", memoryChunk.Namespace);
            Assert.Equal("project", memoryChunk.ScopeType);
            Assert.Equal(TestProjectId, memoryChunk.ScopeId);
            Assert.Contains("prioritize reversible rollout checkpoints", memoryChunk.Content, StringComparison.Ordinal);
            Assert.Equal(ProjectSourceEventId, memoryChunk.SourceEventId);

            Assert.Equal(MemoryIndexOutboxJobContract.JobType, outboxJob.JobType);
            Assert.Equal(MemoryIndexOutboxJobContract.RoleMemoryLensAggregateType, outboxJob.AggregateType);
            Assert.Equal(roleMemoryLensId, outboxJob.AggregateId);
            Assert.Equal("pending", outboxJob.Status);

            Assert.Equal("role_memory_lens", idempotencyRecord.ResourceType);
            Assert.Equal(roleMemoryLensId, idempotencyRecord.ResourceId);

            var duplicateLensPayload = await SendProposalAsync(
                client,
                "proposal-role-lens-duplicate-key",
                CreateProjectRoleLensProposalBody(baseMemoryFactId));

            Assert.Equal("stored", duplicateLensPayload.GetProperty("decision").GetString());
            Assert.Equal(roleMemoryLensId, duplicateLensPayload.GetProperty("memoryId").GetGuid());
            Assert.Contains("existing durable role memory lens", duplicateLensPayload.GetProperty("reason").GetString(), StringComparison.Ordinal);
            Assert.Equal(1, await CountRoleMemoryLensesAsync(databaseConnectionString));
            Assert.Equal(2, await CountMemoryChunksAsync(databaseConnectionString));
            Assert.Equal(2, await CountOutboxJobsAsync(databaseConnectionString));

            await using var dataSource = NpgsqlDataSource.Create(databaseConnectionString);
            var processor = new OutboxJobProcessor(
                new PostgresOutboxJobStore(dataSource),
                [CreateMemoryIndexHandler(dataSource)],
                Options.Create(new OutboxWorkerOptions
                {
                    WorkerId = "role-lens-index-test-worker",
                    BatchSize = 2,
                    MaxAttempts = 2,
                    LeaseDuration = TimeSpan.FromMinutes(1),
                    HandlerTimeout = TimeSpan.FromSeconds(10),
                    RetryDelay = TimeSpan.Zero
                }),
                NullLogger<OutboxJobProcessor>.Instance);

            Assert.Equal(2, await processor.ProcessAvailableAsync());
            outboxJob = await ReadOutboxJobAsync(
                databaseConnectionString,
                roleMemoryLensId,
                MemoryIndexOutboxJobContract.RoleMemoryLensAggregateType);

            Assert.Equal("completed", outboxJob.Status);
            Assert.Equal(1, await CountRoleMemoryLensesAsync(databaseConnectionString));
            Assert.Equal(2, await CountMemoryEmbeddingsAsync(databaseConnectionString));
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Post_memory_proposals_rejects_role_lens_namespace_that_does_not_match_role_id()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_proposal_role_lens_namespace_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);
            await PrepareProjectScopeAsync(databaseConnectionString, includeMembershipAndGrant: true);
            await ApiDatabaseTestSupport.InsertMemoryAccessGrantAsync(
                databaseConnectionString,
                $"/project/{TestProjectId}/role/cto/lens",
                "write",
                roleId: "cto");

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var basePayload = await SendProposalAsync(
                client,
                "proposal-role-lens-namespace-base-key",
                CreateProjectProposalBody());
            var baseMemoryFactId = basePayload.GetProperty("memoryId").GetGuid();

            var (statusCode, payload) = await SendProposalResponseAsync(
                client,
                "proposal-role-lens-namespace-key",
                CreateProjectRoleLensProposalBody(baseMemoryFactId, roleId: "cfo"));
            var idempotencyRecord = await ReadIdempotencyRecordByKeyAsync(
                databaseConnectionString,
                "proposal-role-lens-namespace-key");

            Assert.Equal(HttpStatusCode.BadRequest, statusCode);
            Assert.Equal("Memory proposal is invalid.", payload.GetProperty("title").GetString());
            Assert.Contains("roleId 'cfo'", payload.GetProperty("detail").GetString(), StringComparison.Ordinal);
            Assert.Equal("completed", idempotencyRecord.Status);
            Assert.Equal(400, idempotencyRecord.ResponseStatus);
            Assert.Equal(0, await CountRoleMemoryLensesAsync(databaseConnectionString));
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Post_memory_proposals_returns_bad_request_for_unknown_role_lens_base_fact()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_proposal_unknown_role_lens_base_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);
            await PrepareProjectScopeAsync(databaseConnectionString, includeMembershipAndGrant: true);
            await ApiDatabaseTestSupport.InsertMemoryAccessGrantAsync(
                databaseConnectionString,
                $"/project/{TestProjectId}/role/cto/lens",
                "write",
                roleId: "cto");

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var (statusCode, payload) = await SendProposalResponseAsync(
                client,
                "proposal-unknown-role-lens-base-key",
                CreateProjectRoleLensProposalBody(Guid.NewGuid()));
            var idempotencyRecord = await ReadIdempotencyRecordAsync(databaseConnectionString);

            Assert.Equal(HttpStatusCode.BadRequest, statusCode);
            Assert.Equal("Memory proposal is invalid.", payload.GetProperty("title").GetString());
            Assert.Contains("baseMemoryFactId", payload.GetProperty("detail").GetString(), StringComparison.Ordinal);
            Assert.Equal("completed", idempotencyRecord.Status);
            Assert.Equal(400, idempotencyRecord.ResponseStatus);
            Assert.Equal(0, await CountRoleMemoryLensesAsync(databaseConnectionString));
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Post_memory_proposals_forbids_project_proposal_without_membership_or_grant()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_proposal_project_forbidden_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);
            await PrepareProjectScopeAsync(databaseConnectionString, includeMembershipAndGrant: false);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var (statusCode, payload) = await SendProposalResponseAsync(
                client,
                "proposal-project-forbidden-key",
                CreateProjectProposalBody());
            var idempotencyRecord = await ReadIdempotencyRecordAsync(databaseConnectionString);

            Assert.Equal(HttpStatusCode.Forbidden, statusCode);
            Assert.Equal("Memory proposal is forbidden.", payload.GetProperty("title").GetString());
            Assert.Contains("membership access to project", payload.GetProperty("detail").GetString(), StringComparison.Ordinal);
            Assert.Equal("completed", idempotencyRecord.Status);
            Assert.Equal(403, idempotencyRecord.ResponseStatus);
            await AssertNoDurableProposalWritesAsync(databaseConnectionString);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Post_memory_proposals_forbids_unauthorized_project_review_candidate_before_broker_decision()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_proposal_project_review_forbidden_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);
            await PrepareProjectScopeAsync(databaseConnectionString, includeMembershipAndGrant: false);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var (statusCode, payload) = await SendProposalResponseAsync(
                client,
                "proposal-project-review-forbidden-key",
                CreateProjectProposalBody(confidence: 0.40m));
            var idempotencyRecord = await ReadIdempotencyRecordAsync(databaseConnectionString);

            Assert.Equal(HttpStatusCode.Forbidden, statusCode);
            Assert.Equal("Memory proposal is forbidden.", payload.GetProperty("title").GetString());
            Assert.Contains("membership access to project", payload.GetProperty("detail").GetString(), StringComparison.Ordinal);
            Assert.Equal("completed", idempotencyRecord.Status);
            Assert.Equal(403, idempotencyRecord.ResponseStatus);
            await AssertNoDurableProposalWritesAsync(databaseConnectionString);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseTheory]
    [InlineData("review_required", 0.40, "preference", "user", "/user/11111111-1111-4111-8111-111111111111/preferences")]
    [InlineData("session_only", 0.95, "session_instruction", "session", "/session/session-1/instructions")]
    [Trait("Category", "Database")]
    public async Task Post_memory_proposals_returns_non_stored_decisions(
        string expectedDecision,
        decimal confidence,
        string memoryType,
        string scopeType,
        string namespaceValue)
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_proposal_{expectedDecision}_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();
            var scopeId = scopeType == "session" ? "session-1" : TestPrincipalId;
            var sourceEventId = SourceEventId;

            if (scopeType == "session")
            {
                sourceEventId = OtherSourceEventId;
                await ApiDatabaseTestSupport.InsertSourceEventAsync(
                    databaseConnectionString,
                    sourceEventId,
                    Guid.Parse(TestPrincipalId),
                    scopeType: "session",
                    scopeId: scopeId);
            }

            var body = CreateProposalBody(
                sourceEventId: sourceEventId,
                confidence: confidence,
                memoryType: memoryType,
                scopeType: scopeType,
                scopeId: scopeId,
                namespaceValue: namespaceValue);

            var payload = await SendProposalAsync(client, $"proposal-{expectedDecision}-key", body);

            Assert.Equal(expectedDecision, payload.GetProperty("decision").GetString());
            Assert.True(payload.TryGetProperty("candidateKind", out _));
            Assert.Null(payload.GetProperty("memoryId").GetString());
            Assert.Equal(sourceEventId, payload.GetProperty("sourceEventId").GetGuid());
            await AssertNoDurableProposalWritesAsync(databaseConnectionString);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Post_memory_proposals_rejects_omitted_source_event()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_proposal_no_source_event_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();
            var body = CreateProposalBody(includeSourceEventId: false);

            var payload = await SendProposalAsync(client, "proposal-no-source-event-key", body);

            Assert.Equal("rejected", payload.GetProperty("decision").GetString());
            Assert.Contains("sourceEventId", payload.GetProperty("reason").GetString(), StringComparison.Ordinal);
            Assert.Null(payload.GetProperty("sourceEventId").GetString());
            await AssertNoDurableProposalWritesAsync(databaseConnectionString);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Post_memory_proposals_rejects_unknown_source_event()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_proposal_unknown_source_event_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();
            var body = CreateProposalBody(sourceEventId: Guid.NewGuid());

            var payload = await SendProposalAsync(client, "proposal-unknown-source-event-key", body);

            Assert.Equal("rejected", payload.GetProperty("decision").GetString());
            Assert.Contains("source event", payload.GetProperty("reason").GetString(), StringComparison.OrdinalIgnoreCase);
            await AssertNoDurableProposalWritesAsync(databaseConnectionString);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseTheory]
    [InlineData("erasure_requested", "none")]
    [InlineData("standard", "redacted")]
    [Trait("Category", "Database")]
    public async Task Post_memory_proposals_rejects_erased_or_redacted_source_event(
        string sourceEventRetentionClass,
        string sourceEventRedactionStatus)
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_proposal_inactive_source_event_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(
                databaseConnectionString,
                sourceEventRetentionClass: sourceEventRetentionClass,
                sourceEventRedactionStatus: sourceEventRedactionStatus);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var payload = await SendProposalAsync(
                client,
                $"proposal-inactive-source-event-{sourceEventRetentionClass}-{sourceEventRedactionStatus}",
                CreateProposalBody());

            Assert.Equal("rejected", payload.GetProperty("decision").GetString());
            Assert.Contains("source event", payload.GetProperty("reason").GetString(), StringComparison.OrdinalIgnoreCase);
            await AssertNoDurableProposalWritesAsync(databaseConnectionString);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Post_memory_proposals_rejects_source_event_from_other_principal_scope()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_proposal_cross_source_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);
            await ApiDatabaseTestSupport.InsertPrincipalAsync(databaseConnectionString, Guid.Parse(OtherPrincipalId));
            await ApiDatabaseTestSupport.InsertSourceEventAsync(
                databaseConnectionString,
                OtherSourceEventId,
                Guid.Parse(OtherPrincipalId),
                scopeType: "user",
                scopeId: OtherPrincipalId);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();
            var body = CreateProposalBody(sourceEventId: OtherSourceEventId);

            var payload = await SendProposalAsync(client, "proposal-cross-source-event-key", body);

            Assert.Equal("rejected", payload.GetProperty("decision").GetString());
            Assert.Contains("source event", payload.GetProperty("reason").GetString(), StringComparison.OrdinalIgnoreCase);
            await AssertNoDurableProposalWritesAsync(databaseConnectionString);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Post_memory_proposals_rejects_user_scope_that_does_not_match_authenticated_principal()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_proposal_cross_user_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();
            var body = CreateProposalBody(
                scopeId: OtherPrincipalId,
                namespaceValue: $"/user/{OtherPrincipalId}/preferences");

            var (statusCode, payload) = await SendProposalResponseAsync(client, "proposal-cross-user-key", body);
            var idempotencyRecord = await ReadIdempotencyRecordAsync(databaseConnectionString);

            Assert.Equal(HttpStatusCode.BadRequest, statusCode);
            Assert.Equal("Memory proposal is invalid.", payload.GetProperty("title").GetString());
            Assert.Contains("authenticated principal", payload.GetProperty("detail").GetString(), StringComparison.OrdinalIgnoreCase);
            Assert.Equal("completed", idempotencyRecord.Status);
            Assert.Equal(400, idempotencyRecord.ResponseStatus);
            await AssertNoDurableProposalWritesAsync(databaseConnectionString);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseTheory]
    [InlineData("not-a-guid", "/user/not-a-guid/preferences", "private", "user_scoped", "scopeId must be a valid GUID")]
    [InlineData("11111111-1111-4111-8111-111111111111", "/user/22222222-2222-4222-8222-222222222222/preferences", "private", "user_scoped", "namespace must start")]
    [InlineData("11111111-1111-4111-8111-111111111111", "/user/11111111-1111-4111-8111-111111111111/preferences", "public", "user_scoped", "visibility is not supported")]
    [InlineData("11111111-1111-4111-8111-111111111111", "/user/11111111-1111-4111-8111-111111111111/preferences", "private", "totally_trusted", "trustLevel is not supported")]
    [InlineData("11111111-1111-4111-8111-111111111111", "/user/11111111-1111-4111-8111-111111111111/preferences", "private", "system_trusted", "trusted internal source")]
    [Trait("Category", "Database")]
    public async Task Post_memory_proposals_returns_bad_request_for_malformed_stored_candidates(
        string scopeId,
        string namespaceValue,
        string visibility,
        string trustLevel,
        string expectedDetail)
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_proposal_bad_request_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();
            var body = CreateProposalBody(
                scopeId: scopeId,
                namespaceValue: namespaceValue,
                visibility: visibility,
                trustLevel: trustLevel);

            var (statusCode, payload) = await SendProposalResponseAsync(client, $"proposal-bad-request-{Guid.NewGuid():N}", body);
            var idempotencyRecord = await ReadIdempotencyRecordAsync(databaseConnectionString);

            Assert.Equal(HttpStatusCode.BadRequest, statusCode);
            Assert.Equal("Memory proposal is invalid.", payload.GetProperty("title").GetString());
            Assert.Contains(expectedDetail, payload.GetProperty("detail").GetString(), StringComparison.Ordinal);
            Assert.Equal("completed", idempotencyRecord.Status);
            Assert.Equal(400, idempotencyRecord.ResponseStatus);
            await AssertNoDurableProposalWritesAsync(databaseConnectionString);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Post_memory_proposals_returns_bad_request_for_non_json_content_type()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_proposal_non_json_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var (statusCode, payload) = await SendProposalResponseAsync(
                client,
                "proposal-non-json-key",
                CreateProposalBody(),
                "text/plain");
            var idempotencyRecord = await ReadIdempotencyRecordAsync(databaseConnectionString);

            Assert.Equal(HttpStatusCode.BadRequest, statusCode);
            Assert.Equal("Memory proposal is invalid.", payload.GetProperty("title").GetString());
            Assert.Contains("Content-Type", payload.GetProperty("detail").GetString(), StringComparison.Ordinal);
            Assert.Equal("completed", idempotencyRecord.Status);
            Assert.Equal(400, idempotencyRecord.ResponseStatus);
            await AssertNoDurableProposalWritesAsync(databaseConnectionString);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    private static string CreateProposalBody(
        Guid? sourceEventId = null,
        decimal? confidence = 0.95m,
        string memoryType = "preference",
        string scopeType = "user",
        string? scopeId = null,
        string? namespaceValue = null,
        string visibility = "private",
        string trustLevel = "user_scoped",
        string sensitivity = "none",
        string subject = "technical planning format",
        string predicate = "prefers",
        string objectValue = "concise decision logs",
        bool includeSourceEventId = true,
        bool includeConfidence = true,
        string? roleId = null,
        Guid? baseMemoryFactId = null)
    {
        var resolvedScopeId = scopeId ?? TestPrincipalId;
        var resolvedNamespace = namespaceValue ?? $"/user/{TestPrincipalId}/preferences";
        var resolvedSourceEventId = sourceEventId ?? SourceEventId;

        var body = new Dictionary<string, object?>
        {
            ["memoryType"] = memoryType,
            ["scopeType"] = scopeType,
            ["scopeId"] = resolvedScopeId,
            ["namespace"] = resolvedNamespace,
            ["visibility"] = visibility,
            ["subject"] = subject,
            ["predicate"] = predicate,
            ["object"] = objectValue,
            ["trustLevel"] = trustLevel,
            ["sensitivity"] = sensitivity
        };

        if (includeConfidence)
        {
            body["confidence"] = confidence;
        }

        if (includeSourceEventId)
        {
            body["sourceEventId"] = resolvedSourceEventId;
        }

        if (!string.IsNullOrWhiteSpace(roleId))
        {
            body["roleId"] = roleId;
        }

        if (baseMemoryFactId.HasValue)
        {
            body["baseMemoryFactId"] = baseMemoryFactId.Value;
        }

        return JsonSerializer.Serialize(body, JsonOptions);
    }

    private static string CreateProjectProposalBody(decimal? confidence = 0.95m)
    {
        return CreateProposalBody(
            sourceEventId: ProjectSourceEventId,
            confidence: confidence,
            memoryType: "decision",
            scopeType: "project",
            scopeId: TestProjectId,
            namespaceValue: $"/project/{TestProjectId}/decisions",
            visibility: "project_shared");
    }

    private static string CreateProjectRoleLensProposalBody(
        Guid baseMemoryFactId,
        string roleId = "cto")
    {
        return CreateProposalBody(
            sourceEventId: ProjectSourceEventId,
            memoryType: "project_role_lens",
            scopeType: "project",
            scopeId: TestProjectId,
            namespaceValue: $"/project/{TestProjectId}/role/cto/lens",
            visibility: "project_shared",
            subject: "storage engine decision",
            predicate: "means_for_role",
            objectValue: "prioritize reversible rollout checkpoints",
            roleId: roleId,
            baseMemoryFactId: baseMemoryFactId);
    }

    private static async Task<JsonElement> SendProposalAsync(
        HttpClient client,
        string idempotencyKey,
        string body)
    {
        var (statusCode, payload) = await SendProposalResponseAsync(client, idempotencyKey, body);

        Assert.Equal(HttpStatusCode.OK, statusCode);

        return payload;
    }

    private static async Task<(HttpStatusCode StatusCode, JsonElement Payload)> SendProposalResponseAsync(
        HttpClient client,
        string idempotencyKey,
        string body,
        string mediaType = "application/json")
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/memory/proposals")
        {
            Content = new StringContent(body, Encoding.UTF8, mediaType)
        };
        request.Headers.Add("X-Api-Key", TestApiKey);
        request.Headers.Add("Idempotency-Key", idempotencyKey);

        using var response = await client.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(responseBody);
        return (response.StatusCode, document.RootElement.Clone());
    }

    private static WebApplicationFactory<Program> CreateFactory(string postgresConnectionString)
    {
        return MemorySystemApiTestFactory.Create(postgresConnectionString, TestApiKey, TestPrincipalId);
    }

    private static MemoryIndexOutboxJobHandler CreateMemoryIndexHandler(NpgsqlDataSource dataSource)
    {
        var embeddingOptions = Options.Create(new MemoryEmbeddingOptions
        {
            Model = TestEmbeddingModel,
            Dimension = TestEmbeddingDimension
        });

        return new MemoryIndexOutboxJobHandler(
            dataSource,
            new DeterministicMemoryEmbeddingProvider(embeddingOptions),
            new PostgresMemoryChunkEmbeddingStore(dataSource),
            NullLogger<MemoryIndexOutboxJobHandler>.Instance);
    }

    private static async Task PrepareDatabaseAsync(
        string connectionString,
        string sourceEventTrustLevel = "user_scoped",
        string sourceEventSensitivity = "none",
        string sourceEventRetentionClass = "standard",
        string sourceEventRedactionStatus = "none")
    {
        await ApiDatabaseTestSupport.ApplyMigrationsAsync(connectionString);
        await ApiDatabaseTestSupport.InsertPrincipalAsync(connectionString, Guid.Parse(TestPrincipalId));
        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            SourceEventId,
            Guid.Parse(TestPrincipalId),
            scopeType: "user",
            scopeId: TestPrincipalId,
            trustLevel: sourceEventTrustLevel,
            sensitivity: sourceEventSensitivity,
            retentionClass: sourceEventRetentionClass,
            redactionStatus: sourceEventRedactionStatus);
        await ApiDatabaseTestSupport.InsertMemoryAccessGrantAsync(
            connectionString,
            $"/user/{TestPrincipalId}/preferences",
            "write",
            principalId: Guid.Parse(TestPrincipalId));
    }

    private static async Task PrepareProjectScopeAsync(
        string connectionString,
        bool includeMembershipAndGrant)
    {
        await ApiDatabaseTestSupport.InsertOrganizationAndProjectAsync(
            connectionString,
            Guid.Parse(TestOrgId),
            Guid.Parse(TestProjectId));
        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            ProjectSourceEventId,
            Guid.Parse(TestPrincipalId),
            scopeType: "project",
            scopeId: TestProjectId,
            scopeOrgId: Guid.Parse(TestOrgId),
            scopeProjectId: Guid.Parse(TestProjectId));

        if (!includeMembershipAndGrant)
        {
            return;
        }

        await ApiDatabaseTestSupport.InsertProjectMembershipAsync(
            connectionString,
            Guid.Parse(TestProjectId),
            Guid.Parse(TestPrincipalId),
            "contributor");
        await ApiDatabaseTestSupport.InsertRoleAssignmentAsync(
            connectionString,
            Guid.Parse(TestPrincipalId),
            "cto",
            "project",
            Guid.Parse(TestProjectId));
        await ApiDatabaseTestSupport.InsertMemoryAccessGrantAsync(
            connectionString,
            $"/project/{TestProjectId}/decisions",
            "write",
            roleId: "cto");
    }

    private static async Task<int> CountIdempotencyRecordsAsync(string connectionString)
    {
        return await ApiDatabaseTestSupport.CountIdempotencyRecordsAsync(connectionString);
    }

    private static async Task<int> CountMemoryFactsAsync(string connectionString)
    {
        return await CountRowsAsync(connectionString, "memory_facts");
    }

    private static async Task<int> CountMemoryChunksAsync(string connectionString)
    {
        return await CountRowsAsync(connectionString, "memory_chunks");
    }

    private static async Task<int> CountOutboxJobsAsync(string connectionString)
    {
        return await CountRowsAsync(connectionString, "outbox_jobs");
    }

    private static async Task<int> CountRoleMemoryLensesAsync(string connectionString)
    {
        return await CountRowsAsync(connectionString, "role_memory_lenses");
    }

    private static async Task<int> CountMemoryEmbeddingsAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT count(*)::int
            FROM memory_embeddings
            WHERE embedding_model = @embedding_model;
            """,
            connection);
        command.Parameters.AddWithValue("embedding_model", TestEmbeddingModel);

        return (int)(await command.ExecuteScalarAsync() ?? 0);
    }

    private static async Task SetMemoryFactStatusAsync(
        string connectionString,
        Guid memoryId,
        string status)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            UPDATE memory_facts
            SET status = @status
            WHERE id = @memory_id;
            """,
            connection);
        command.Parameters.AddWithValue("memory_id", memoryId);
        command.Parameters.AddWithValue("status", status);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task ReplaceOutboxPayloadAsync(
        string connectionString,
        Guid aggregateId,
        string payload)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            UPDATE outbox_jobs
            SET payload = @payload
            WHERE aggregate_id = @aggregate_id;
            """,
            connection);
        command.Parameters.Add("payload", NpgsqlTypes.NpgsqlDbType.Jsonb).Value = payload;
        command.Parameters.AddWithValue("aggregate_id", aggregateId);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task AssertNoDurableProposalWritesAsync(string connectionString)
    {
        Assert.Equal(0, await CountMemoryFactsAsync(connectionString));
        Assert.Equal(0, await CountMemoryChunksAsync(connectionString));
        Assert.Equal(0, await CountOutboxJobsAsync(connectionString));
    }

    private static async Task<int> CountRowsAsync(string connectionString, string tableName)
    {
        return await ApiDatabaseTestSupport.CountRowsAsync(connectionString, tableName);
    }

    private static async Task<MemoryFactState> ReadMemoryFactAsync(string connectionString, Guid memoryId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT
                scope_type,
                scope_id,
                namespace,
                user_principal_id,
                project_id,
                org_id,
                role_id,
                agent_principal_id,
                memory_type,
                subject,
                predicate,
                object,
                confidence,
                trust_level,
                source_event_id,
                proposed_by_principal_id
            FROM memory_facts
            WHERE id = @memory_id;
            """,
            connection);
        command.Parameters.AddWithValue("memory_id", memoryId);

        await using var reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());

        return new MemoryFactState(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetGuid(3),
            reader.IsDBNull(4) ? null : reader.GetGuid(4),
            reader.IsDBNull(5) ? null : reader.GetGuid(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetGuid(7),
            reader.GetString(8),
            reader.GetString(9),
            reader.GetString(10),
            reader.GetString(11),
            reader.GetDecimal(12),
            reader.GetString(13),
            reader.GetGuid(14),
            reader.GetGuid(15));
    }

    private static async Task<MemoryChunkState> ReadMemoryChunkAsync(
        string connectionString,
        Guid sourceId,
        string sourceType = MemoryIndexOutboxJobContract.AggregateType)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT
                source_type,
                source_id,
                namespace,
                scope_type,
                scope_id,
                content,
                content_hash,
                trust_level,
                source_event_id
            FROM memory_chunks
            WHERE source_type = @source_type
                AND source_id = @source_id;
            """,
            connection);
        command.Parameters.AddWithValue("source_type", sourceType);
        command.Parameters.AddWithValue("source_id", sourceId);

        await using var reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());

        return new MemoryChunkState(
            reader.GetString(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetString(6),
            reader.GetString(7),
            reader.GetGuid(8));
    }

    private static async Task<MemoryEmbeddingState> ReadMemoryEmbeddingAsync(
        string connectionString,
        Guid sourceId,
        string sourceType = MemoryIndexOutboxJobContract.AggregateType)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT
                embedding.embedding_model,
                embedding.embedding_dimension,
                vector_dims(embedding.embedding),
                embedding.embedding::text
            FROM memory_chunks AS chunk
            JOIN memory_embeddings AS embedding ON embedding.chunk_id = chunk.id
            WHERE chunk.source_type = @source_type
                AND chunk.source_id = @source_id
                AND embedding.embedding_model = @embedding_model;
            """,
            connection);
        command.Parameters.AddWithValue("source_type", sourceType);
        command.Parameters.AddWithValue("source_id", sourceId);
        command.Parameters.AddWithValue("embedding_model", TestEmbeddingModel);

        await using var reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());

        return new MemoryEmbeddingState(
            reader.GetString(0),
            reader.GetInt32(1),
            reader.GetInt32(2),
            reader.GetString(3));
    }

    private static async Task<RoleMemoryLensState> ReadRoleMemoryLensAsync(
        string connectionString,
        Guid roleMemoryLensId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT
                role_id,
                scope_type,
                scope_id,
                org_id,
                project_id,
                base_memory_fact_id,
                interpretation,
                confidence,
                status,
                source_event_id,
                proposed_by_principal_id
            FROM role_memory_lenses
            WHERE id = @role_memory_lens_id;
            """,
            connection);
        command.Parameters.AddWithValue("role_memory_lens_id", roleMemoryLensId);

        await using var reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());

        return new RoleMemoryLensState(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetGuid(3),
            reader.IsDBNull(4) ? null : reader.GetGuid(4),
            reader.GetGuid(5),
            reader.GetString(6),
            reader.GetDecimal(7),
            reader.GetString(8),
            reader.GetGuid(9),
            reader.GetGuid(10));
    }

    private static async Task<OutboxJobState> ReadOutboxJobAsync(
        string connectionString,
        Guid aggregateId,
        string aggregateType = MemoryIndexOutboxJobContract.AggregateType)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT job_type, aggregate_type, aggregate_id, status
            FROM outbox_jobs
            WHERE aggregate_type = @aggregate_type
                AND aggregate_id = @aggregate_id;
            """,
            connection);
        command.Parameters.AddWithValue("aggregate_type", aggregateType);
        command.Parameters.AddWithValue("aggregate_id", aggregateId);

        await using var reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());

        return new OutboxJobState(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetGuid(2),
            reader.GetString(3));
    }

    private static async Task<IdempotencyRecordState> ReadIdempotencyRecordAsync(string connectionString)
    {
        var record = await ApiDatabaseTestSupport.ReadSingleIdempotencySummaryAsync(connectionString);

        return new IdempotencyRecordState(
            record.Endpoint,
            record.IdempotencyKey,
            record.Status,
            record.ResponseStatus,
            record.ResourceType,
            record.ResourceId);
    }

    private static async Task<IdempotencyRecordState> ReadIdempotencyRecordByKeyAsync(
        string connectionString,
        string idempotencyKey)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT endpoint, idempotency_key, status, response_status, resource_type, resource_id
            FROM api_idempotency_keys
            WHERE idempotency_key = @idempotency_key;
            """,
            connection);
        command.Parameters.AddWithValue("idempotency_key", idempotencyKey);

        await using var reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());

        return new IdempotencyRecordState(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetInt32(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetGuid(5));
    }

    private sealed record IdempotencyRecordState(
        string Endpoint,
        string IdempotencyKey,
        string Status,
        int ResponseStatus,
        string? ResourceType,
        Guid? ResourceId);

    private sealed record MemoryFactState(
        string ScopeType,
        string ScopeId,
        string Namespace,
        Guid? UserPrincipalId,
        Guid? ProjectId,
        Guid? OrgId,
        string? RoleId,
        Guid? AgentPrincipalId,
        string MemoryType,
        string Subject,
        string Predicate,
        string Object,
        decimal Confidence,
        string TrustLevel,
        Guid SourceEventId,
        Guid ProposedByPrincipalId);

    private sealed record MemoryChunkState(
        string SourceType,
        Guid SourceId,
        string Namespace,
        string ScopeType,
        string ScopeId,
        string Content,
        string ContentHash,
        string TrustLevel,
        Guid SourceEventId);

    private sealed record MemoryEmbeddingState(
        string Model,
        int Dimension,
        int VectorDimensions,
        string VectorLiteral);

    private sealed record RoleMemoryLensState(
        string RoleId,
        string ScopeType,
        string ScopeId,
        Guid? OrgId,
        Guid? ProjectId,
        Guid BaseMemoryFactId,
        string Interpretation,
        decimal Confidence,
        string Status,
        Guid SourceEventId,
        Guid ProposedByPrincipalId);

    private sealed record OutboxJobState(
        string JobType,
        string AggregateType,
        Guid AggregateId,
        string Status);
}
