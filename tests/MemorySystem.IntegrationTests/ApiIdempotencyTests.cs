using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MemorySystem.IntegrationTests;

public sealed class ApiIdempotencyTests
{
    private const string TestApiKey = "test-api-key";
    private const string TestPrincipalId = "11111111-1111-1111-1111-111111111111";
    private const string SecondApiKey = "second-test-api-key";
    private const string SecondPrincipalId = "22222222-2222-2222-2222-222222222222";
    private const string TestEndpoint = "POST /__test/idempotency/widgets";

    [Fact]
    [Trait("Category", "Database")]
    public async Task Mutating_endpoint_requires_idempotency_key()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_idempotency_missing_key_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString, Guid.Parse(TestPrincipalId));

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();
            using var request = CreateRequest(TestApiKey, idempotencyKey: null, """{"value":"alpha"}""");

            using var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(0, await CountIdempotencyRecordsAsync(databaseConnectionString));
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task Same_idempotency_key_and_request_hash_replays_original_response()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_idempotency_replay_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString, Guid.Parse(TestPrincipalId));

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();
            const string idempotencyKey = "replay-key";

            var firstPayload = await SendWidgetAsync(client, TestApiKey, idempotencyKey, """{"value":"alpha"}""");
            var secondPayload = await SendWidgetAsync(client, TestApiKey, idempotencyKey, """{"value":"alpha"}""");
            var records = await ReadIdempotencyRecordsAsync(databaseConnectionString);

            Assert.Equal(firstPayload.GetProperty("id").GetGuid(), secondPayload.GetProperty("id").GetGuid());
            Assert.Equal("alpha", secondPayload.GetProperty("value").GetString());

            var record = Assert.Single(records);
            Assert.Equal(Guid.Parse(TestPrincipalId), record.PrincipalId);
            Assert.Equal(TestEndpoint, record.Endpoint);
            Assert.Equal(idempotencyKey, record.IdempotencyKey);
            Assert.StartsWith("sha256:", record.RequestHash, StringComparison.Ordinal);
            Assert.Equal(71, record.RequestHash.Length);
            Assert.Equal(201, record.ResponseStatus);
            Assert.Equal("completed", record.Status);
            Assert.True(record.ExpiresAt > DateTimeOffset.UtcNow);

            using var storedResponse = JsonDocument.Parse(record.ResponseBody);
            Assert.Equal(firstPayload.GetProperty("id").GetGuid(), storedResponse.RootElement.GetProperty("id").GetGuid());
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task Same_idempotency_key_with_different_request_hash_returns_conflict()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_idempotency_conflict_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString, Guid.Parse(TestPrincipalId));

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();
            const string idempotencyKey = "conflict-key";

            await SendWidgetAsync(client, TestApiKey, idempotencyKey, """{"value":"alpha"}""");
            using var conflictRequest = CreateRequest(TestApiKey, idempotencyKey, """{"value":"beta"}""");

            using var response = await client.SendAsync(conflictRequest);
            var records = await ReadIdempotencyRecordsAsync(databaseConnectionString);

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

            var record = Assert.Single(records);
            Assert.Equal("completed", record.Status);

            using var storedResponse = JsonDocument.Parse(record.ResponseBody);
            Assert.Equal("alpha", storedResponse.RootElement.GetProperty("value").GetString());
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task Idempotency_key_scope_includes_principal()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_idempotency_principal_scope_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(
                databaseConnectionString,
                Guid.Parse(TestPrincipalId),
                Guid.Parse(SecondPrincipalId));

            using var factory = CreateFactory(databaseConnectionString, includeSecondKey: true);
            using var client = factory.CreateClient();
            const string idempotencyKey = "shared-client-key";

            var firstPayload = await SendWidgetAsync(client, TestApiKey, idempotencyKey, """{"value":"alpha"}""");
            var secondPayload = await SendWidgetAsync(client, SecondApiKey, idempotencyKey, """{"value":"alpha"}""");
            var records = await ReadIdempotencyRecordsAsync(databaseConnectionString);

            Assert.NotEqual(
                firstPayload.GetProperty("id").GetGuid(),
                secondPayload.GetProperty("id").GetGuid());
            Assert.Equal(2, records.Count);
            Assert.Contains(records, record => record.PrincipalId == Guid.Parse(TestPrincipalId));
            Assert.Contains(records, record => record.PrincipalId == Guid.Parse(SecondPrincipalId));
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    private static async Task PrepareDatabaseAsync(string connectionString, params Guid[] principalIds)
    {
        await ApiDatabaseTestSupport.ApplyMigrationsAsync(connectionString);

        foreach (var principalId in principalIds)
        {
            await ApiDatabaseTestSupport.InsertPrincipalAsync(
                connectionString,
                principalId,
                principalType: "service",
                displayName: "API Principal");
        }
    }

    private static async Task<JsonElement> SendWidgetAsync(
        HttpClient client,
        string apiKey,
        string idempotencyKey,
        string body)
    {
        using var request = CreateRequest(apiKey, idempotencyKey, body);
        using var response = await client.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var document = JsonDocument.Parse(responseBody);
        return document.RootElement.Clone();
    }

    private static HttpRequestMessage CreateRequest(string apiKey, string? idempotencyKey, string body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/__test/idempotency/widgets")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

        request.Headers.Add("X-Api-Key", apiKey);

        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        return request;
    }

    private static WebApplicationFactory<Program> CreateFactory(
        string postgresConnectionString,
        bool includeSecondKey = false)
    {
        var apiKeys = new List<ApiKeyConfiguration>
        {
            new("test-key", TestApiKey, TestPrincipalId, "Test API caller")
        };

        if (includeSecondKey)
        {
            apiKeys.Add(new ApiKeyConfiguration("second-key", SecondApiKey, SecondPrincipalId, "Second API caller"));
        }

        return MemorySystemApiTestFactory.Create(postgresConnectionString, apiKeys);
    }

    private static async Task<int> CountIdempotencyRecordsAsync(string connectionString)
    {
        return await ApiDatabaseTestSupport.CountIdempotencyRecordsAsync(connectionString);
    }

    private static async Task<IReadOnlyList<ApiIdempotencyRecordDetail>> ReadIdempotencyRecordsAsync(
        string connectionString)
    {
        return await ApiDatabaseTestSupport.ReadIdempotencyDetailsAsync(connectionString);
    }
}
