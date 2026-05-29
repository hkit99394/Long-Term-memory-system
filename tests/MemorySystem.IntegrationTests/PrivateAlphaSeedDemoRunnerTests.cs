using System.Net;
using System.Text;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.IntegrationTests;

public sealed class PrivateAlphaSeedDemoRunnerTests
{
    private const string TestApiKey = "test-api-key";
    private static readonly Guid PrincipalId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid ProjectAId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid ProjectBId = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly Guid ProjectDecisionFactId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
    private static readonly Guid FactFindingContradictionOverlayEventId = Guid.Parse("13131313-1313-4131-8131-131313131313");
    private static readonly Guid FactFindingRedactedOverlayEventId = Guid.Parse("14141414-1414-4141-8141-141414141414");
    private static readonly Guid FactFindingSupersededOverlayMemoryFactId = Guid.Parse("f1f1f1f1-f1f1-4f1f-8f1f-f1f1f1f1f1f1");
    private static readonly Guid FactFindingRedactedOverlayMemoryFactId = Guid.Parse("f2f2f2f2-f2f2-4f2f-8f2f-f2f2f2f2f2f2");

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task RunAsync_creates_scenario_0001_demo_data_and_is_repeatable()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_private_alpha_seed_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await RunSeederAsync(databaseConnectionString);
            await RunSeederAsync(databaseConnectionString);

            Assert.Equal(1, await CountRowsAsync(databaseConnectionString, "principals", PrincipalId));
            Assert.Equal(5, await CountRowsAsync(databaseConnectionString, "events", [
                Guid.Parse("66666666-6666-4666-8666-666666666666"),
                Guid.Parse("77777777-7777-4777-8777-777777777777"),
                Guid.Parse("88888888-8888-4888-8888-888888888888"),
                Guid.Parse("99999999-9999-4999-8999-999999999999"),
                Guid.Parse("12121212-1212-4121-8121-121212121212")
            ]));
            Assert.Equal(4, await CountRowsAsync(databaseConnectionString, "memory_facts", [
                Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),
                Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"),
                Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc"),
                Guid.Parse("eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee")
            ]));
            Assert.Equal(2, await CountRowsAsync(databaseConnectionString, "role_memory_lenses", [
                Guid.Parse("dddddddd-dddd-4ddd-8ddd-dddddddddddd"),
                Guid.Parse("efefefef-efef-4efe-8efe-efefefefefef")
            ]));
            Assert.Equal(6, await CountSeededChunksAsync(databaseConnectionString));
            Assert.Equal(6, await CountSeededEmbeddingsAsync(databaseConnectionString));
            Assert.Equal(0, await CountProjectMembershipsAsync(databaseConnectionString, ProjectBId, PrincipalId));

            using var factory = MemorySystemApiTestFactory.Create(
                databaseConnectionString,
                TestApiKey,
                PrincipalId.ToString());
            using var client = factory.CreateClient();

            using var response = await SendContextPacketAsync(client);
            var responseBody = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("concise decision logs", responseBody, StringComparison.Ordinal);
            Assert.Contains("SQL-first migrations plus raw Npgsql", responseBody, StringComparison.Ordinal);
            Assert.Contains("architecture risk, operational reversibility", responseBody, StringComparison.Ordinal);
            Assert.Contains("risk-reduction move", responseBody, StringComparison.Ordinal);
            Assert.DoesNotContain("confidential runway model", responseBody, StringComparison.Ordinal);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task RunAsync_with_benchmark_overlays_creates_fact_finding_fixture_and_is_repeatable()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_private_alpha_overlay_seed_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await RunSeederAsync(databaseConnectionString, includeBenchmarkOverlays: true);
            await RunSeederAsync(databaseConnectionString, includeBenchmarkOverlays: true);

            Assert.Equal(2, await CountRowsAsync(databaseConnectionString, "events", [
                FactFindingContradictionOverlayEventId,
                FactFindingRedactedOverlayEventId
            ]));
            Assert.Equal(2, await CountRowsAsync(databaseConnectionString, "memory_facts", [
                FactFindingSupersededOverlayMemoryFactId,
                FactFindingRedactedOverlayMemoryFactId
            ]));

            using var factory = MemorySystemApiTestFactory.Create(
                databaseConnectionString,
                TestApiKey,
                PrincipalId.ToString());
            using var client = factory.CreateClient();

            var (statusCode, payload, responseBody) = await SendQueryFactsAsync(
                client,
                new
                {
                    query = "M1-M3 data access",
                    targetScope = new
                    {
                        scopeType = "project",
                        scopeId = ProjectAId.ToString()
                    },
                    roleId = "cto",
                    memoryTypes = new[] { "decision" },
                    includeContradictions = true,
                    includeExcluded = true,
                    limit = 8
                });

            Assert.Equal(HttpStatusCode.OK, statusCode);
            Assert.Contains(payload.GetProperty("facts").EnumerateArray(), fact =>
                fact.GetProperty("id").GetGuid() == ProjectDecisionFactId
                && fact.GetProperty("claim").GetString()!.Contains(
                    "SQL-first migrations plus raw Npgsql",
                    StringComparison.Ordinal));

            var contradiction = Assert.Single(
                payload.GetProperty("contradictions").EnumerateArray(),
                contradiction =>
                    contradiction.GetProperty("currentFactId").GetGuid() == ProjectDecisionFactId
                    && contradiction.GetProperty("relatedFactId").GetGuid() == FactFindingSupersededOverlayMemoryFactId);
            Assert.Equal("superseded", contradiction.GetProperty("relatedStatus").GetString());

            var exclusions = payload.GetProperty("excluded").EnumerateArray().ToArray();
            Assert.Contains(exclusions, exclusion =>
                exclusion.GetProperty("reason").GetString() == "inactive"
                && exclusion.GetProperty("count").GetInt32() >= 1);
            Assert.Contains(exclusions, exclusion =>
                exclusion.GetProperty("reason").GetString() == "redacted_or_deleted"
                && exclusion.GetProperty("count").GetInt32() >= 1);

            Assert.DoesNotContain("redacted benchmark migration path should stay hidden", responseBody, StringComparison.Ordinal);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    private static async Task RunSeederAsync(
        string databaseConnectionString,
        bool includeBenchmarkOverlays = false)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var args = new List<string>
        {
            "--connection-string",
            databaseConnectionString,
            "--migrations-directory",
            MigrationTestPaths.FindMigrationsDirectory()
        };

        var exitCode = await PrivateAlphaSeedCli.RunAsync(
            includeBenchmarkOverlays
                ? [.. args, "--include-benchmark-overlays"]
                : [.. args],
            output,
            error);

        Assert.Equal(0, exitCode);
        Assert.Empty(error.ToString());
        Assert.Contains("Scenario 0001 private-alpha demo data is ready.", output.ToString(), StringComparison.Ordinal);
        if (includeBenchmarkOverlays)
        {
            Assert.Contains(
                "benchmark_overlays: fact_finding_contradiction_overlay",
                output.ToString(),
                StringComparison.Ordinal);
        }
    }

    private static async Task<HttpResponseMessage> SendContextPacketAsync(HttpClient client)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/memory/context?q={Uri.EscapeDataString("cto context packet concise decision logs SQL-first Npgsql architecture risk operational reversibility")}&scopeType=project&scopeId={ProjectAId}&roleId=cto&limit=6");
        request.Headers.Add("X-Api-Key", TestApiKey);

        return await client.SendAsync(request);
    }

    private static async Task<(HttpStatusCode StatusCode, JsonElement Payload, string ResponseBody)> SendQueryFactsAsync(
        HttpClient client,
        object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/memory/query-facts")
        {
            Content = JsonBody(body)
        };
        request.Headers.Add("X-Api-Key", TestApiKey);

        using var response = await client.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(responseBody);

        return (response.StatusCode, document.RootElement.Clone(), responseBody);
    }

    private static StringContent JsonBody(object body)
    {
        return new StringContent(
            JsonSerializer.Serialize(body),
            Encoding.UTF8,
            "application/json");
    }

    private static async Task<int> CountRowsAsync(string connectionString, string tableName, Guid id)
    {
        return await CountRowsAsync(connectionString, tableName, [id]);
    }

    private static async Task<int> CountRowsAsync(string connectionString, string tableName, Guid[] ids)
    {
        if (tableName is not ("principals" or "events" or "memory_facts" or "role_memory_lenses"))
        {
            throw new ArgumentException($"Table '{tableName}' is not supported.", nameof(tableName));
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            $"SELECT count(*)::int FROM {tableName} WHERE id = ANY(@ids);",
            connection);
        command.Parameters.Add("ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid).Value = ids;

        return (int)(await command.ExecuteScalarAsync() ?? 0);
    }

    private static async Task<int> CountSeededChunksAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT count(*)::int
            FROM memory_chunks
            WHERE source_id = ANY(@source_ids);
            """,
            connection);
        command.Parameters.Add("source_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid).Value = new[]
        {
            Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),
            Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"),
            Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc"),
            Guid.Parse("dddddddd-dddd-4ddd-8ddd-dddddddddddd"),
            Guid.Parse("efefefef-efef-4efe-8efe-efefefefefef"),
            Guid.Parse("eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee")
        };

        return (int)(await command.ExecuteScalarAsync() ?? 0);
    }

    private static async Task<int> CountSeededEmbeddingsAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT count(*)::int
            FROM memory_embeddings AS embedding
            INNER JOIN memory_chunks AS chunk ON chunk.id = embedding.chunk_id
            WHERE chunk.source_id = ANY(@source_ids)
                AND embedding.embedding_model = 'memory-deterministic-v1';
            """,
            connection);
        command.Parameters.Add("source_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid).Value = new[]
        {
            Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),
            Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"),
            Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc"),
            Guid.Parse("dddddddd-dddd-4ddd-8ddd-dddddddddddd"),
            Guid.Parse("efefefef-efef-4efe-8efe-efefefefefef"),
            Guid.Parse("eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee")
        };

        return (int)(await command.ExecuteScalarAsync() ?? 0);
    }

    private static async Task<int> CountProjectMembershipsAsync(
        string connectionString,
        Guid projectId,
        Guid principalId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT count(*)::int
            FROM project_memberships
            WHERE project_id = @project_id
                AND principal_id = @principal_id;
            """,
            connection);
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("principal_id", principalId);

        return (int)(await command.ExecuteScalarAsync() ?? 0);
    }
}
