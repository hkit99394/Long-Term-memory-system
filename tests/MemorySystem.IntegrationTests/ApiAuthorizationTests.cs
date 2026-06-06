using MemorySystem.Api.Authentication;
using MemorySystem.Application.Authentication;
using MemorySystem.Infrastructure.MemoryEmbeddings;
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
    private const string ProductionSafeApiKey = "production-api-key-0123456789abcdef";
    private const string SecondProductionSafeApiKey = "production-api-key-fedcba9876543210";
    private const string ProductionSafeOpenAiKey = "production-openai-key-0123456789abcdef";

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
        Assert.Equal("Test API caller", payload.GetProperty("name").GetString());
        Assert.Equal(TestPrincipalId, payload.GetProperty("principalId").GetString());
        Assert.Equal("human", payload.GetProperty("principalType").GetString());
        Assert.Equal(AuthenticationMethods.ApiKey, payload.GetProperty("authMethod").GetString());
        Assert.Equal("test-key", payload.GetProperty("credentialId").GetString());
        Assert.Equal("test-key", payload.GetProperty("apiKeyId").GetString());
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
    public async Task Fallback_authorization_policy_surfaces_principal_resolver_failures()
    {
        using var factory = CreateFactory(new ThrowingPrincipalResolver());
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
        var serviceCredentialId = Guid.NewGuid();

        try
        {
            await SqlMigrationRunner.ApplyAsync(databaseConnectionString, MigrationTestPaths.FindMigrationsDirectory());
            await InsertPrincipalAsync(databaseConnectionString, principalId, status: "active");
            await InsertServiceAccountCredentialAsync(databaseConnectionString, principalId, serviceCredentialId);

            using var factory = CreateDatabaseBackedFactory(databaseConnectionString, principalId, serviceCredentialId);
            var payload = await GetFallbackAuthPayloadAsync(factory, TestApiKey);

            Assert.Equal(principalId.ToString(), payload.GetProperty("nameIdentifier").GetString());
            Assert.Equal("Database API caller", payload.GetProperty("name").GetString());
            Assert.Equal(principalId.ToString(), payload.GetProperty("principalId").GetString());
            Assert.Equal("service", payload.GetProperty("principalType").GetString());
            Assert.Equal(AuthenticationMethods.ApiKey, payload.GetProperty("authMethod").GetString());
            Assert.Equal(serviceCredentialId.ToString(), payload.GetProperty("credentialId").GetString());
            Assert.Equal("test-key", payload.GetProperty("apiKeyId").GetString());
            Assert.True(await ServiceCredentialWasUsedAsync(databaseConnectionString, serviceCredentialId));
            Assert.Equal(
                1L,
                await CountAuthenticationAuditEventsAsync(databaseConnectionString, principalId, outcome: "succeeded"));
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Fallback_authorization_policy_rejects_api_key_for_disabled_service_credential()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();

        var databaseName = $"memorysystem_api_disabled_service_credential_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);
        var principalId = Guid.NewGuid();
        var serviceCredentialId = Guid.NewGuid();

        try
        {
            await SqlMigrationRunner.ApplyAsync(databaseConnectionString, MigrationTestPaths.FindMigrationsDirectory());
            await InsertPrincipalAsync(databaseConnectionString, principalId, status: "active");
            await InsertServiceAccountCredentialAsync(
                databaseConnectionString,
                principalId,
                serviceCredentialId,
                credentialStatus: "disabled");

            using var factory = CreateDatabaseBackedFactory(databaseConnectionString, principalId, serviceCredentialId);
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
            ["Authentication:ApiKey:Keys:first-key:Key"] = ProductionSafeApiKey,
            ["Authentication:ApiKey:Keys:first-key:PrincipalId"] = TestPrincipalId,
            ["Authentication:ApiKey:Keys:second-key:Key"] = ProductionSafeApiKey,
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
            ["Authentication:ApiKey:Keys:test-key:Key"] = ProductionSafeApiKey,
            ["Authentication:ApiKey:Keys:test-key:PrincipalId"] = "not-a-guid"
        });

        var exception = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains("PrincipalId must be a valid GUID", exception.Message);
    }

    [Theory]
    [InlineData(TestApiKey)]
    [InlineData("private-alpha-local-key")]
    public void Production_host_rejects_placeholder_api_key_values(string unsafeApiKey)
    {
        using var factory = CreateProductionFactory(new Dictionary<string, string?>
        {
            ["Authentication:ApiKey:Keys:test-key:Key"] = unsafeApiKey,
            ["Authentication:ApiKey:Keys:test-key:PrincipalId"] = TestPrincipalId
        });

        var exception = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains("production-safe", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Production_host_accepts_production_safe_api_key_values()
    {
        using var factory = CreateProductionFactory(new Dictionary<string, string?>
        {
            ["Authentication:ApiKey:Keys:test-key:Key"] = ProductionSafeApiKey,
            ["Authentication:ApiKey:Keys:test-key:PrincipalId"] = TestPrincipalId,
            ["Authentication:ApiKey:Keys:second-key:Key"] = SecondProductionSafeApiKey,
            ["Authentication:ApiKey:Keys:second-key:PrincipalId"] = SecondPrincipalId
        });

        using var client = factory.CreateClient();

        Assert.NotNull(client);
    }

    [Fact]
    public void Production_host_rejects_local_postgres_defaults()
    {
        using var factory = CreateProductionFactory(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] =
                "Host=localhost;Port=55432;Database=memory_system;Username=memory_system;Password=memory_system_dev_password",
            ["Authentication:ApiKey:Keys:test-key:Key"] = ProductionSafeApiKey,
            ["Authentication:ApiKey:Keys:test-key:PrincipalId"] = TestPrincipalId
        });

        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("Local Docker Compose PostgreSQL defaults", exception.Message);
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
            new TestPrincipalResolver(
                activePrincipalIds is null
                    ? new HashSet<Guid> { Guid.Parse(TestPrincipalId), Guid.Parse(SecondPrincipalId) }
                    : activePrincipalIds.Select(Guid.Parse).ToHashSet()));
    }

    private static WebApplicationFactory<Program> CreateFactory(IPrincipalResolver principalResolver)
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
                    services.AddSingleton(principalResolver);
                });
            });
    }

    private static WebApplicationFactory<Program> CreateDatabaseBackedFactory(
        string postgresConnectionString,
        Guid principalId,
        Guid? serviceCredentialId = null)
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
                        credentialId: serviceCredentialId?.ToString("D"),
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

    private static async Task InsertServiceAccountCredentialAsync(
        string connectionString,
        Guid principalId,
        Guid serviceCredentialId,
        string credentialStatus = "active")
    {
        var orgId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var isActive = string.Equals(credentialStatus, "active", StringComparison.Ordinal);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO organizations (id, name)
            VALUES (@org_id, 'API Auth Test Org');

            INSERT INTO projects (id, org_id, name, status)
            VALUES (@project_id, @org_id, 'API Auth Test Project', 'active');

            INSERT INTO service_accounts (
                principal_id,
                owner_project_id,
                admin_contact,
                allowed_auth_method,
                expires_at,
                created_by_principal_id
            )
            VALUES (
                @principal_id,
                @project_id,
                'api-auth-owner@example.test',
                'api_key',
                now() + interval '90 days',
                @principal_id
            );

            INSERT INTO service_account_credentials (
                id,
                service_principal_id,
                credential_label,
                auth_method,
                credential_fingerprint,
                status,
                expires_at,
                created_by_principal_id,
                disabled_by_principal_id,
                disabled_at,
                disable_reason
            )
            VALUES (
                @service_credential_id,
                @principal_id,
                'configured-api-key',
                'api_key',
                'sha256:configured-api-key',
                @credential_status,
                now() + interval '90 days',
                @principal_id,
                @disabled_by_principal_id,
                @disabled_at,
                @disable_reason
            );
            """,
            connection);

        command.Parameters.AddWithValue("org_id", orgId);
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("principal_id", principalId);
        command.Parameters.AddWithValue("service_credential_id", serviceCredentialId);
        command.Parameters.AddWithValue("credential_status", credentialStatus);
        command.Parameters.Add("disabled_by_principal_id", NpgsqlTypes.NpgsqlDbType.Uuid).Value =
            isActive ? DBNull.Value : principalId;
        command.Parameters.Add("disabled_at", NpgsqlTypes.NpgsqlDbType.TimestampTz).Value =
            isActive ? DBNull.Value : DateTimeOffset.UtcNow;
        command.Parameters.Add("disable_reason", NpgsqlTypes.NpgsqlDbType.Text).Value =
            isActive ? DBNull.Value : "test disabled credential";

        await command.ExecuteNonQueryAsync();
    }

    private static async Task<bool> ServiceCredentialWasUsedAsync(
        string connectionString,
        Guid serviceCredentialId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT last_used_at IS NOT NULL
            FROM service_account_credentials
            WHERE id = @service_credential_id;
            """,
            connection);
        command.Parameters.AddWithValue("service_credential_id", serviceCredentialId);

        return await command.ExecuteScalarAsync() is true;
    }

    private static async Task<long> CountAuthenticationAuditEventsAsync(
        string connectionString,
        Guid principalId,
        string outcome)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT count(*)
            FROM access_audit_events
            WHERE action_type = 'authentication'
                AND outcome = @outcome
                AND actor_principal_id = @principal_id;
            """,
            connection);
        command.Parameters.AddWithValue("outcome", outcome);
        command.Parameters.AddWithValue("principal_id", principalId);

        return (long)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Authentication audit count was not returned."));
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
                            "Host=prod-db;Database=memory_prod;Username=memory_user;Password=prod-password",
                        ["ForwardedHeaders:KnownProxies:0"] = "10.0.0.10",
                        ["Embeddings:Provider"] = MemoryEmbeddingOptions.OpenAiProvider,
                        ["Embeddings:ApiKey"] = ProductionSafeOpenAiKey
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
        string? credentialId = null,
        bool includeSecondKey = true)
    {
        var configuration = new Dictionary<string, string?>
        {
            ["Authentication:ApiKey:Keys:test-key:Key"] = TestApiKey,
            ["Authentication:ApiKey:Keys:test-key:PrincipalId"] = principalId,
            ["Authentication:ApiKey:Keys:test-key:DisplayName"] = displayName
        };

        if (!string.IsNullOrWhiteSpace(credentialId))
        {
            configuration["Authentication:ApiKey:Keys:test-key:CredentialId"] = credentialId;
        }

        if (includeSecondKey)
        {
            configuration["Authentication:ApiKey:Keys:second-key:Key"] = SecondApiKey;
            configuration["Authentication:ApiKey:Keys:second-key:PrincipalId"] = SecondPrincipalId;
            configuration["Authentication:ApiKey:Keys:second-key:DisplayName"] = "Second API caller";
        }

        return configuration;
    }

    private sealed class TestPrincipalResolver(IReadOnlySet<Guid> activePrincipalIds)
        : IPrincipalResolver
    {
        public Task<AuthenticatedPrincipal?> ResolveApiKeyAsync(
            ApiKeyPrincipalResolutionRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<AuthenticatedPrincipal?>(
                activePrincipalIds.Contains(request.PrincipalId)
                    ? new AuthenticatedPrincipal(
                        request.PrincipalId,
                        "human",
                        request.DisplayName,
                        AuthenticationMethods.ApiKey,
                        request.ApiKeyId)
                    : null);
        }

        public Task<AuthenticatedPrincipal?> ResolveIdentityBindingAsync(
            IdentityBindingLookup lookup,
            string authMethod = AuthenticationMethods.Oidc,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<AuthenticatedPrincipal?>(null);
        }
    }

    private sealed class ThrowingPrincipalResolver : IPrincipalResolver
    {
        public Task<AuthenticatedPrincipal?> ResolveApiKeyAsync(
            ApiKeyPrincipalResolutionRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("principal store unavailable");
        }

        public Task<AuthenticatedPrincipal?> ResolveIdentityBindingAsync(
            IdentityBindingLookup lookup,
            string authMethod = AuthenticationMethods.Oidc,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("principal store unavailable");
        }
    }
}
