using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MemorySystem.Api.Authentication;
using MemorySystem.Application.Authentication;
using MemorySystem.Infrastructure.MemoryEmbeddings;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.IntegrationTests;

public sealed class ApiOidcAuthenticationTests
{
    private const string Issuer = "https://issuer.example.test";
    private const string Audience = "memory-system-api";
    private const string Subject = "external-subject-123";
    private const string ProductionSafeApiKey = "production-api-key-0123456789abcdef";
    private const string ProductionSafeOpenAiKey = "production-openai-key-0123456789abcdef";

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Oidc_bearer_authenticates_active_human_identity_binding()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_oidc_success_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApiDatabaseTestSupport.ApplyMigrationsAsync(databaseConnectionString);
            var principalId = Guid.NewGuid();
            var bindingId = Guid.NewGuid();
            await ApiDatabaseTestSupport.InsertPrincipalAsync(
                databaseConnectionString,
                principalId,
                principalType: "human",
                displayName: "OIDC Pilot User");
            await InsertIdentityBindingAsync(databaseConnectionString, bindingId, principalId, Subject);

            using var tokenFactory = new TestOidcTokenFactory();
            using var factory = CreateOidcFactory(databaseConnectionString, tokenFactory.Jwks);

            var payload = await GetFallbackAuthPayloadAsync(factory, tokenFactory.CreateToken());

            Assert.True(payload.GetProperty("authenticated").GetBoolean());
            Assert.Equal(OidcAuthenticationDefaults.AuthenticationScheme, payload.GetProperty("scheme").GetString());
            Assert.Equal(principalId.ToString(), payload.GetProperty("nameIdentifier").GetString());
            Assert.Equal("OIDC Pilot User", payload.GetProperty("name").GetString());
            Assert.Equal(principalId.ToString(), payload.GetProperty("principalId").GetString());
            Assert.Equal("human", payload.GetProperty("principalType").GetString());
            Assert.Equal(AuthenticationMethods.Oidc, payload.GetProperty("authMethod").GetString());
            Assert.Equal(bindingId.ToString(), payload.GetProperty("credentialId").GetString());
            Assert.Equal(Issuer, payload.GetProperty("externalIssuer").GetString());
            Assert.Equal(Subject, payload.GetProperty("externalSubject").GetString());
            Assert.Equal(JsonValueKind.Null, payload.GetProperty("apiKeyId").ValueKind);
            Assert.NotNull(await ReadIdentityBindingLastSeenAsync(databaseConnectionString, bindingId));
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
    public async Task Oidc_bearer_rejects_unbound_subject()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_oidc_unbound_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApiDatabaseTestSupport.ApplyMigrationsAsync(databaseConnectionString);

            using var tokenFactory = new TestOidcTokenFactory();
            using var factory = CreateOidcFactory(databaseConnectionString, tokenFactory.Jwks);
            var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new("Bearer", tokenFactory.CreateToken());

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
    public async Task Oidc_bearer_rejects_disabled_identity_binding()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_oidc_disabled_binding_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApiDatabaseTestSupport.ApplyMigrationsAsync(databaseConnectionString);
            var principalId = Guid.NewGuid();
            await ApiDatabaseTestSupport.InsertPrincipalAsync(databaseConnectionString, principalId);
            await InsertIdentityBindingAsync(
                databaseConnectionString,
                Guid.NewGuid(),
                principalId,
                Subject,
                status: "disabled");

            using var tokenFactory = new TestOidcTokenFactory();
            using var factory = CreateOidcFactory(databaseConnectionString, tokenFactory.Jwks);
            var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new("Bearer", tokenFactory.CreateToken());

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
    public async Task Oidc_bearer_rejects_non_human_principal_binding()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_oidc_service_principal_test_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            await ApiDatabaseTestSupport.ApplyMigrationsAsync(databaseConnectionString);
            var principalId = Guid.NewGuid();
            await ApiDatabaseTestSupport.InsertPrincipalAsync(
                databaseConnectionString,
                principalId,
                principalType: "service",
                displayName: "Service Principal");
            await InsertIdentityBindingAsync(databaseConnectionString, Guid.NewGuid(), principalId, Subject);

            using var tokenFactory = new TestOidcTokenFactory();
            using var factory = CreateOidcFactory(databaseConnectionString, tokenFactory.Jwks);
            var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new("Bearer", tokenFactory.CreateToken());

            using var response = await client.GetAsync("/__test/auth/fallback");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    [Fact]
    public async Task Oidc_bearer_rejects_wrong_audience_before_identity_lookup()
    {
        using var tokenFactory = new TestOidcTokenFactory();
        using var factory = CreateOidcFactory(
            "Host=unused;Database=unused;Username=unused;Password=unused",
            tokenFactory.Jwks,
            new ThrowingPrincipalResolver());
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokenFactory.CreateToken(audience: "other-api"));

        using var response = await client.GetAsync("/__test/auth/fallback");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Oidc_bearer_rejects_expired_token_before_identity_lookup()
    {
        using var tokenFactory = new TestOidcTokenFactory();
        using var factory = CreateOidcFactory(
            "Host=unused;Database=unused;Username=unused;Password=unused",
            tokenFactory.Jwks,
            new ThrowingPrincipalResolver(),
            new Dictionary<string, string?>
            {
                ["Authentication:Oidc:ClockSkewSeconds"] = "0"
            });
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new(
            "Bearer",
            tokenFactory.CreateToken(expiresAt: DateTimeOffset.UtcNow.AddMinutes(-1)));

        using var response = await client.GetAsync("/__test/auth/fallback");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Oidc_bearer_rejects_unavailable_jwks_before_identity_lookup()
    {
        using var tokenFactory = new TestOidcTokenFactory();
        using var factory = CreateOidcFactory(
            "Host=unused;Database=unused;Username=unused;Password=unused",
            tokenFactory.Jwks,
            principalResolver: new ThrowingPrincipalResolver(),
            jwksProvider: new ThrowingOidcJwksProvider());
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokenFactory.CreateToken());

        using var response = await client.GetAsync("/__test/auth/fallback");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public void Production_host_rejects_http_oidc_jwks_when_https_metadata_is_required()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production");
                builder.ConfigureAppConfiguration((_, configurationBuilder) =>
                {
                    configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Postgres"] =
                            "Host=prod-db;Database=memory_prod;Username=memory_user;Password=prod-password",
                        ["ForwardedHeaders:KnownProxies:0"] = "10.0.0.10",
                        ["Embeddings:Provider"] = MemoryEmbeddingOptions.OpenAiProvider,
                        ["Embeddings:ApiKey"] = ProductionSafeOpenAiKey,
                        ["Authentication:ApiKey:Keys:test-key:Key"] = ProductionSafeApiKey,
                        ["Authentication:ApiKey:Keys:test-key:PrincipalId"] =
                            "11111111-1111-1111-1111-111111111111",
                        ["Authentication:Oidc:Enabled"] = "true",
                        ["Authentication:Oidc:Issuer"] = Issuer,
                        ["Authentication:Oidc:Audience"] = Audience,
                        ["Authentication:Oidc:JwksUri"] = "http://issuer.example.test/.well-known/jwks.json",
                        ["Authentication:Oidc:RequireHttpsMetadata"] = "true"
                    });
                });
            });

        var exception = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains("HTTPS", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Http_oidc_jwks_provider_caches_jwks_by_uri()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        using var handler = new CountingJwksHandler(
            """
            {
              "keys": [
                {
                  "kty": "RSA",
                  "kid": "cached-key",
                  "alg": "RS256",
                  "n": "AQAB",
                  "e": "AQAB"
                }
              ]
            }
            """);
        using var httpClient = new HttpClient(handler);
        var provider = new HttpOidcJwksProvider(httpClient, memoryCache);
        var options = new OidcAuthenticationOptions
        {
            Enabled = true,
            Issuer = Issuer,
            Audience = Audience,
            JwksUri = "https://issuer.example.test/.well-known/jwks.json"
        };

        var first = await provider.GetJwksAsync(options);
        var second = await provider.GetJwksAsync(options);

        Assert.Equal(1, handler.RequestCount);
        Assert.Single(first.Keys);
        Assert.Single(second.Keys);
        Assert.Equal("cached-key", second.Keys[0].KeyId);
    }

    private static async Task<JsonElement> GetFallbackAuthPayloadAsync(
        WebApplicationFactory<Program> factory,
        string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        using var response = await client.GetAsync("/__test/auth/fallback");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }

    private static WebApplicationFactory<Program> CreateOidcFactory(
        string postgresConnectionString,
        OidcJwksDocument jwks,
        IPrincipalResolver? principalResolver = null,
        Dictionary<string, string?>? additionalConfiguration = null,
        IOidcJwksProvider? jwksProvider = null)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, configurationBuilder) =>
                {
                    var configuration = new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Postgres"] = postgresConnectionString,
                        ["Authentication:Oidc:Enabled"] = "true",
                        ["Authentication:Oidc:Issuer"] = Issuer,
                        ["Authentication:Oidc:Audience"] = Audience,
                        ["Authentication:Oidc:JwksUri"] = "https://issuer.example.test/.well-known/jwks.json",
                        ["Authentication:Oidc:ClockSkewSeconds"] = "30"
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
                    services.AddSingleton(jwksProvider ?? new StaticOidcJwksProvider(jwks));
                    if (principalResolver is not null)
                    {
                        services.AddSingleton(principalResolver);
                    }
                });
            });
    }

    private static async Task InsertIdentityBindingAsync(
        string connectionString,
        Guid bindingId,
        Guid principalId,
        string subject,
        string status = "active")
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO identity_bindings (
                id,
                provider,
                issuer,
                subject,
                principal_id,
                status,
                external_display_name,
                external_email,
                external_tenant_id,
                provider_metadata
            )
            VALUES (
                @binding_id,
                'oidc',
                @issuer,
                @subject,
                @principal_id,
                @status,
                'External OIDC User',
                'oidc.user@example.test',
                'tenant-123',
                @provider_metadata
            );
            """,
            connection);

        command.Parameters.AddWithValue("binding_id", bindingId);
        command.Parameters.AddWithValue("issuer", Issuer);
        command.Parameters.AddWithValue("subject", subject);
        command.Parameters.AddWithValue("principal_id", principalId);
        command.Parameters.AddWithValue("status", status);
        command.Parameters.Add("provider_metadata", NpgsqlDbType.Jsonb).Value = """{"source":"oidc-test"}""";

        await command.ExecuteNonQueryAsync();
    }

    private static async Task<DateTimeOffset?> ReadIdentityBindingLastSeenAsync(
        string connectionString,
        Guid bindingId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT last_seen_at
            FROM identity_bindings
            WHERE id = @binding_id;
            """,
            connection);
        command.Parameters.AddWithValue("binding_id", bindingId);

        var value = await command.ExecuteScalarAsync();
        return value switch
        {
            null or DBNull => null,
            DateTimeOffset dateTimeOffset => dateTimeOffset,
            DateTime dateTime => new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)),
            _ => throw new InvalidOperationException("Unexpected last-seen timestamp value returned from PostgreSQL.")
        };
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

    private sealed class StaticOidcJwksProvider(OidcJwksDocument jwks) : IOidcJwksProvider
    {
        public Task<OidcJwksDocument> GetJwksAsync(
            OidcAuthenticationOptions options,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(jwks);
        }
    }

    private sealed class ThrowingOidcJwksProvider : IOidcJwksProvider
    {
        public Task<OidcJwksDocument> GetJwksAsync(
            OidcAuthenticationOptions options,
            CancellationToken cancellationToken = default)
        {
            throw new HttpRequestException("JWKS endpoint is unavailable.");
        }
    }

    private sealed class CountingJwksHandler(string jwksJson) : HttpMessageHandler, IDisposable
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jwksJson, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class ThrowingPrincipalResolver : IPrincipalResolver
    {
        public Task<AuthenticatedPrincipal?> ResolveApiKeyAsync(
            ApiKeyPrincipalResolutionRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Principal resolver should not be called.");
        }

        public Task<AuthenticatedPrincipal?> ResolveIdentityBindingAsync(
            IdentityBindingLookup lookup,
            string authMethod = AuthenticationMethods.Oidc,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Principal resolver should not be called.");
        }
    }

    private sealed class TestOidcTokenFactory : IDisposable
    {
        private const string KeyId = "test-rsa-key";
        private readonly RSA rsa = RSA.Create(2048);

        public OidcJwksDocument Jwks
        {
            get
            {
                var parameters = rsa.ExportParameters(false);
                return new OidcJwksDocument(
                    [
                        new OidcJsonWebKey(
                            KeyId,
                            "RSA",
                            "RS256",
                            Base64UrlEncode(parameters.Modulus!),
                            Base64UrlEncode(parameters.Exponent!))
                    ]);
            }
        }

        public string CreateToken(
            string issuer = Issuer,
            string audience = Audience,
            string subject = Subject,
            DateTimeOffset? expiresAt = null,
            DateTimeOffset? notBefore = null)
        {
            var now = DateTimeOffset.UtcNow;
            var header = new Dictionary<string, object>
            {
                ["alg"] = "RS256",
                ["typ"] = "JWT",
                ["kid"] = KeyId
            };
            var payload = new Dictionary<string, object>
            {
                ["iss"] = issuer,
                ["aud"] = audience,
                ["sub"] = subject,
                ["iat"] = now.ToUnixTimeSeconds(),
                ["nbf"] = (notBefore ?? now.AddMinutes(-1)).ToUnixTimeSeconds(),
                ["exp"] = (expiresAt ?? now.AddMinutes(10)).ToUnixTimeSeconds()
            };

            var encodedHeader = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(header));
            var encodedPayload = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload));
            var signingInput = $"{encodedHeader}.{encodedPayload}";
            var signature = rsa.SignData(
                Encoding.ASCII.GetBytes(signingInput),
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            return $"{signingInput}.{Base64UrlEncode(signature)}";
        }

        public void Dispose()
        {
            rsa.Dispose();
        }

        private static string Base64UrlEncode(byte[] value)
        {
            return Convert.ToBase64String(value)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }
    }
}
