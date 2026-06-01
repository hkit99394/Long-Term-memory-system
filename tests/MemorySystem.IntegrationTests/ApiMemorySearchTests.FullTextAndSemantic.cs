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
    public async Task Get_memory_search_returns_authorized_full_text_matches_only()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_memory_search_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            var (projectAMemoryId, _, sensitiveMemoryId) = await PrepareSearchFixtureAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var (statusCode, payload, responseBody) = await SendSearchAsync(client, "postgres audit retrieval");

            Assert.Equal(HttpStatusCode.OK, statusCode);

            var results = payload.GetProperty("results").EnumerateArray().ToArray();
            var result = Assert.Single(results);

            Assert.Equal(projectAMemoryId, result.GetProperty("sourceId").GetGuid());
            Assert.Equal("memory_fact", result.GetProperty("sourceType").GetString());
            Assert.Equal("project", result.GetProperty("scopeType").GetString());
            Assert.Equal(ProjectAId.ToString(), result.GetProperty("scopeId").GetString());
            Assert.Equal($"/project/{ProjectAId}/decisions", result.GetProperty("namespace").GetString());
            Assert.True(result.GetProperty("rank").GetDouble() > 0);
            Assert.Contains("Project A retrieval decision", result.GetProperty("content").GetString(), StringComparison.Ordinal);
            Assert.DoesNotContain("Project B retrieval decision", responseBody, StringComparison.Ordinal);
            Assert.DoesNotContain(sensitiveMemoryId.ToString(), responseBody, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Get_memory_search_rejects_blank_query()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_memory_search_blank_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApiDatabaseTestSupport.ApplyMigrationsAsync(databaseConnectionString);
            await ApiDatabaseTestSupport.InsertPrincipalAsync(databaseConnectionString, PrincipalId);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var (statusCode, payload, _) = await SendSearchAsync(client, " ");

            Assert.Equal(HttpStatusCode.BadRequest, statusCode);
            Assert.Equal("Memory search is invalid.", payload.GetProperty("title").GetString());
            Assert.Contains("'q' is required", payload.GetProperty("detail").GetString(), StringComparison.Ordinal);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Get_memory_semantic_search_returns_authorized_vector_matches_only()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_memory_semantic_search_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            var (projectATargetMemoryId, projectBMemoryId, sensitiveMemoryId, query) =
                await PrepareSemanticSearchFixtureAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var (statusCode, payload, responseBody) = await SendSemanticSearchAsync(client, query, limit: 3);

            Assert.Equal(HttpStatusCode.OK, statusCode);

            var results = payload.GetProperty("results").EnumerateArray().ToArray();
            var targetResult = Assert.Single(
                results,
                result => result.GetProperty("sourceId").GetGuid() == projectATargetMemoryId);

            Assert.Equal(2, results.Length);
            Assert.Equal("memory_fact", targetResult.GetProperty("sourceType").GetString());
            Assert.Equal("project", targetResult.GetProperty("scopeType").GetString());
            Assert.Equal(ProjectAId.ToString(), targetResult.GetProperty("scopeId").GetString());
            Assert.Equal($"/project/{ProjectAId}/decisions", targetResult.GetProperty("namespace").GetString());
            Assert.InRange(targetResult.GetProperty("rank").GetDouble(), -1.0d, 1.0d);
            Assert.DoesNotContain(projectBMemoryId.ToString(), responseBody, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(sensitiveMemoryId.ToString(), responseBody, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Get_memory_semantic_search_rejects_blank_query()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_memory_semantic_blank_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApiDatabaseTestSupport.ApplyMigrationsAsync(databaseConnectionString);
            await ApiDatabaseTestSupport.InsertPrincipalAsync(databaseConnectionString, PrincipalId);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var (statusCode, payload, _) = await SendSemanticSearchAsync(client, " ");

            Assert.Equal(HttpStatusCode.BadRequest, statusCode);
            Assert.Equal("Memory semantic search is invalid.", payload.GetProperty("title").GetString());
            Assert.Contains("'q' is required", payload.GetProperty("detail").GetString(), StringComparison.Ordinal);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }
}
