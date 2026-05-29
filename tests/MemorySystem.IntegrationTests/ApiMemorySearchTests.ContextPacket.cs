using System.Net;
using System.Text.Json;
using MemorySystem.Application.MemoryEmbeddings;
using MemorySystem.Application.MemoryEvaluations;
using MemorySystem.Application.MemoryFacts;
using MemorySystem.Application.RoleMemoryLenses;
using MemorySystem.Application.Scopes;
using MemorySystem.Infrastructure.MemoryEmbeddings;
using MemorySystem.Infrastructure.MemoryFacts;
using MemorySystem.Infrastructure.RoleMemoryLenses;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;

namespace MemorySystem.IntegrationTests;

public sealed partial class ApiMemorySearchTests
{
    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Get_memory_context_returns_compact_source_identified_explainable_packet()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_memory_context_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            var fixture = await PrepareContextPacketFixtureAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var (statusCode, payload, responseBody) = await SendContextPacketAsync(
                client,
                "cto context packet concise decision logs authorization predicates operational reversibility",
                scopeType: "project",
                scopeId: ProjectAId.ToString(),
                roleId: "cto",
                limit: 6);

            Assert.Equal(HttpStatusCode.OK, statusCode);
            Assert.Equal(PrincipalId, payload.GetProperty("principalId").GetGuid());
            Assert.Equal("cto", payload.GetProperty("roleId").GetString());
            Assert.Equal("project", payload.GetProperty("targetScope").GetProperty("scopeType").GetString());
            Assert.Equal(ProjectAId.ToString(), payload.GetProperty("targetScope").GetProperty("scopeId").GetString());
            Assert.Equal("cto", payload.GetProperty("currentTask").GetProperty("roleId").GetString());

            var userPreference = Assert.Single(payload.GetProperty("userPreferences").EnumerateArray());
            Assert.Equal("user_preference", userPreference.GetProperty("kind").GetString());
            Assert.True(userPreference.GetProperty("content").GetString()!.Length <= 360);
            Assert.Equal(fixture.UserPreferenceEventId, userPreference.GetProperty("sourceEventId").GetGuid());
            Assert.Equal($"/api/events/{fixture.UserPreferenceEventId}", userPreference.GetProperty("sourceLink").GetString());
            Assert.True(userPreference.GetProperty("explanation").GetProperty("rank").GetDouble() > 0);
            Assert.True(userPreference.GetProperty("explanation").GetProperty("components").GetProperty("relevance").GetDouble() >= 0);
            Assert.Contains("confidence", userPreference.GetProperty("explanation").GetProperty("summary").GetString(), StringComparison.Ordinal);

            var relevantDecision = Assert.Single(payload.GetProperty("relevantDecisions").EnumerateArray());
            Assert.Equal(fixture.ProjectDecisionId, relevantDecision.GetProperty("sourceId").GetGuid());
            Assert.Equal("project_decision", relevantDecision.GetProperty("kind").GetString());
            Assert.Equal($"/api/events/{fixture.ProjectDecisionEventId}", relevantDecision.GetProperty("sourceLink").GetString());

            var roleMemory = Assert.Single(payload.GetProperty("roleMemory").EnumerateArray());
            Assert.Equal("project_role_lens", roleMemory.GetProperty("kind").GetString());
            Assert.Equal(fixture.RoleMemoryLensId, roleMemory.GetProperty("sourceId").GetGuid());
            Assert.Equal(fixture.ProjectDecisionId, roleMemory.GetProperty("baseMemoryFactId").GetGuid());
            Assert.Equal($"/api/events/{fixture.RoleLensEventId}", roleMemory.GetProperty("sourceLink").GetString());

            var sourceEvents = payload
                .GetProperty("sourceEvents")
                .EnumerateArray()
                .ToArray();
            var sourceEventIds = sourceEvents
                .Select(sourceEvent => sourceEvent.GetProperty("id").GetGuid())
                .ToArray();

            Assert.Contains(fixture.UserPreferenceEventId, sourceEventIds);
            Assert.Contains(fixture.ProjectDecisionEventId, sourceEventIds);
            Assert.Contains(fixture.RoleLensEventId, sourceEventIds);
            Assert.All(
                sourceEvents,
                sourceEvent => Assert.Equal(
                    $"/api/events/{sourceEvent.GetProperty("id").GetGuid()}",
                    sourceEvent.GetProperty("link").GetString()));
            Assert.DoesNotContain(fixture.CfoRoleLensEventId, sourceEventIds);
            Assert.DoesNotContain(fixture.ProjectBDecisionEventId, sourceEventIds);
            Assert.DoesNotContain(fixture.CfoRoleMemoryLensId.ToString(), responseBody, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(fixture.ProjectBDecisionId.ToString(), responseBody, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Project B private decision", responseBody, StringComparison.Ordinal);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Get_memory_context_filters_role_specific_candidates_before_ranking_limit()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_memory_context_role_limit_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            var ctoMemoryId = await PrepareRoleFilteredContextOverflowFixtureAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var (statusCode, payload, responseBody) = await SendContextPacketAsync(
                client,
                "role filtered ranking saturation signal",
                scopeType: "project",
                scopeId: ProjectAId.ToString(),
                roleId: "cto",
                limit: 1);

            Assert.Equal(HttpStatusCode.OK, statusCode);

            var roleMemory = Assert.Single(payload.GetProperty("roleMemory").EnumerateArray());

            Assert.Equal(ctoMemoryId, roleMemory.GetProperty("sourceId").GetGuid());
            Assert.Equal($"/project/{ProjectAId}/role/cto/lens", roleMemory.GetProperty("namespace").GetString());
            Assert.DoesNotContain($"/project/{ProjectAId}/role/cfo/lens", responseBody, StringComparison.Ordinal);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Get_memory_context_can_be_scored_with_retrieval_evaluation_metrics()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_memory_context_eval_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            var fixture = await PrepareContextPacketFixtureAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var (statusCode, payload, _) = await SendContextPacketAsync(
                client,
                "cto context packet concise decision logs authorization predicates operational reversibility",
                scopeType: "project",
                scopeId: ProjectAId.ToString(),
                roleId: "cto",
                limit: 6);

            Assert.Equal(HttpStatusCode.OK, statusCode);

            var sessionOnlyCandidateId = Guid.Parse("88888888-8888-4888-8888-888888888888");
            var evaluation = MemoryRetrievalEvaluator.Evaluate(
                new MemoryRetrievalEvaluationCase(
                    RelevantSourceIds:
                    [
                        fixture.UserPreferenceId,
                        fixture.ProjectDecisionId,
                        fixture.RoleMemoryLensId
                    ],
                    AllowedSourceIds:
                    [
                        fixture.UserPreferenceId,
                        fixture.ProjectDecisionId,
                        fixture.RoleMemoryLensId
                    ],
                    ForbiddenSourceIds: [fixture.ProjectBDecisionId],
                    ContradictedSourceIds: [fixture.ContradictedProjectDecisionId],
                    MaxItemCount: 6,
                    MaxContentLength: 360,
                    WriteObservations:
                    [
                        new MemoryRetrievalWriteObservation(
                            fixture.UserPreferenceId,
                            ExpectedDurable: true,
                            StoredDurably: true),
                        new MemoryRetrievalWriteObservation(
                            sessionOnlyCandidateId,
                            ExpectedDurable: false,
                            StoredDurably: false),
                        new MemoryRetrievalWriteObservation(
                            fixture.ContradictedProjectDecisionId,
                            ExpectedDurable: false,
                            StoredDurably: false,
                            ExpectedContradiction: true,
                            RoutedAsContradiction: true)
                    ]),
                ReadContextPacketEvaluationItems(payload));

            Assert.Equal(1.0d, evaluation.Relevance, precision: 3);
            Assert.True(evaluation.IsCompact);
            Assert.Equal(1.0d, evaluation.Compactness, precision: 3);
            Assert.Equal(1.0d, evaluation.WritePrecision, precision: 3);
            Assert.Equal(0, evaluation.RetrievalFalsePositiveCount);
            Assert.Equal(0, evaluation.DurableWriteFalsePositiveCount);
            Assert.Equal(0.0d, evaluation.FalsePositiveRate, precision: 3);
            Assert.Equal(1.0d, evaluation.ContradictionQuality, precision: 3);
            Assert.Empty(evaluation.MissingRelevantSourceIds);
            Assert.Empty(evaluation.FalsePositiveSourceIds);
            Assert.Empty(evaluation.FalsePositiveWriteCandidateIds);
            Assert.Empty(evaluation.OversizedSourceIds);
            Assert.Empty(evaluation.RetrievedContradictedSourceIds);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Get_memory_context_rejects_large_packet_limit()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_memory_context_limit_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApiDatabaseTestSupport.ApplyMigrationsAsync(databaseConnectionString);
            await ApiDatabaseTestSupport.InsertPrincipalAsync(databaseConnectionString, PrincipalId);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var (statusCode, payload, _) = await SendContextPacketAsync(
                client,
                "context packet",
                limit: 13);

            Assert.Equal(HttpStatusCode.BadRequest, statusCode);
            Assert.Equal("Memory context request is invalid.", payload.GetProperty("title").GetString());
            Assert.Contains("between 1 and 12", payload.GetProperty("detail").GetString(), StringComparison.Ordinal);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }
}
