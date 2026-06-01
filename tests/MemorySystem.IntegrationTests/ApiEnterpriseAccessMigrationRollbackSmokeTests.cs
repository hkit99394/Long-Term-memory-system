using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MemorySystem.Api.Authentication;
using MemorySystem.Application.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NpgsqlTypes;

namespace MemorySystem.IntegrationTests;

public sealed class ApiEnterpriseAccessMigrationRollbackSmokeTests
{
    private const string Issuer = "https://issuer.example.test";
    private const string Audience = "memory-system-api";
    private const string Subject = "ea-08-human-subject";
    private const string HumanApiKey = "ea-08-human-api-key";
    private const string ServiceApiKey = "ea-08-service-api-key";
    private const string HumanApiKeyName = "ea-08-human-key";
    private const string ServiceApiKeyName = "ea-08-service-key";

    private static readonly Guid HumanPrincipalId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid ServicePrincipalId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid OrganizationId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid ProjectId = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly Guid SourceEventId = Guid.Parse("55555555-5555-4555-8555-555555555555");
    private static readonly Guid IdentityBindingId = Guid.Parse("66666666-6666-4666-8666-666666666666");
    private static readonly Guid ServiceCredentialId = Guid.Parse("77777777-7777-4777-8777-777777777777");

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Migration_and_rollback_smoke_proves_auth_modes_without_namespace_grant_drift()
    {
        var adminConnectionString = PostgresTestDatabase.RequireAdminConnectionString();
        var databaseName = $"memorysystem_ea08_migration_rollback_smoke_{Guid.NewGuid():N}";
        var databaseConnectionString = await PostgresTestDatabase.CreateAsync(adminConnectionString, databaseName);

        try
        {
            var (authorizedMemoryId, restrictedMemoryId) = await PrepareFixtureAsync(databaseConnectionString);
            var grantFingerprintBefore = await ReadMemoryAccessGrantFingerprintAsync(databaseConnectionString);

            using var tokenFactory = new TestOidcTokenFactory();
            var oidcToken = tokenFactory.CreateToken();

            using (var apiKeyOnly = CreateFactory(
                       databaseConnectionString,
                       tokenFactory.Jwks,
                       SmokeAuthMode.ApiKeyOnly))
            {
                await AssertApiKeyAuthenticatesAsync(
                    apiKeyOnly,
                    HumanApiKey,
                    HumanPrincipalId,
                    "human",
                    AuthenticationMethods.ApiKey,
                    HumanApiKeyName);
                await AssertBearerIsUnauthorizedAsync(apiKeyOnly, oidcToken);
                await AssertMemoryReadAsync(apiKeyOnly, AuthHeader.ApiKey(HumanApiKey), authorizedMemoryId, restrictedMemoryId);
            }

            using (var oidcOnly = CreateFactory(
                       databaseConnectionString,
                       tokenFactory.Jwks,
                       SmokeAuthMode.OidcOnly))
            {
                await AssertOidcAuthenticatesAsync(oidcOnly, oidcToken, HumanPrincipalId);
                await AssertApiKeyIsUnauthorizedAsync(oidcOnly, HumanApiKey);
                await AssertMemoryReadAsync(oidcOnly, AuthHeader.Bearer(oidcToken), authorizedMemoryId, restrictedMemoryId);
            }

            using (var dualAuth = CreateFactory(
                       databaseConnectionString,
                       tokenFactory.Jwks,
                       SmokeAuthMode.DualAuth))
            {
                var apiKeyPayload = await AssertApiKeyAuthenticatesAsync(
                    dualAuth,
                    HumanApiKey,
                    HumanPrincipalId,
                    "human",
                    AuthenticationMethods.ApiKey,
                    HumanApiKeyName);
                var oidcPayload = await AssertOidcAuthenticatesAsync(dualAuth, oidcToken, HumanPrincipalId);

                Assert.Equal(
                    apiKeyPayload.GetProperty("principalId").GetString(),
                    oidcPayload.GetProperty("principalId").GetString());
                await AssertMemoryReadAsync(dualAuth, AuthHeader.ApiKey(HumanApiKey), authorizedMemoryId, restrictedMemoryId);
                await AssertMemoryReadAsync(dualAuth, AuthHeader.Bearer(oidcToken), authorizedMemoryId, restrictedMemoryId);
            }

            using (var serviceAccount = CreateServiceAccountFactory(databaseConnectionString))
            {
                await AssertApiKeyAuthenticatesAsync(
                    serviceAccount,
                    ServiceApiKey,
                    ServicePrincipalId,
                    "service",
                    AuthenticationMethods.ApiKey,
                    ServiceCredentialId.ToString("D"));
                await AssertMemoryReadAsync(serviceAccount, AuthHeader.ApiKey(ServiceApiKey), authorizedMemoryId, restrictedMemoryId);
                Assert.True(await ServiceCredentialWasUsedAsync(databaseConnectionString));
            }

            using (var oidcDisabledRollback = CreateFactory(
                       databaseConnectionString,
                       tokenFactory.Jwks,
                       SmokeAuthMode.OidcDisabledRollback))
            {
                await AssertApiKeyAuthenticatesAsync(
                    oidcDisabledRollback,
                    HumanApiKey,
                    HumanPrincipalId,
                    "human",
                    AuthenticationMethods.ApiKey,
                    HumanApiKeyName);
                await AssertBearerIsUnauthorizedAsync(oidcDisabledRollback, oidcToken);
                await AssertMemoryReadAsync(
                    oidcDisabledRollback,
                    AuthHeader.ApiKey(HumanApiKey),
                    authorizedMemoryId,
                    restrictedMemoryId);
            }

            var grantFingerprintAfter = await ReadMemoryAccessGrantFingerprintAsync(databaseConnectionString);
            Assert.Equal(grantFingerprintBefore, grantFingerprintAfter);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(adminConnectionString, databaseName);
        }
    }

    private static async Task<(Guid AuthorizedMemoryId, Guid RestrictedMemoryId)> PrepareFixtureAsync(
        string connectionString)
    {
        await ApiDatabaseTestSupport.ApplyMigrationsAsync(connectionString);
        await ApiDatabaseTestSupport.InsertPrincipalAsync(
            connectionString,
            HumanPrincipalId,
            principalType: "human",
            displayName: "EA-08 Human Pilot");
        await ApiDatabaseTestSupport.InsertPrincipalAsync(
            connectionString,
            ServicePrincipalId,
            principalType: "service",
            displayName: "EA-08 Service Account");
        await ApiDatabaseTestSupport.InsertOrganizationAndProjectAsync(
            connectionString,
            OrganizationId,
            ProjectId,
            organizationName: "EA-08 Organization",
            projectName: "EA-08 Project");
        await ApiDatabaseTestSupport.InsertProjectMembershipAsync(connectionString, ProjectId, HumanPrincipalId, "reader");
        await ApiDatabaseTestSupport.InsertProjectMembershipAsync(connectionString, ProjectId, ServicePrincipalId, "reader");
        await ApiDatabaseTestSupport.InsertMemoryAccessGrantAsync(
            connectionString,
            $"/project/{ProjectId}/decisions",
            "read",
            principalId: HumanPrincipalId);
        await ApiDatabaseTestSupport.InsertMemoryAccessGrantAsync(
            connectionString,
            $"/project/{ProjectId}/decisions",
            "read",
            principalId: ServicePrincipalId);
        await ApiDatabaseTestSupport.InsertSourceEventAsync(
            connectionString,
            SourceEventId,
            HumanPrincipalId,
            "project",
            ProjectId.ToString(),
            scopeOrgId: OrganizationId,
            scopeProjectId: ProjectId);
        await InsertIdentityBindingAsync(connectionString);
        await InsertServiceAccountCredentialAsync(connectionString);

        var authorizedMemoryId = await InsertProjectMemoryFactAsync(
            connectionString,
            "EA-08 migration smoke allowed decision",
            "OIDC and API keys share one local grant boundary.",
            $"/project/{ProjectId}/decisions");
        var restrictedMemoryId = await InsertProjectMemoryFactAsync(
            connectionString,
            "EA-08 migration smoke restricted decision",
            "This should stay hidden without a namespace grant.",
            $"/project/{ProjectId}/restricted");

        return (authorizedMemoryId, restrictedMemoryId);
    }

    private static WebApplicationFactory<Program> CreateFactory(
        string connectionString,
        OidcJwksDocument jwks,
        SmokeAuthMode mode)
    {
        var configuration = CreateBaseConfiguration(connectionString);

        if (mode is SmokeAuthMode.ApiKeyOnly or SmokeAuthMode.DualAuth or SmokeAuthMode.OidcDisabledRollback)
        {
            AddHumanApiKeyConfiguration(configuration);
        }

        if (mode is SmokeAuthMode.OidcOnly or SmokeAuthMode.DualAuth or SmokeAuthMode.OidcDisabledRollback)
        {
            AddOidcConfiguration(configuration, enabled: mode is not SmokeAuthMode.OidcDisabledRollback);
        }

        return CreateFactory(connectionString, jwks, configuration);
    }

    private static WebApplicationFactory<Program> CreateServiceAccountFactory(string connectionString)
    {
        var configuration = CreateBaseConfiguration(connectionString);
        configuration[$"Authentication:ApiKey:Keys:{ServiceApiKeyName}:Key"] = ServiceApiKey;
        configuration[$"Authentication:ApiKey:Keys:{ServiceApiKeyName}:PrincipalId"] = ServicePrincipalId.ToString("D");
        configuration[$"Authentication:ApiKey:Keys:{ServiceApiKeyName}:DisplayName"] = "EA-08 Service Account";
        configuration[$"Authentication:ApiKey:Keys:{ServiceApiKeyName}:CredentialId"] = ServiceCredentialId.ToString("D");

        return CreateFactory(connectionString, jwks: new OidcJwksDocument([]), configuration);
    }

    private static WebApplicationFactory<Program> CreateFactory(
        string connectionString,
        OidcJwksDocument jwks,
        Dictionary<string, string?> configuration)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, configurationBuilder) =>
                {
                    configuration["ConnectionStrings:Postgres"] = connectionString;
                    configurationBuilder.AddInMemoryCollection(configuration);
                });
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton<IOidcJwksProvider>(new StaticOidcJwksProvider(jwks));
                });
            });
    }

    private static Dictionary<string, string?> CreateBaseConfiguration(string connectionString)
    {
        return new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = connectionString,
            ["MemorySystem:ContextProductBenchmark:LatestResultPath"] = Path.Combine(
                Path.GetTempPath(),
                $"memorysystem-ea08-context-product-benchmark-{Guid.NewGuid():N}.json")
        };
    }

    private static void AddHumanApiKeyConfiguration(Dictionary<string, string?> configuration)
    {
        configuration[$"Authentication:ApiKey:Keys:{HumanApiKeyName}:Key"] = HumanApiKey;
        configuration[$"Authentication:ApiKey:Keys:{HumanApiKeyName}:PrincipalId"] = HumanPrincipalId.ToString("D");
        configuration[$"Authentication:ApiKey:Keys:{HumanApiKeyName}:DisplayName"] = "EA-08 Human Pilot";
    }

    private static void AddOidcConfiguration(Dictionary<string, string?> configuration, bool enabled)
    {
        configuration["Authentication:Oidc:Enabled"] = enabled ? "true" : "false";
        configuration["Authentication:Oidc:Issuer"] = Issuer;
        configuration["Authentication:Oidc:Audience"] = Audience;
        configuration["Authentication:Oidc:JwksUri"] = "https://issuer.example.test/.well-known/jwks.json";
        configuration["Authentication:Oidc:ClockSkewSeconds"] = "30";
    }

    private static async Task<JsonElement> AssertApiKeyAuthenticatesAsync(
        WebApplicationFactory<Program> factory,
        string apiKey,
        Guid expectedPrincipalId,
        string expectedPrincipalType,
        string expectedAuthMethod,
        string expectedCredentialId)
    {
        var payload = await GetFallbackAuthPayloadAsync(factory, AuthHeader.ApiKey(apiKey));

        Assert.True(payload.GetProperty("authenticated").GetBoolean());
        Assert.Equal(ApiKeyAuthenticationDefaults.AuthenticationScheme, payload.GetProperty("scheme").GetString());
        Assert.Equal(expectedPrincipalId.ToString("D"), payload.GetProperty("principalId").GetString());
        Assert.Equal(expectedPrincipalType, payload.GetProperty("principalType").GetString());
        Assert.Equal(expectedAuthMethod, payload.GetProperty("authMethod").GetString());
        Assert.Equal(expectedCredentialId, payload.GetProperty("credentialId").GetString());

        return payload;
    }

    private static async Task<JsonElement> AssertOidcAuthenticatesAsync(
        WebApplicationFactory<Program> factory,
        string oidcToken,
        Guid expectedPrincipalId)
    {
        var payload = await GetFallbackAuthPayloadAsync(factory, AuthHeader.Bearer(oidcToken));

        Assert.True(payload.GetProperty("authenticated").GetBoolean());
        Assert.Equal(OidcAuthenticationDefaults.AuthenticationScheme, payload.GetProperty("scheme").GetString());
        Assert.Equal(expectedPrincipalId.ToString("D"), payload.GetProperty("principalId").GetString());
        Assert.Equal("human", payload.GetProperty("principalType").GetString());
        Assert.Equal(AuthenticationMethods.Oidc, payload.GetProperty("authMethod").GetString());
        Assert.Equal(IdentityBindingId.ToString("D"), payload.GetProperty("credentialId").GetString());
        Assert.Equal(Issuer, payload.GetProperty("externalIssuer").GetString());
        Assert.Equal(Subject, payload.GetProperty("externalSubject").GetString());

        return payload;
    }

    private static async Task<JsonElement> GetFallbackAuthPayloadAsync(
        WebApplicationFactory<Program> factory,
        AuthHeader authHeader)
    {
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/__test/auth/fallback");
        authHeader.Apply(request);

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }

    private static async Task AssertMemoryReadAsync(
        WebApplicationFactory<Program> factory,
        AuthHeader authHeader,
        Guid authorizedMemoryId,
        Guid restrictedMemoryId)
    {
        var client = factory.CreateClient();
        using var authorizedRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/memory/{authorizedMemoryId}");
        authHeader.Apply(authorizedRequest);

        using var authorizedResponse = await client.SendAsync(authorizedRequest);
        var authorizedBody = await authorizedResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, authorizedResponse.StatusCode);
        Assert.Contains("EA-08 migration smoke allowed decision", authorizedBody, StringComparison.Ordinal);

        using var restrictedRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/memory/{restrictedMemoryId}");
        authHeader.Apply(restrictedRequest);

        using var restrictedResponse = await client.SendAsync(restrictedRequest);
        var restrictedBody = await restrictedResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, restrictedResponse.StatusCode);
        Assert.DoesNotContain("EA-08 migration smoke restricted decision", restrictedBody, StringComparison.Ordinal);
    }

    private static async Task AssertApiKeyIsUnauthorizedAsync(WebApplicationFactory<Program> factory, string apiKey)
    {
        await AssertUnauthorizedAsync(factory, AuthHeader.ApiKey(apiKey));
    }

    private static async Task AssertBearerIsUnauthorizedAsync(WebApplicationFactory<Program> factory, string bearerToken)
    {
        await AssertUnauthorizedAsync(factory, AuthHeader.Bearer(bearerToken));
    }

    private static async Task AssertUnauthorizedAsync(WebApplicationFactory<Program> factory, AuthHeader authHeader)
    {
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/__test/auth/fallback");
        authHeader.Apply(request);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task InsertIdentityBindingAsync(string connectionString)
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
                'active',
                'EA-08 Human Pilot',
                'ea08.human@example.test',
                'tenant-ea08',
                @provider_metadata
            );
            """,
            connection);
        command.Parameters.AddWithValue("binding_id", IdentityBindingId);
        command.Parameters.AddWithValue("issuer", Issuer);
        command.Parameters.AddWithValue("subject", Subject);
        command.Parameters.AddWithValue("principal_id", HumanPrincipalId);
        command.Parameters.Add("provider_metadata", NpgsqlDbType.Jsonb).Value = """{"source":"ea-08-smoke"}""";

        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertServiceAccountCredentialAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO service_accounts (
                principal_id,
                owner_project_id,
                admin_contact,
                allowed_auth_method,
                expires_at,
                created_by_principal_id
            )
            VALUES (
                @service_principal_id,
                @project_id,
                'ea08-service-owner@example.test',
                'api_key',
                now() + interval '90 days',
                @human_principal_id
            );

            INSERT INTO service_account_credentials (
                id,
                service_principal_id,
                credential_label,
                auth_method,
                credential_fingerprint,
                status,
                expires_at,
                created_by_principal_id
            )
            VALUES (
                @service_credential_id,
                @service_principal_id,
                'ea-08-api-key',
                'api_key',
                'sha256:ea-08-api-key',
                'active',
                now() + interval '90 days',
                @human_principal_id
            );
            """,
            connection);
        command.Parameters.AddWithValue("service_principal_id", ServicePrincipalId);
        command.Parameters.AddWithValue("service_credential_id", ServiceCredentialId);
        command.Parameters.AddWithValue("project_id", ProjectId);
        command.Parameters.AddWithValue("human_principal_id", HumanPrincipalId);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task<Guid> InsertProjectMemoryFactAsync(
        string connectionString,
        string subject,
        string objectValue,
        string namespaceValue)
    {
        var memoryFactId = Guid.NewGuid();

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO memory_facts (
                id,
                scope_type,
                scope_id,
                namespace,
                project_id,
                org_id,
                memory_type,
                visibility,
                subject,
                predicate,
                object,
                confidence,
                trust_level,
                status,
                source_event_id,
                proposed_by_principal_id
            )
            VALUES (
                @memory_fact_id,
                'project',
                @project_id_text,
                @namespace,
                @project_id,
                @org_id,
                'decision',
                'project_shared',
                @subject,
                'uses',
                @object,
                0.950,
                'user_scoped',
                'active',
                @source_event_id,
                @principal_id
            );
            """,
            connection);
        command.Parameters.AddWithValue("memory_fact_id", memoryFactId);
        command.Parameters.AddWithValue("project_id_text", ProjectId.ToString("D"));
        command.Parameters.AddWithValue("namespace", namespaceValue);
        command.Parameters.AddWithValue("project_id", ProjectId);
        command.Parameters.AddWithValue("org_id", OrganizationId);
        command.Parameters.AddWithValue("subject", subject);
        command.Parameters.Add("object", NpgsqlDbType.Text).Value = objectValue;
        command.Parameters.AddWithValue("source_event_id", SourceEventId);
        command.Parameters.AddWithValue("principal_id", HumanPrincipalId);

        await command.ExecuteNonQueryAsync();

        return memoryFactId;
    }

    private static async Task<string> ReadMemoryAccessGrantFingerprintAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT coalesce(
                md5(string_agg(
                    id::text
                    || '|' || coalesce(principal_id::text, '')
                    || '|' || coalesce(role_id, '')
                    || '|' || namespace_prefix
                    || '|' || permission,
                    E'\n'
                    ORDER BY id
                )),
                md5('')
            )
            FROM memory_access_grants;
            """,
            connection);

        return (string)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Memory access grant fingerprint was not returned."));
    }

    private static async Task<bool> ServiceCredentialWasUsedAsync(string connectionString)
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
        command.Parameters.AddWithValue("service_credential_id", ServiceCredentialId);

        return await command.ExecuteScalarAsync() is true;
    }

    private enum SmokeAuthMode
    {
        ApiKeyOnly,
        OidcOnly,
        DualAuth,
        OidcDisabledRollback
    }

    private sealed record AuthHeader(string Name, string Value)
    {
        public static AuthHeader ApiKey(string value)
        {
            return new AuthHeader("X-Api-Key", value);
        }

        public static AuthHeader Bearer(string value)
        {
            return new AuthHeader("Authorization", $"Bearer {value}");
        }

        public void Apply(HttpRequestMessage request)
        {
            request.Headers.Add(Name, Value);
        }
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

    private sealed class TestOidcTokenFactory : IDisposable
    {
        private const string KeyId = "ea-08-rsa-key";
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

        public string CreateToken()
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
                ["iss"] = Issuer,
                ["aud"] = Audience,
                ["sub"] = Subject,
                ["iat"] = now.ToUnixTimeSeconds(),
                ["nbf"] = now.AddMinutes(-1).ToUnixTimeSeconds(),
                ["exp"] = now.AddMinutes(10).ToUnixTimeSeconds()
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
