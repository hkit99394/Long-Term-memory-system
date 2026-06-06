using System.Net;
using System.Text;
using MemorySystem.Application.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MemorySystem.IntegrationTests;

public sealed class ApiRateLimitingTests
{
    private const string ApiKeyA = "test-api-key-a";
    private const string ApiKeyB = "test-api-key-b";
    private const string PrincipalId = "11111111-1111-4111-8111-111111111111";

    [Fact]
    public async Task Rate_limiter_partitions_authenticated_requests_by_api_key()
    {
        using var factory = CreateFactory(defaultPermitLimit: 1);
        using var client = factory.CreateClient();

        using var firstKeyA = CreateRequest(ApiKeyA);
        using var firstKeyB = CreateRequest(ApiKeyB);
        using var secondKeyA = CreateRequest(ApiKeyA);

        using var firstKeyAResponse = await client.SendAsync(firstKeyA);
        using var firstKeyBResponse = await client.SendAsync(firstKeyB);
        using var secondKeyAResponse = await client.SendAsync(secondKeyA);

        Assert.Equal(HttpStatusCode.OK, firstKeyAResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, firstKeyBResponse.StatusCode);
        Assert.Equal((HttpStatusCode)429, secondKeyAResponse.StatusCode);
    }

    [Fact]
    public async Task Health_and_root_endpoints_are_not_rate_limited()
    {
        using var factory = CreateFactory(defaultPermitLimit: 1);
        using var client = factory.CreateClient();

        using var first = CreateRequest(ApiKeyA);
        using var second = CreateRequest(ApiKeyA);

        using var firstResponse = await client.SendAsync(first);
        using var secondResponse = await client.SendAsync(second);
        using var healthResponse = await client.GetAsync("/health/live");
        using var rootResponse = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal((HttpStatusCode)429, secondResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, healthResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, rootResponse.StatusCode);
    }

    [Fact]
    public async Task Rate_limiter_throttles_invalid_key_attempts_by_remote_partition()
    {
        using var factory = CreateFactory(defaultPermitLimit: 1);
        using var client = factory.CreateClient();

        using var first = CreateRequest("unknown-api-key");
        using var second = CreateRequest("unknown-api-key");

        using var firstResponse = await client.SendAsync(first);
        using var secondResponse = await client.SendAsync(second);

        Assert.Equal(HttpStatusCode.Unauthorized, firstResponse.StatusCode);
        Assert.Equal((HttpStatusCode)429, secondResponse.StatusCode);
    }

    private static HttpRequestMessage CreateRequest(string apiKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/__test/json-body/widgets")
        {
            Content = new StringContent("""{"value":"alpha"}""", Encoding.UTF8, "application/json")
        };

        request.Headers.Add("X-Api-Key", apiKey);
        return request;
    }

    private static WebApplicationFactory<Program> CreateFactory(int defaultPermitLimit)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, configurationBuilder) =>
                {
                    configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Postgres"] =
                            "Host=unused;Database=unused;Username=unused;Password=unused",
                        ["RateLimiting:Default:PermitLimit"] = defaultPermitLimit.ToString(),
                        ["RateLimiting:Default:WindowSeconds"] = "60",
                        ["Authentication:ApiKey:Keys:key-a:Key"] = ApiKeyA,
                        ["Authentication:ApiKey:Keys:key-a:PrincipalId"] = PrincipalId,
                        ["Authentication:ApiKey:Keys:key-a:DisplayName"] = "Test API caller A",
                        ["Authentication:ApiKey:Keys:key-b:Key"] = ApiKeyB,
                        ["Authentication:ApiKey:Keys:key-b:PrincipalId"] = PrincipalId,
                        ["Authentication:ApiKey:Keys:key-b:DisplayName"] = "Test API caller B"
                    });
                });
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton<IPrincipalResolver>(new AlwaysActivePrincipalResolver());
                });
            });
    }

    private sealed class AlwaysActivePrincipalResolver : IPrincipalResolver
    {
        public Task<AuthenticatedPrincipal?> ResolveApiKeyAsync(
            ApiKeyPrincipalResolutionRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<AuthenticatedPrincipal?>(
                new AuthenticatedPrincipal(
                    request.PrincipalId,
                    "human",
                    request.DisplayName,
                    AuthenticationMethods.ApiKey,
                    request.ApiKeyId));
        }

        public Task<AuthenticatedPrincipal?> ResolveIdentityBindingAsync(
            IdentityBindingLookup lookup,
            string authMethod = AuthenticationMethods.Oidc,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<AuthenticatedPrincipal?>(null);
        }
    }
}
