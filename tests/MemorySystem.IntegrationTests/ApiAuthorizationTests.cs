using MemorySystem.Api.Authentication;
using MemorySystem.Application.Authentication;
using MemorySystem.Infrastructure.Migrations;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;

namespace MemorySystem.IntegrationTests;

public sealed class ApiAuthorizationTests
{
    private const string TestApiKey = "test-api-key";
    private const string TestPrincipalId = "11111111-1111-1111-1111-111111111111";
    private const string SecondApiKey = "second-test-api-key";
    private const string SecondPrincipalId = "22222222-2222-2222-2222-222222222222";

    [Fact]
    public async Task Root_endpoint_remains_anonymous()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        using var response = await client.GetAsync("/");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Hello World!", body);
    }

    [Fact]
    public async Task Fallback_authorization_policy_rejects_anonymous_requests()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        using var response = await client.GetAsync("/__test/auth/fallback");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Fallback_authorization_policy_accepts_configured_api_key()
    {
        using var factory = CreateFactory();
        var payload = await GetFallbackAuthPayloadAsync(factory, TestApiKey);

        Assert.True(payload.GetProperty("authenticated").GetBoolean());
        Assert.Equal("ApiKey", payload.GetProperty("scheme").GetString());
        Assert.Equal(TestPrincipalId, payload.GetProperty("nameIdentifier").GetString());
    }

    [Fact]
    public async Task Fallback_authorization_policy_maps_api_keys_to_distinct_principals()
    {
        using var factory = CreateFactory();

        var firstPayload = await GetFallbackAuthPayloadAsync(factory, TestApiKey);
        var secondPayload = await GetFallbackAuthPayloadAsync(factory, SecondApiKey);

        Assert.Equal(TestPrincipalId, firstPayload.GetProperty("nameIdentifier").GetString());
        Assert.Equal(SecondPrincipalId, secondPayload.GetProperty("nameIdentifier").GetString());
        Assert.NotEqual(
            firstPayload.GetProperty("nameIdentifier").GetString(),
            secondPayload.GetProperty("nameIdentifier").GetString());
    }

    [Fact]
    public async Task Fallback_authorization_policy_rejects_api_key_when_principal_is_not_active()
    {
        using var factory = CreateFactory(activePrincipalIds: [SecondPrincipalId]);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", TestApiKey);

        using var response = await client.GetAsync("/__test/auth/fallback");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Fallback_authorization_policy_surfaces_principal_validator_failures()
    {
        using var factory = CreateFactory(new ThrowingApiKeyPrincipalValidator());
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", TestApiKey);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GetAsync("/__test/auth/fallback"));

        Assert.Contains("principal store unavailable", exception.Message, StringComparison.Ordinal);
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Fallback_authorization_policy_accepts_api_key_for_active_database_principal()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();

        var databaseName = $"memorysystem_api_active_principal_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);
        var principalId = Guid.NewGuid();

        try
        {
            await SqlMigrationRunner.ApplyAsync(databaseConnectionString, MigrationTestPaths.FindMigrationsDirectory());
            await InsertPrincipalAsync(databaseConnectionString, principalId, status: "active");

            using var factory = CreateDatabaseBackedFactory(databaseConnectionString, principalId);
            var payload = await GetFallbackAuthPayloadAsync(factory, TestApiKey);

            Assert.Equal(principalId.ToString(), payload.GetProperty("nameIdentifier").GetString());
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Fallback_authorization_policy_rejects_api_key_for_disabled_database_principal()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();

        var databaseName = $"memorysystem_api_disabled_principal_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);
        var principalId = Guid.NewGuid();

        try
        {
            await SqlMigrationRunner.ApplyAsync(databaseConnectionString, MigrationTestPaths.FindMigrationsDirectory());
            await InsertPrincipalAsync(databaseConnectionString, principalId, status: "disabled");

            using var factory = CreateDatabaseBackedFactory(databaseConnectionString, principalId);
            var client = factory.CreateClient();
            client.DefaultRequestHeaders.Add("X-Api-Key", TestApiKey);

            using var response = await client.GetAsync("/__test/auth/fallback");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [Fact]
    public void Production_host_requires_api_key_configuration()
    {
        using var factory = CreateProductionFactory();

        var exception = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains("Authentication:ApiKey:Keys", exception.Message);
    }

    [Fact]
    public void Production_host_rejects_duplicate_api_key_values()
    {
        using var factory = CreateProductionFactory(new Dictionary<string, string?>
        {
            ["Authentication:ApiKey:Keys:first-key:Key"] = TestApiKey,
            ["Authentication:ApiKey:Keys:first-key:PrincipalId"] = TestPrincipalId,
            ["Authentication:ApiKey:Keys:second-key:Key"] = TestApiKey,
            ["Authentication:ApiKey:Keys:second-key:PrincipalId"] = SecondPrincipalId
        });

        var exception = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains("duplicate Key values", exception.Message);
    }

    [Fact]
    public void Production_host_rejects_malformed_api_key_principal_id()
    {
        using var factory = CreateProductionFactory(new Dictionary<string, string?>
        {
            ["Authentication:ApiKey:Keys:test-key:Key"] = TestApiKey,
            ["Authentication:ApiKey:Keys:test-key:PrincipalId"] = "not-a-guid"
        });

        var exception = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains("PrincipalId must be a valid GUID", exception.Message);
    }

    private static async Task<JsonElement> GetFallbackAuthPayloadAsync(WebApplicationFactory<Program> factory, string apiKey)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

        using var response = await client.GetAsync("/__test/auth/fallback");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }

    private static WebApplicationFactory<Program> CreateFactory(IEnumerable<string>? activePrincipalIds = null)
    {
        return CreateFactory(
            new TestApiKeyPrincipalValidator(
                activePrincipalIds is null
                    ? new HashSet<Guid> { Guid.Parse(TestPrincipalId), Guid.Parse(SecondPrincipalId) }
                    : activePrincipalIds.Select(Guid.Parse).ToHashSet()));
    }

    private static WebApplicationFactory<Program> CreateFactory(IApiKeyPrincipalValidator principalValidator)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, configurationBuilder) =>
                {
                    configurationBuilder.AddInMemoryCollection(CreateApiKeyConfiguration());
                });
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(principalValidator);
                });
            });
    }

    private static WebApplicationFactory<Program> CreateDatabaseBackedFactory(
        string postgresConnectionString,
        Guid principalId)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, configurationBuilder) =>
                {
                    var configuration = CreateApiKeyConfiguration(
                        principalId.ToString(),
                        displayName: "Database API caller",
                        includeSecondKey: false);
                    configuration["ConnectionStrings:Postgres"] = postgresConnectionString;

                    configurationBuilder.AddInMemoryCollection(configuration);
                });
            });
    }

    private static async Task InsertPrincipalAsync(string connectionString, Guid principalId, string status)
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
                'service',
                'API Principal',
                @status
            );
            """,
            connection);

        command.Parameters.AddWithValue("principal_id", principalId);
        command.Parameters.AddWithValue("status", status);

        await command.ExecuteNonQueryAsync();
    }

    private static WebApplicationFactory<Program> CreateProductionFactory(
        Dictionary<string, string?>? apiKeyConfiguration = null)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production");
                builder.ConfigureAppConfiguration((_, configurationBuilder) =>
                {
                    var configuration = new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Postgres"] =
                            "Host=prod-db;Database=memory_prod;Username=memory_user;Password=prod-password"
                    };

                    if (apiKeyConfiguration is not null)
                    {
                        foreach (var (key, value) in apiKeyConfiguration)
                        {
                            configuration[key] = value;
                        }
                    }

                    configurationBuilder.AddInMemoryCollection(configuration);
                });
            });
    }

    private static Dictionary<string, string?> CreateApiKeyConfiguration(
        string principalId = TestPrincipalId,
        string displayName = "Test API caller",
        bool includeSecondKey = true)
    {
        var configuration = new Dictionary<string, string?>
        {
            ["Authentication:ApiKey:Keys:test-key:Key"] = TestApiKey,
            ["Authentication:ApiKey:Keys:test-key:PrincipalId"] = principalId,
            ["Authentication:ApiKey:Keys:test-key:DisplayName"] = displayName
        };

        if (includeSecondKey)
        {
            configuration["Authentication:ApiKey:Keys:second-key:Key"] = SecondApiKey;
            configuration["Authentication:ApiKey:Keys:second-key:PrincipalId"] = SecondPrincipalId;
            configuration["Authentication:ApiKey:Keys:second-key:DisplayName"] = "Second API caller";
        }

        return configuration;
    }

    private sealed class TestApiKeyPrincipalValidator(IReadOnlySet<Guid> activePrincipalIds)
        : IApiKeyPrincipalValidator
    {
        public Task<bool> IsActiveAsync(Guid principalId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(activePrincipalIds.Contains(principalId));
        }
    }

    private sealed class ThrowingApiKeyPrincipalValidator : IApiKeyPrincipalValidator
    {
        public Task<bool> IsActiveAsync(Guid principalId, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("principal store unavailable");
        }
    }
}
