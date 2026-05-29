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
}
