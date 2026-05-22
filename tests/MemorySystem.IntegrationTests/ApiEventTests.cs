using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace MemorySystem.IntegrationTests;

public sealed class ApiEventTests
{
    private const string TestApiKey = "test-api-key";
    private const string TestPrincipalId = "11111111-1111-4111-8111-111111111111";
    private const string TestOrgId = "22222222-2222-4222-8222-222222222222";
    private const string TestProjectId = "33333333-3333-4333-8333-333333333333";

    [Fact]
    [Trait("Category", "Database")]
    public async Task Post_events_stores_event_and_idempotency_record()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_events_append_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();
            var body = CreateUserEventBody("For planning, use concise decision logs.");

            var responsePayload = await SendEventAsync(client, "event-append-key", body);
            var eventId = responsePayload.GetProperty("id").GetGuid();
            var storedEvent = await ReadEventAsync(databaseConnectionString, eventId);
            var idempotencyRecord = await ReadIdempotencyRecordAsync(databaseConnectionString);

            Assert.Equal(Guid.Parse(TestPrincipalId), storedEvent.PrincipalId);
            Assert.Equal("user_message", storedEvent.EventType);
            Assert.StartsWith("sha256:", storedEvent.ContentHash, StringComparison.Ordinal);
            Assert.Equal("user_scoped", storedEvent.TrustLevel);
            Assert.Equal("standard", storedEvent.RetentionClass);
            Assert.Equal("none", storedEvent.Sensitivity);
            Assert.Equal("user", storedEvent.ScopeType);
            Assert.Equal(TestPrincipalId, storedEvent.ScopeId);
            Assert.Equal(Guid.Parse(TestPrincipalId), storedEvent.ScopePrincipalId);

            using var content = JsonDocument.Parse(storedEvent.ContentJson);
            Assert.Equal(
                "For planning, use concise decision logs.",
                content.RootElement.GetProperty("message").GetString());

            Assert.Equal("POST /api/events", idempotencyRecord.Endpoint);
            Assert.Equal("event-append-key", idempotencyRecord.IdempotencyKey);
            Assert.Equal("completed", idempotencyRecord.Status);
            Assert.Equal("event", idempotencyRecord.ResourceType);
            Assert.Equal(eventId, idempotencyRecord.ResourceId);
            Assert.Equal(201, idempotencyRecord.ResponseStatus);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task Post_events_replays_original_event_for_same_idempotency_key_and_body()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_events_replay_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();
            var body = CreateUserEventBody("Remember the first event id.");

            var firstPayload = await SendEventAsync(client, "event-replay-key", body);
            var secondPayload = await SendEventAsync(client, "event-replay-key", body);

            Assert.Equal(firstPayload.GetProperty("id").GetGuid(), secondPayload.GetProperty("id").GetGuid());
            Assert.Equal(1, await CountEventsAsync(databaseConnectionString));
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task Post_events_conflicts_when_idempotency_key_is_reused_with_different_body()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_events_conflict_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            await SendEventAsync(client, "event-conflict-key", CreateUserEventBody("First event."));
            using var conflictRequest = CreateRequest(
                "event-conflict-key",
                CreateUserEventBody("Different event."));

            using var response = await client.SendAsync(conflictRequest);

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal(1, await CountEventsAsync(databaseConnectionString));
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task Post_events_returns_bad_request_for_non_json_content_type()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_events_non_json_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();
            using var request = CreateRequest(
                "event-non-json-key",
                CreateUserEventBody("This should be rejected before JSON parsing."),
                "text/plain");

            using var response = await client.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();
            var idempotencyRecord = await ReadIdempotencyRecordAsync(databaseConnectionString);

            using var payload = JsonDocument.Parse(responseBody);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("Event request is invalid.", payload.RootElement.GetProperty("title").GetString());
            Assert.Contains("Content-Type", payload.RootElement.GetProperty("detail").GetString(), StringComparison.Ordinal);
            Assert.Equal(400, idempotencyRecord.ResponseStatus);
            Assert.Null(idempotencyRecord.ResourceType);
            Assert.Null(idempotencyRecord.ResourceId);
            Assert.Equal(0, await CountEventsAsync(databaseConnectionString));
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task Post_events_derives_project_scope_organization()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_events_project_scope_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString, createProject: true);

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();
            var body = $$"""
                {
                  "eventType": "user_message",
                  "scopeType": "project",
                  "scopeId": "{{TestProjectId}}",
                  "payload": {
                    "decision": "Use SQL-first migrations plus raw Npgsql."
                  }
                }
                """;

            var responsePayload = await SendEventAsync(client, "project-event-key", body);
            var storedEvent = await ReadEventAsync(databaseConnectionString, responsePayload.GetProperty("id").GetGuid());

            Assert.Equal("project", storedEvent.ScopeType);
            Assert.Equal(TestProjectId, storedEvent.ScopeId);
            Assert.Equal(Guid.Parse(TestOrgId), storedEvent.ScopeOrgId);
            Assert.Equal(Guid.Parse(TestProjectId), storedEvent.ScopeProjectId);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    private static string CreateUserEventBody(string message)
    {
        return $$"""
            {
              "eventType": "user_message",
              "principalId": "{{TestPrincipalId}}",
              "scopeType": "user",
              "scopeId": "{{TestPrincipalId}}",
              "payload": {
                "message": "{{message}}"
              }
            }
            """;
    }

    private static async Task<JsonElement> SendEventAsync(
        HttpClient client,
        string idempotencyKey,
        string body)
    {
        using var request = CreateRequest(idempotencyKey, body);
        using var response = await client.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var document = JsonDocument.Parse(responseBody);
        return document.RootElement.Clone();
    }

    private static HttpRequestMessage CreateRequest(
        string idempotencyKey,
        string body,
        string mediaType = "application/json")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/events")
        {
            Content = new StringContent(body, Encoding.UTF8, mediaType)
        };

        request.Headers.Add("X-Api-Key", TestApiKey);
        request.Headers.Add("Idempotency-Key", idempotencyKey);

        return request;
    }

    private static WebApplicationFactory<Program> CreateFactory(string postgresConnectionString)
    {
        return MemorySystemApiTestFactory.Create(postgresConnectionString, TestApiKey, TestPrincipalId);
    }

    private static async Task PrepareDatabaseAsync(string connectionString, bool createProject = false)
    {
        await ApiDatabaseTestSupport.ApplyMigrationsAsync(connectionString);
        await ApiDatabaseTestSupport.InsertPrincipalAsync(connectionString, Guid.Parse(TestPrincipalId));

        if (createProject)
        {
            await ApiDatabaseTestSupport.InsertOrganizationAndProjectAsync(
                connectionString,
                Guid.Parse(TestOrgId),
                Guid.Parse(TestProjectId));
        }
    }

    private static async Task<EventRecordState> ReadEventAsync(string connectionString, Guid eventId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT
                principal_id,
                event_type,
                content::text,
                content_hash,
                trust_level,
                retention_class,
                sensitivity,
                scope_type,
                scope_id,
                scope_org_id,
                scope_project_id,
                scope_principal_id,
                scope_role_id
            FROM events
            WHERE id = @event_id;
            """,
            connection);
        command.Parameters.AddWithValue("event_id", eventId);

        await using var reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());

        return new EventRecordState(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetString(6),
            reader.GetString(7),
            reader.GetString(8),
            reader.IsDBNull(9) ? null : reader.GetGuid(9),
            reader.IsDBNull(10) ? null : reader.GetGuid(10),
            reader.IsDBNull(11) ? null : reader.GetGuid(11),
            reader.IsDBNull(12) ? null : reader.GetString(12));
    }

    private static async Task<IdempotencyRecordState> ReadIdempotencyRecordAsync(string connectionString)
    {
        var record = await ApiDatabaseTestSupport.ReadSingleIdempotencySummaryAsync(connectionString);

        return new IdempotencyRecordState(
            record.Endpoint,
            record.IdempotencyKey,
            record.Status,
            record.ResponseStatus,
            record.ResourceType,
            record.ResourceId);
    }

    private static async Task<int> CountEventsAsync(string connectionString)
    {
        return await ApiDatabaseTestSupport.CountRowsAsync(connectionString, "events");
    }

    private sealed record EventRecordState(
        Guid PrincipalId,
        string EventType,
        string ContentJson,
        string ContentHash,
        string TrustLevel,
        string RetentionClass,
        string Sensitivity,
        string ScopeType,
        string ScopeId,
        Guid? ScopeOrgId,
        Guid? ScopeProjectId,
        Guid? ScopePrincipalId,
        string? ScopeRoleId);

    private sealed record IdempotencyRecordState(
        string Endpoint,
        string IdempotencyKey,
        string Status,
        int ResponseStatus,
        string? ResourceType,
        Guid? ResourceId);
}
