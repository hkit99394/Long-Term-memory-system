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
}
