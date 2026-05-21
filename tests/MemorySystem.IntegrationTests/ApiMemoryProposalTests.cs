using MemorySystem.Infrastructure.Migrations;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.IntegrationTests;

public sealed class ApiMemoryProposalTests
{
    private const string TestApiKey = "test-api-key";
    private const string TestPrincipalId = "11111111-1111-4111-8111-111111111111";
    private static readonly Guid SourceEventId = Guid.Parse("66666666-6666-4666-8666-666666666666");

    [Fact]
    [Trait("Category", "Database")]
    public async Task Post_memory_proposals_returns_stored_decision_for_supported_durable_candidate()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_proposal_stored_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var payload = await SendProposalAsync(client, "proposal-stored-key", CreateProposalBody());
            var idempotencyRecord = await ReadIdempotencyRecordAsync(databaseConnectionString);

            Assert.Equal("stored", payload.GetProperty("decision").GetString());
            Assert.Null(payload.GetProperty("memoryId").GetString());
            Assert.Equal(SourceEventId, payload.GetProperty("sourceEventId").GetGuid());
            Assert.Equal("POST /api/memory/proposals", idempotencyRecord.Endpoint);
            Assert.Equal("proposal-stored-key", idempotencyRecord.IdempotencyKey);
            Assert.Equal(200, idempotencyRecord.ResponseStatus);
            Assert.Null(idempotencyRecord.ResourceType);
            Assert.Null(idempotencyRecord.ResourceId);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [Fact]
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
            Assert.Equal(1, await CountIdempotencyRecordsAsync(databaseConnectionString));
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [Theory]
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
            var body = CreateProposalBody(
                confidence: confidence,
                memoryType: memoryType,
                scopeType: scopeType,
                scopeId: scopeId,
                namespaceValue: namespaceValue);

            var payload = await SendProposalAsync(client, $"proposal-{expectedDecision}-key", body);

            Assert.Equal(expectedDecision, payload.GetProperty("decision").GetString());
            Assert.Null(payload.GetProperty("memoryId").GetString());
            Assert.Equal(SourceEventId, payload.GetProperty("sourceEventId").GetGuid());
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task Post_memory_proposals_rejects_missing_source_event()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_proposal_rejected_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();
            var body = CreateProposalBody(sourceEventId: Guid.NewGuid());

            var payload = await SendProposalAsync(client, "proposal-rejected-key", body);

            Assert.Equal("rejected", payload.GetProperty("decision").GetString());
            Assert.Contains("source event", payload.GetProperty("reason").GetString(), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    private static string CreateProposalBody(
        Guid? sourceEventId = null,
        decimal confidence = 0.95m,
        string memoryType = "preference",
        string scopeType = "user",
        string? scopeId = null,
        string? namespaceValue = null)
    {
        var resolvedScopeId = scopeId ?? TestPrincipalId;
        var resolvedNamespace = namespaceValue ?? $"/user/{TestPrincipalId}/preferences";
        var resolvedSourceEventId = sourceEventId ?? SourceEventId;

        return $$"""
            {
              "sourceEventId": "{{resolvedSourceEventId}}",
              "memoryType": "{{memoryType}}",
              "scopeType": "{{scopeType}}",
              "scopeId": "{{resolvedScopeId}}",
              "namespace": "{{resolvedNamespace}}",
              "visibility": "private",
              "subject": "technical planning format",
              "predicate": "prefers",
              "object": "concise decision logs",
              "confidence": {{confidence}},
              "trustLevel": "user_scoped",
              "sensitivity": "none"
            }
            """;
    }

    private static async Task<JsonElement> SendProposalAsync(
        HttpClient client,
        string idempotencyKey,
        string body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/memory/proposals")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Api-Key", TestApiKey);
        request.Headers.Add("Idempotency-Key", idempotencyKey);

        using var response = await client.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(responseBody);
        return document.RootElement.Clone();
    }

    private static WebApplicationFactory<Program> CreateFactory(string postgresConnectionString)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, configurationBuilder) =>
                {
                    configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Authentication:ApiKey:Keys:test-key:Key"] = TestApiKey,
                        ["Authentication:ApiKey:Keys:test-key:PrincipalId"] = TestPrincipalId,
                        ["Authentication:ApiKey:Keys:test-key:DisplayName"] = "Test API caller",
                        ["ConnectionStrings:Postgres"] = postgresConnectionString
                    });
                });
            });
    }

    private static async Task PrepareDatabaseAsync(string connectionString)
    {
        await SqlMigrationRunner.ApplyAsync(connectionString, MigrationTestPaths.FindMigrationsDirectory());
        await InsertPrincipalAsync(connectionString);
        await InsertSourceEventAsync(connectionString);
    }

    private static async Task InsertPrincipalAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO principals (
                id,
                principal_type,
                display_name,
                status
            )
            VALUES (
                @principal_id,
                'human',
                'Jack Tam',
                'active'
            );
            """,
            connection);
        command.Parameters.AddWithValue("principal_id", Guid.Parse(TestPrincipalId));

        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertSourceEventAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO events (
                id,
                principal_id,
                event_type,
                content,
                content_hash,
                retention_class,
                sensitivity,
                trust_level,
                scope_type,
                scope_id,
                scope_principal_id
            )
            VALUES (
                @event_id,
                @principal_id,
                'user_message',
                @content,
                'sha256:test',
                'standard',
                'none',
                'user_scoped',
                'user',
                @principal_id_text,
                @principal_id
            );
            """,
            connection);
        command.Parameters.AddWithValue("event_id", SourceEventId);
        command.Parameters.AddWithValue("principal_id", Guid.Parse(TestPrincipalId));
        command.Parameters.Add("content", NpgsqlDbType.Jsonb).Value = """{"message":"source"}""";
        command.Parameters.AddWithValue("principal_id_text", TestPrincipalId);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> CountIdempotencyRecordsAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand("SELECT count(*) FROM api_idempotency_keys;", connection);

        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<IdempotencyRecordState> ReadIdempotencyRecordAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT endpoint, idempotency_key, response_status, resource_type, resource_id
            FROM api_idempotency_keys;
            """,
            connection);

        await using var reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());

        return new IdempotencyRecordState(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetInt32(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetGuid(4));
    }

    private sealed record IdempotencyRecordState(
        string Endpoint,
        string IdempotencyKey,
        int ResponseStatus,
        string? ResourceType,
        Guid? ResourceId);
}
