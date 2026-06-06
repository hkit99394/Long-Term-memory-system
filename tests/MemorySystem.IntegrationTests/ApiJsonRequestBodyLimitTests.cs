using System.Net;
using System.Text;
using System.Text.Json;
using MemorySystem.Application.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MemorySystem.IntegrationTests;

public sealed class ApiJsonRequestBodyLimitTests
{
    private const string TestApiKey = "test-api-key";
    private const string TestPrincipalId = "11111111-1111-1111-1111-111111111111";

    [Fact]
    public async Task Json_body_reader_accepts_request_within_configured_limit()
    {
        using var factory = CreateFactory(maxBodyBytes: 64);
        using var client = factory.CreateClient();
        using var request = CreateRequest("""{"value":"alpha"}""");

        using var response = await client.SendAsync(request);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("alpha", document.RootElement.GetProperty("value").GetString());
    }

    [Fact]
    public async Task Json_body_reader_rejects_request_over_configured_limit()
    {
        using var factory = CreateFactory(maxBodyBytes: 12);
        using var client = factory.CreateClient();
        using var request = CreateRequest("""{"value":"alpha"}""");

        using var response = await client.SendAsync(request);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal((HttpStatusCode)413, response.StatusCode);
        Assert.Equal("JSON body test request is invalid.", document.RootElement.GetProperty("title").GetString());
        Assert.Contains("12 bytes or fewer", document.RootElement.GetProperty("detail").GetString());
    }

    private static HttpRequestMessage CreateRequest(string body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/__test/json-body/widgets")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

        request.Headers.Add("X-Api-Key", TestApiKey);
        return request;
    }

    private static WebApplicationFactory<Program> CreateFactory(long maxBodyBytes)
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
                        ["ApiIdempotency:MaxBodyBytes"] = maxBodyBytes.ToString(),
                        ["Authentication:ApiKey:Keys:test-key:Key"] = TestApiKey,
                        ["Authentication:ApiKey:Keys:test-key:PrincipalId"] = TestPrincipalId,
                        ["Authentication:ApiKey:Keys:test-key:DisplayName"] = "Test API caller"
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
