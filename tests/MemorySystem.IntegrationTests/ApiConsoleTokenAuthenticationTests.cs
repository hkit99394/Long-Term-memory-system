using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MemorySystem.Api.Authentication;
using MemorySystem.Application.AccessAuditing;
using MemorySystem.Application.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MemorySystem.IntegrationTests;

public sealed class ApiConsoleTokenAuthenticationTests
{
    private const string Issuer = "https://issuer.example.test";
    private const string Audience = "memory-system-api";
    private const string Subject = "console-subject-123";
    private const string BreakGlassApiKey = "break-glass-api-key-0123456789abcdef";

    [Fact]
    public async Task Console_static_entrypoints_load_without_cookie_session()
    {
        using var factory = CreateConsoleTokenFactory(new StaticPrincipalResolver());
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var adminResponse = await client.GetAsync("/admin/");
        using var reviewsResponse = await client.GetAsync("/reviews/");

        Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, reviewsResponse.StatusCode);
        Assert.False(adminResponse.Headers.TryGetValues("Set-Cookie", out _));
        Assert.False(reviewsResponse.Headers.TryGetValues("Set-Cookie", out _));
    }

    [Fact]
    public async Task Console_oidc_token_validation_accepts_active_human_identity_binding_without_issuing_cookie()
    {
        var principalId = Guid.NewGuid();
        var bindingId = Guid.NewGuid();
        using var tokenFactory = new TestOidcTokenFactory();
        using var factory = CreateConsoleTokenFactory(
            new StaticPrincipalResolver(
                oidcPrincipal: new AuthenticatedPrincipal(
                    principalId,
                    "human",
                    "Console User",
                    AuthenticationMethods.Oidc,
                    bindingId.ToString("D"),
                    Issuer,
                    Subject)),
            tokenFactory.Jwks);
        var client = factory.CreateClient();

        using var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/console/oidc-token",
            new
            {
                token = tokenFactory.CreateToken(),
                returnUrl = "/reviews/"
            });

        var tokenResult = await loginResponse.Content.ReadFromJsonAsync<ConsoleTokenPayload>();
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.False(loginResponse.Headers.TryGetValues("Set-Cookie", out _));
        Assert.NotNull(tokenResult);
        Assert.True(tokenResult.Authenticated);
        Assert.Equal("/reviews/", tokenResult.ReturnUrl);
        Assert.Equal("oidc_jwt", tokenResult.CredentialKind);
        Assert.Equal(AuthenticationMethods.Oidc, tokenResult.AuthMethod);

        client.DefaultRequestHeaders.Authorization = new("Bearer", tokenFactory.CreateToken());
        var authPayload = await ReadFallbackAuthPayloadAsync(client);
        Assert.Equal(OidcAuthenticationDefaults.AuthenticationScheme, authPayload.GetProperty("scheme").GetString());
        Assert.Equal(principalId.ToString(), authPayload.GetProperty("principalId").GetString());
        Assert.Equal(AuthenticationMethods.Oidc, authPayload.GetProperty("authMethod").GetString());
        Assert.Equal(bindingId.ToString(), authPayload.GetProperty("credentialId").GetString());
    }

    [Fact]
    public async Task Console_break_glass_key_validation_rejects_service_principals()
    {
        var principalId = Guid.NewGuid();
        using var factory = CreateConsoleTokenFactory(
            new StaticPrincipalResolver(
                apiKeyPrincipal: new AuthenticatedPrincipal(
                    principalId,
                    "service",
                    "Automation Service",
                    AuthenticationMethods.ApiKey,
                    "break-glass-key")),
            configuredApiKey: BreakGlassApiKey,
            configuredApiKeyPrincipalId: principalId.ToString("D"));
        var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/auth/console/break-glass-key",
            new
            {
                apiKey = BreakGlassApiKey,
                returnUrl = "/admin/"
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(response.Headers.TryGetValues("Set-Cookie", out _));
    }

    [Fact]
    public async Task Console_break_glass_key_validation_allows_human_principals_without_issuing_cookie()
    {
        var principalId = Guid.NewGuid();
        using var factory = CreateConsoleTokenFactory(
            new StaticPrincipalResolver(
                apiKeyPrincipal: new AuthenticatedPrincipal(
                    principalId,
                    "human",
                    "Break Glass Operator",
                    AuthenticationMethods.ApiKey,
                    "break-glass-key")),
            configuredApiKey: BreakGlassApiKey,
            configuredApiKeyPrincipalId: principalId.ToString("D"));
        var client = factory.CreateClient();

        using var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/console/break-glass-key",
            new
            {
                apiKey = BreakGlassApiKey,
                returnUrl = "/admin/"
            });

        var tokenResult = await loginResponse.Content.ReadFromJsonAsync<ConsoleTokenPayload>();
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.False(loginResponse.Headers.TryGetValues("Set-Cookie", out _));
        Assert.NotNull(tokenResult);
        Assert.Equal("break_glass_api_key", tokenResult.CredentialKind);

        client.DefaultRequestHeaders.Add("X-Api-Key", BreakGlassApiKey);
        var authPayload = await ReadFallbackAuthPayloadAsync(client);
        Assert.Equal(ApiKeyAuthenticationDefaults.AuthenticationScheme, authPayload.GetProperty("scheme").GetString());
        Assert.Equal(principalId.ToString(), authPayload.GetProperty("principalId").GetString());
        Assert.Equal(AuthenticationMethods.ApiKey, authPayload.GetProperty("authMethod").GetString());
    }

    private static async Task<JsonElement> ReadFallbackAuthPayloadAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/__test/auth/fallback");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }

    private static WebApplicationFactory<Program> CreateConsoleTokenFactory(
        IPrincipalResolver principalResolver,
        OidcJwksDocument? jwks = null,
        string? configuredApiKey = null,
        string? configuredApiKeyPrincipalId = null)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, configurationBuilder) =>
                {
                    var configuration = new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Postgres"] = "Host=unused;Database=unused;Username=unused;Password=unused",
                        ["Authentication:Oidc:Enabled"] = "true",
                        ["Authentication:Oidc:Issuer"] = Issuer,
                        ["Authentication:Oidc:Audience"] = Audience,
                        ["Authentication:Oidc:JwksUri"] = "https://issuer.example.test/.well-known/jwks.json",
                        ["Authentication:Oidc:ClockSkewSeconds"] = "30"
                    };

                    if (configuredApiKey is not null && configuredApiKeyPrincipalId is not null)
                    {
                        configuration["Authentication:ApiKey:Keys:break-glass:Key"] = configuredApiKey;
                        configuration["Authentication:ApiKey:Keys:break-glass:PrincipalId"] = configuredApiKeyPrincipalId;
                        configuration["Authentication:ApiKey:Keys:break-glass:DisplayName"] = "Break Glass";
                    }

                    configurationBuilder.AddInMemoryCollection(configuration);
                });
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(principalResolver);
                    services.AddSingleton<IOidcJwksProvider>(new StaticOidcJwksProvider(
                        jwks ?? new OidcJwksDocument([])));
                    services.AddSingleton<IAuthenticationAuditRecorder, NoopAuthenticationAuditRecorder>();
                });
            });
    }

    private sealed record ConsoleTokenPayload(
        bool Authenticated,
        string PrincipalId,
        string DisplayName,
        string PrincipalType,
        string AuthMethod,
        string CredentialId,
        string CredentialKind,
        string ReturnUrl);

    private sealed class StaticPrincipalResolver(
        AuthenticatedPrincipal? oidcPrincipal = null,
        AuthenticatedPrincipal? apiKeyPrincipal = null) : IPrincipalResolver
    {
        public Task<AuthenticatedPrincipal?> ResolveApiKeyAsync(
            ApiKeyPrincipalResolutionRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(apiKeyPrincipal);
        }

        public Task<AuthenticatedPrincipal?> ResolveIdentityBindingAsync(
            IdentityBindingLookup lookup,
            string authMethod = AuthenticationMethods.Oidc,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(oidcPrincipal);
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

    private sealed class NoopAuthenticationAuditRecorder : IAuthenticationAuditRecorder
    {
        public Task RecordAuthenticationAsync(
            HttpContext context,
            string scheme,
            string outcome,
            Guid? principalId = null,
            string? principalType = null,
            string? authMethod = null,
            string? credentialId = null,
            Guid? identityBindingId = null,
            string? reasonCode = null,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task RecordAuthorizationDeniedAsync(
            HttpContext context,
            string reasonCode,
            CancellationToken cancellationToken = default,
            string? resourceType = null,
            string? resourceId = null)
        {
            return Task.CompletedTask;
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
