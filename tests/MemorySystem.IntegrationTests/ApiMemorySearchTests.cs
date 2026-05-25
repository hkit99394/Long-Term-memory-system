using System.Net;
using System.Text.Json;
using MemorySystem.Application.MemoryEmbeddings;
using MemorySystem.Application.MemoryFacts;
using MemorySystem.Application.Scopes;
using MemorySystem.Infrastructure.MemoryEmbeddings;
using MemorySystem.Infrastructure.MemoryFacts;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;

namespace MemorySystem.IntegrationTests;

public sealed class ApiMemorySearchTests
{
    private const string TestApiKey = "test-api-key";
    private static readonly Guid PrincipalId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid OrgAId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid ProjectAId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid OrgBId = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly Guid ProjectBId = Guid.Parse("55555555-5555-4555-8555-555555555555");
    private static readonly Guid ProjectAEventId = Guid.Parse("66666666-6666-4666-8666-666666666666");
    private static readonly Guid ProjectBEventId = Guid.Parse("77777777-7777-4777-8777-777777777777");

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

    private static async Task<(Guid ProjectAMemoryId, Guid ProjectBMemoryId)> PrepareSearchFixtureAsync(
        string connectionString)
    {
        await ApiDatabaseTestSupport.ApplyMigrationsAsync(connectionString);
        await ApiDatabaseTestSupport.InsertPrincipalAsync(connectionString, PrincipalId);
        await ApiDatabaseTestSupport.InsertOrganizationAndProjectAsync(connectionString, OrgAId, ProjectAId);
        await ApiDatabaseTestSupport.InsertOrganizationAndProjectAsync(connectionString, OrgBId, ProjectBId);
        await ApiDatabaseTestSupport.InsertProjectMembershipAsync(
            connectionString,
            ProjectAId,
            PrincipalId,
            "reader");
        await ApiDatabaseTestSupport.InsertMemoryAccessGrantAsync(
            connectionString,
            $"/project/{ProjectAId}/decisions",
            "read",
            principalId: PrincipalId);
        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            ProjectAEventId,
            PrincipalId,
            "project",
            ProjectAId.ToString(),
            scopeOrgId: OrgAId,
            scopeProjectId: ProjectAId);
        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            ProjectBEventId,
            PrincipalId,
            "project",
            ProjectBId.ToString(),
            scopeOrgId: OrgBId,
            scopeProjectId: ProjectBId);

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var repository = new PostgresMemoryFactRepository(dataSource);
        var projectAMemory = await repository.StoreAsync(new MemoryFactWriteCommand(
            new MemoryScopeResolution("project", ProjectAId.ToString(), OrgId: OrgAId, ProjectId: ProjectAId),
            $"/project/{ProjectAId}/decisions",
            "decision",
            "project_shared",
            "Project A retrieval decision",
            "uses",
            "postgres audit retrieval",
            0.950m,
            ProjectAEventId,
            PrincipalId));
        var projectBMemory = await repository.StoreAsync(new MemoryFactWriteCommand(
            new MemoryScopeResolution("project", ProjectBId.ToString(), OrgId: OrgBId, ProjectId: ProjectBId),
            $"/project/{ProjectBId}/decisions",
            "decision",
            "project_shared",
            "Project B retrieval decision",
            "uses",
            "postgres audit retrieval",
            0.950m,
            ProjectBEventId,
            PrincipalId));

        return (projectAMemory.Id, projectBMemory.Id);
    }

    private static async Task<(Guid ProjectATargetMemoryId, Guid ProjectBMemoryId, string Query)>
        PrepareSemanticSearchFixtureAsync(string connectionString)
    {
        await ApiDatabaseTestSupport.ApplyMigrationsAsync(connectionString);
        await ApiDatabaseTestSupport.InsertPrincipalAsync(connectionString, PrincipalId);
        await ApiDatabaseTestSupport.InsertOrganizationAndProjectAsync(connectionString, OrgAId, ProjectAId);
        await ApiDatabaseTestSupport.InsertOrganizationAndProjectAsync(connectionString, OrgBId, ProjectBId);
        await ApiDatabaseTestSupport.InsertProjectMembershipAsync(
            connectionString,
            ProjectAId,
            PrincipalId,
            "reader");
        await ApiDatabaseTestSupport.InsertMemoryAccessGrantAsync(
            connectionString,
            $"/project/{ProjectAId}/decisions",
            "read",
            principalId: PrincipalId);
        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            ProjectAEventId,
            PrincipalId,
            "project",
            ProjectAId.ToString(),
            scopeOrgId: OrgAId,
            scopeProjectId: ProjectAId);
        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            ProjectBEventId,
            PrincipalId,
            "project",
            ProjectBId.ToString(),
            scopeOrgId: OrgBId,
            scopeProjectId: ProjectBId);

        const string targetSubject = "semantic retrieval target";
        const string targetObject = "pgvector cosine recall";
        var exactQuery = $"{targetSubject} {targetSubject} uses {targetObject}";

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var repository = new PostgresMemoryFactRepository(dataSource);
        var projectATargetMemory = await repository.StoreAsync(new MemoryFactWriteCommand(
            new MemoryScopeResolution("project", ProjectAId.ToString(), OrgId: OrgAId, ProjectId: ProjectAId),
            $"/project/{ProjectAId}/decisions",
            "decision",
            "project_shared",
            targetSubject,
            "uses",
            targetObject,
            0.950m,
            ProjectAEventId,
            PrincipalId));
        var projectBMemory = await repository.StoreAsync(new MemoryFactWriteCommand(
            new MemoryScopeResolution("project", ProjectBId.ToString(), OrgId: OrgBId, ProjectId: ProjectBId),
            $"/project/{ProjectBId}/decisions",
            "decision",
            "project_shared",
            targetSubject,
            "uses",
            targetObject,
            0.950m,
            ProjectBEventId,
            PrincipalId));
        await repository.StoreAsync(new MemoryFactWriteCommand(
            new MemoryScopeResolution("project", ProjectAId.ToString(), OrgId: OrgAId, ProjectId: ProjectAId),
            $"/project/{ProjectAId}/decisions",
            "decision",
            "project_shared",
            "semantic distractor",
            "uses",
            "calendar planning",
            0.950m,
            ProjectAEventId,
            PrincipalId));

        await EmbedMemoryChunksAsync(dataSource);

        return (projectATargetMemory.Id, projectBMemory.Id, exactQuery);
    }

    private static async Task EmbedMemoryChunksAsync(NpgsqlDataSource dataSource)
    {
        var chunks = new List<(Guid ChunkId, string Input)>();

        await using (var connection = await dataSource.OpenConnectionAsync())
        await using (var command = new NpgsqlCommand(
            """
            SELECT id, concat_ws(' ', title, content) AS input
            FROM memory_chunks
            ORDER BY created_at, id;
            """,
            connection))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                chunks.Add((reader.GetGuid(0), reader.GetString(1)));
            }
        }

        var provider = new DeterministicMemoryEmbeddingProvider(Options.Create(new MemoryEmbeddingOptions()));
        var store = new PostgresMemoryChunkEmbeddingStore(dataSource);

        foreach (var chunk in chunks)
        {
            var embedding = await provider.EmbedAsync(new MemoryEmbeddingRequest(chunk.Input));
            await store.StoreAsync(new MemoryChunkEmbeddingWriteCommand(
                chunk.ChunkId,
                embedding.Model,
                embedding.Dimension,
                embedding.Values));
        }
    }

    private static async Task<(HttpStatusCode StatusCode, JsonElement Payload, string Body)> SendSearchAsync(
        HttpClient client,
        string query,
        int limit = 10)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/memory/search?q={Uri.EscapeDataString(query)}&limit={limit}");
        request.Headers.Add("X-Api-Key", TestApiKey);

        using var response = await client.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(responseBody);
        return (response.StatusCode, document.RootElement.Clone(), responseBody);
    }

    private static async Task<(HttpStatusCode StatusCode, JsonElement Payload, string Body)> SendSemanticSearchAsync(
        HttpClient client,
        string query,
        int limit = 10)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/memory/search/semantic?q={Uri.EscapeDataString(query)}&limit={limit}");
        request.Headers.Add("X-Api-Key", TestApiKey);

        using var response = await client.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(responseBody);
        return (response.StatusCode, document.RootElement.Clone(), responseBody);
    }

    private static WebApplicationFactory<Program> CreateFactory(string postgresConnectionString)
    {
        return MemorySystemApiTestFactory.Create(postgresConnectionString, TestApiKey, PrincipalId.ToString());
    }
}
