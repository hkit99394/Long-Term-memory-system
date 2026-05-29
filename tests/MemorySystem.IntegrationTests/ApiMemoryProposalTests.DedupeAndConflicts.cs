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
}
