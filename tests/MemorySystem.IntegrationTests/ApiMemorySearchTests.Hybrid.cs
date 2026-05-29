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
                roleId: "intern");

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
