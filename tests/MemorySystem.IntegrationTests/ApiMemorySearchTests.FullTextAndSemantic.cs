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
            var (projectAMemoryId, _) = await PrepareSearchFixtureAsync(databaseConnectionString);

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
            var (projectATargetMemoryId, projectBMemoryId, query) =
                await PrepareSemanticSearchFixtureAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var (statusCode, payload, responseBody) = await SendSemanticSearchAsync(client, query, limit: 2);

            Assert.Equal(HttpStatusCode.OK, statusCode);

            var results = payload.GetProperty("results").EnumerateArray().ToArray();

            Assert.Equal(2, results.Length);
            Assert.Equal(projectATargetMemoryId, results[0].GetProperty("sourceId").GetGuid());
            Assert.Equal("memory_fact", results[0].GetProperty("sourceType").GetString());
            Assert.Equal("project", results[0].GetProperty("scopeType").GetString());
            Assert.Equal(ProjectAId.ToString(), results[0].GetProperty("scopeId").GetString());
            Assert.Equal($"/project/{ProjectAId}/decisions", results[0].GetProperty("namespace").GetString());
            Assert.InRange(results[0].GetProperty("rank").GetDouble(), 0.999d, 1.001d);
            Assert.DoesNotContain(projectBMemoryId.ToString(), responseBody, StringComparison.OrdinalIgnoreCase);
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
