using MemorySystem.Application.Authentication;
using Microsoft.AspNetCore.Hosting;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace MemorySystem.IntegrationTests;

public sealed class ApiIdempotencyTests
{
    private const string TestApiKey = "test-api-key";
    private const string TestPrincipalId = "11111111-1111-1111-1111-111111111111";
    private const string SecondApiKey = "second-test-api-key";
    private const string SecondPrincipalId = "22222222-2222-2222-2222-222222222222";
    private const string TestEndpoint = "POST /__test/idempotency/widgets";

    [Fact]
    public async Task Invalid_idempotency_key_variants_use_generic_problem_title()
    {
        using var factory = CreateNoDatabaseFactory();
        using var client = factory.CreateClient();
        using var request = CreateRequest(TestApiKey, new string('a', 201), """{"value":"alpha"}""");

        using var response = await client.SendAsync(request);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Invalid idempotency key.", document.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Oversized_mutating_request_body_returns_payload_too_large_before_idempotency_store()
    {
        using var factory = CreateNoDatabaseFactory(new Dictionary<string, string?>
        {
            ["ApiIdempotency:MaxBodyBytes"] = "12"
        });
        using var client = factory.CreateClient();
        using var request = CreateRequest(TestApiKey, "oversized-body-key", """{"value":"alpha"}""");

        using var response = await client.SendAsync(request);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal((HttpStatusCode)413, response.StatusCode);
        Assert.Equal("Request body is too large.", document.RootElement.GetProperty("title").GetString());
    }

    [DatabaseFact]
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

    [DatabaseFact]
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
            Assert.Equal("application/json; charset=utf-8", record.ResponseContentType);
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

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Problem_details_response_preserves_content_type_on_first_response_and_replay()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_idempotency_problem_content_type_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString, Guid.Parse(TestPrincipalId));

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();
            const string idempotencyKey = "problem-content-type-key";

            using var firstRequest = CreateRequest(TestApiKey, idempotencyKey, """{"value":""}""");
            using var firstResponse = await client.SendAsync(firstRequest);
            using var secondRequest = CreateRequest(TestApiKey, idempotencyKey, """{"value":""}""");
            using var secondResponse = await client.SendAsync(secondRequest);
            var records = await ReadIdempotencyRecordsAsync(databaseConnectionString);

            Assert.Equal(HttpStatusCode.BadRequest, firstResponse.StatusCode);
            Assert.Equal("application/problem+json", firstResponse.Content.Headers.ContentType?.MediaType);
            Assert.Equal(HttpStatusCode.BadRequest, secondResponse.StatusCode);
            Assert.Equal("application/problem+json", secondResponse.Content.Headers.ContentType?.MediaType);

            var record = Assert.Single(records);
            Assert.Equal(400, record.ResponseStatus);
            Assert.Equal("application/problem+json; charset=utf-8", record.ResponseContentType);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
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

    [DatabaseFact]
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

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Expired_processing_idempotency_key_can_start_again()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_idempotency_expired_processing_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await PrepareDatabaseAsync(databaseConnectionString, Guid.Parse(TestPrincipalId));

            const string idempotencyKey = "expired-processing-key";
            const string body = """{"value":"alpha"}""";
            await InsertProcessingIdempotencyRecordAsync(
                databaseConnectionString,
                Guid.Parse(TestPrincipalId),
                idempotencyKey,
                ComputeRequestHash(body),
                DateTimeOffset.UtcNow.AddMinutes(-1));

            using var factory = CreateFactory(databaseConnectionString);
            using var client = factory.CreateClient();

            var payload = await SendWidgetAsync(client, TestApiKey, idempotencyKey, body);
            var records = await ReadIdempotencyRecordsAsync(databaseConnectionString);

            Assert.Equal("alpha", payload.GetProperty("value").GetString());

            var record = Assert.Single(records);
            Assert.Equal("completed", record.Status);
            Assert.Equal(201, record.ResponseStatus);
            Assert.True(record.ExpiresAt > DateTimeOffset.UtcNow);
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

    private static WebApplicationFactory<Program> CreateNoDatabaseFactory(
        Dictionary<string, string?>? additionalConfiguration = null)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, configurationBuilder) =>
                {
                    var configuration = new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Postgres"] =
                            "Host=unused;Database=unused;Username=unused;Password=unused",
                        ["Authentication:ApiKey:Keys:test-key:Key"] = TestApiKey,
                        ["Authentication:ApiKey:Keys:test-key:PrincipalId"] = TestPrincipalId,
                        ["Authentication:ApiKey:Keys:test-key:DisplayName"] = "Test API caller"
                    };

                    if (additionalConfiguration is not null)
                    {
                        foreach (var (key, value) in additionalConfiguration)
                        {
                            configuration[key] = value;
                        }
                    }

                    configurationBuilder.AddInMemoryCollection(configuration);
                });
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton<IApiKeyPrincipalValidator>(new AlwaysActiveApiKeyPrincipalValidator());
                });
            });
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

    private static async Task InsertProcessingIdempotencyRecordAsync(
        string connectionString,
        Guid principalId,
        string idempotencyKey,
        string requestHash,
        DateTimeOffset expiresAt)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO api_idempotency_keys (
                id,
                principal_id,
                endpoint,
                idempotency_key,
                request_hash,
                status,
                expires_at
            )
            VALUES (
                @id,
                @principal_id,
                @endpoint,
                @idempotency_key,
                @request_hash,
                'processing',
                @expires_at
            );
            """,
            connection);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("principal_id", principalId);
        command.Parameters.AddWithValue("endpoint", TestEndpoint);
        command.Parameters.AddWithValue("idempotency_key", idempotencyKey);
        command.Parameters.AddWithValue("request_hash", requestHash);
        command.Parameters.AddWithValue("expires_at", expiresAt);

        await command.ExecuteNonQueryAsync();
    }

    private static string ComputeRequestHash(string body)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(body));

        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    private sealed class AlwaysActiveApiKeyPrincipalValidator : IApiKeyPrincipalValidator
    {
        public Task<bool> IsActiveAsync(Guid principalId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }
    }
}
