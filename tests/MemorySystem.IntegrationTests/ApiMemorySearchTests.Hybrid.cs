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
    public async Task Get_memory_hybrid_search_combines_ranking_components_inside_authorized_results()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_memory_hybrid_search_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            var (targetMemoryId, staleMemoryId, orgMemoryId, unauthorizedMemoryId, query) =
                await PrepareHybridSearchFixtureAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var (statusCode, payload, responseBody) = await SendHybridSearchAsync(
                client,
                query,
                scopeType: "project",
                scopeId: ProjectAId.ToString());

            Assert.Equal(HttpStatusCode.OK, statusCode);

            var results = payload.GetProperty("results").EnumerateArray().ToArray();

            Assert.True(results.Length >= 2);
            Assert.Equal(targetMemoryId, results[0].GetProperty("sourceId").GetGuid());
            Assert.DoesNotContain(unauthorizedMemoryId.ToString(), responseBody, StringComparison.OrdinalIgnoreCase);

            var target = results.Single(result => result.GetProperty("sourceId").GetGuid() == targetMemoryId);
            var stale = results.Single(result => result.GetProperty("sourceId").GetGuid() == staleMemoryId);
            var org = results.Single(result => result.GetProperty("sourceId").GetGuid() == orgMemoryId);
            var targetComponents = target.GetProperty("components");
            var staleComponents = stale.GetProperty("components");
            var orgComponents = org.GetProperty("components");

            Assert.True(target.GetProperty("rank").GetDouble() > stale.GetProperty("rank").GetDouble());
            Assert.True(targetComponents.GetProperty("confidence").GetDouble() > staleComponents.GetProperty("confidence").GetDouble());
            Assert.True(targetComponents.GetProperty("recency").GetDouble() > staleComponents.GetProperty("recency").GetDouble());
            Assert.True(targetComponents.GetProperty("authority").GetDouble() > staleComponents.GetProperty("authority").GetDouble());
            Assert.Equal(1.0d, targetComponents.GetProperty("scopeMatch").GetDouble(), precision: 3);
            Assert.Equal("org", org.GetProperty("scopeType").GetString());
            Assert.Equal(OrgAId.ToString(), org.GetProperty("scopeId").GetString());
            Assert.Equal(0.80d, orgComponents.GetProperty("scopeMatch").GetDouble(), precision: 3);
            Assert.Equal(
                ComputeHybridScore(targetComponents),
                target.GetProperty("rank").GetDouble(),
                precision: 6);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Get_memory_hybrid_search_applies_context_feedback_ranking_signal_for_matching_scope_and_role()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_memory_hybrid_feedback_rank_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApiDatabaseTestSupport.ApplyMigrationsAsync(databaseConnectionString);
            await ApiDatabaseTestSupport.InsertPrincipalAsync(databaseConnectionString, PrincipalId);
            await ApiDatabaseTestSupport.InsertOrganizationAndProjectAsync(databaseConnectionString, OrgAId, ProjectAId);
            await ApiDatabaseTestSupport.InsertProjectMembershipAsync(
                databaseConnectionString,
                ProjectAId,
                PrincipalId,
                "reader");
            await ApiDatabaseTestSupport.InsertMemoryAccessGrantAsync(
                databaseConnectionString,
                $"/project/{ProjectAId}/decisions",
                "read",
                principalId: PrincipalId);

            var sourceEventId = Guid.NewGuid();
            await ApiDatabaseTestSupport.InsertSourceEventAsync(
                databaseConnectionString,
                sourceEventId,
                PrincipalId,
                "project",
                ProjectAId.ToString(),
                scopeOrgId: OrgAId,
                scopeProjectId: ProjectAId,
                trustLevel: "human_approved");

            const string query = "feedback ranking calibration path";

            await using var dataSource = NpgsqlDataSource.Create(databaseConnectionString);
            var repository = new PostgresMemoryFactRepository(dataSource);
            var alphaMemory = await repository.StoreAsync(new MemoryFactWriteCommand(
                new MemoryScopeResolution("project", ProjectAId.ToString(), OrgId: OrgAId, ProjectId: ProjectAId),
                $"/project/{ProjectAId}/decisions",
                "decision",
                "project_shared",
                "feedback ranking alpha memory",
                "uses",
                query,
                0.950m,
                sourceEventId,
                PrincipalId));
            var betaMemory = await repository.StoreAsync(new MemoryFactWriteCommand(
                new MemoryScopeResolution("project", ProjectAId.ToString(), OrgId: OrgAId, ProjectId: ProjectAId),
                $"/project/{ProjectAId}/decisions",
                "decision",
                "project_shared",
                "feedback ranking beta memory",
                "uses",
                query,
                0.900m,
                sourceEventId,
                PrincipalId));
            await EmbedMemoryChunksAsync(dataSource);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var (baselineStatusCode, baselinePayload, _) = await SendHybridSearchAsync(
                client,
                query,
                scopeType: "project",
                scopeId: ProjectAId.ToString(),
                roleId: "cto");

            Assert.Equal(HttpStatusCode.OK, baselineStatusCode);
            Assert.Equal(
                alphaMemory.Id,
                baselinePayload.GetProperty("results").EnumerateArray().First().GetProperty("sourceId").GetGuid());

            await InsertContextFeedbackAsync(
                databaseConnectionString,
                query,
                "wrong",
                targetScopeType: "project",
                targetScopeId: ProjectAId.ToString(),
                roleId: "cto",
                sourceType: "memory_fact",
                sourceId: alphaMemory.Id);
            await InsertContextFeedbackAsync(
                databaseConnectionString,
                query,
                "useful",
                targetScopeType: "project",
                targetScopeId: ProjectAId.ToString(),
                roleId: "cto",
                sourceType: "memory_fact",
                sourceId: betaMemory.Id);
            await InsertContextFeedbackAsync(
                databaseConnectionString,
                query,
                "useful",
                targetScopeType: "project",
                targetScopeId: ProjectAId.ToString(),
                roleId: "cfo",
                sourceType: "memory_fact",
                sourceId: alphaMemory.Id);

            var (ctoStatusCode, ctoPayload, _) = await SendHybridSearchAsync(
                client,
                query,
                scopeType: "project",
                scopeId: ProjectAId.ToString(),
                roleId: "cto");
            var ctoResults = ctoPayload.GetProperty("results").EnumerateArray().ToArray();
            var ctoAlpha = ctoResults.Single(result => result.GetProperty("sourceId").GetGuid() == alphaMemory.Id);
            var ctoBeta = ctoResults.Single(result => result.GetProperty("sourceId").GetGuid() == betaMemory.Id);

            Assert.Equal(HttpStatusCode.OK, ctoStatusCode);
            Assert.Equal(betaMemory.Id, ctoResults[0].GetProperty("sourceId").GetGuid());
            Assert.True(ctoAlpha.GetProperty("components").GetProperty("feedbackAdjustment").GetDouble() < 0);
            Assert.True(ctoBeta.GetProperty("components").GetProperty("feedbackAdjustment").GetDouble() > 0);
            Assert.Equal(
                ComputeHybridScore(ctoBeta.GetProperty("components")),
                ctoBeta.GetProperty("rank").GetDouble(),
                precision: 6);

            var (cfoStatusCode, cfoPayload, _) = await SendHybridSearchAsync(
                client,
                query,
                scopeType: "project",
                scopeId: ProjectAId.ToString(),
                roleId: "cfo");

            Assert.Equal(HttpStatusCode.OK, cfoStatusCode);
            Assert.Equal(
                alphaMemory.Id,
                cfoPayload.GetProperty("results").EnumerateArray().First().GetProperty("sourceId").GetGuid());
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Get_memory_hybrid_search_consumes_missing_feedback_as_bounded_query_signal()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_memory_hybrid_missing_feedback_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            var (_, _, _, _, query) = await PrepareHybridSearchFixtureAsync(databaseConnectionString);
            await InsertContextFeedbackAsync(
                databaseConnectionString,
                query,
                "missing",
                targetScopeType: "project",
                targetScopeId: ProjectAId.ToString());

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var (statusCode, payload, _) = await SendHybridSearchAsync(
                client,
                query,
                scopeType: "project",
                scopeId: ProjectAId.ToString());

            Assert.Equal(HttpStatusCode.OK, statusCode);
            Assert.All(
                payload.GetProperty("results").EnumerateArray(),
                result => Assert.InRange(
                    result.GetProperty("components").GetProperty("feedbackAdjustment").GetDouble(),
                    -0.03d,
                    -0.001d));
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Get_memory_hybrid_search_rejects_partial_target_scope()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_memory_hybrid_invalid_scope_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApiDatabaseTestSupport.ApplyMigrationsAsync(databaseConnectionString);
            await ApiDatabaseTestSupport.InsertPrincipalAsync(databaseConnectionString, PrincipalId);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var (statusCode, payload, _) = await SendHybridSearchAsync(
                client,
                "hybrid ranking",
                scopeType: "project");

            Assert.Equal(HttpStatusCode.BadRequest, statusCode);
            Assert.Equal("Memory hybrid search is invalid.", payload.GetProperty("title").GetString());
            Assert.Contains("scopeType", payload.GetProperty("detail").GetString(), StringComparison.Ordinal);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Search_endpoints_reject_noncanonical_target_scope_and_role_values()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_memory_search_scope_validation_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApiDatabaseTestSupport.ApplyMigrationsAsync(databaseConnectionString);
            await ApiDatabaseTestSupport.InsertPrincipalAsync(databaseConnectionString, PrincipalId);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var (invalidProjectStatus, invalidProjectPayload, _) = await SendHybridSearchAsync(
                client,
                "hybrid ranking",
                scopeType: "project",
                scopeId: "not-a-guid");
            var (invalidGlobalStatus, invalidGlobalPayload, _) = await SendHybridSearchAsync(
                client,
                "hybrid ranking",
                scopeType: "global",
                scopeId: ProjectAId.ToString());
            var (invalidRoleStatus, invalidRolePayload, _) = await SendContextPacketAsync(
                client,
                "context packet",
                roleId: "1intern");

            Assert.Equal(HttpStatusCode.BadRequest, invalidProjectStatus);
            Assert.Contains("valid GUID", invalidProjectPayload.GetProperty("detail").GetString(), StringComparison.Ordinal);
            Assert.Equal(HttpStatusCode.BadRequest, invalidGlobalStatus);
            Assert.Contains("'global'", invalidGlobalPayload.GetProperty("detail").GetString(), StringComparison.Ordinal);
            Assert.Equal(HttpStatusCode.BadRequest, invalidRoleStatus);
            Assert.Contains("roleId", invalidRolePayload.GetProperty("detail").GetString(), StringComparison.Ordinal);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }
}
