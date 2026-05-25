using System.Net;
using System.Text.Json;
using MemorySystem.Application.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MemorySystem.IntegrationTests;

public sealed class ApiTransportSecurityTests
{
    [Fact]
    public async Task Non_testing_http_requests_are_rejected_before_api_key_authentication()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Staging");
                builder.ConfigureAppConfiguration((_, configurationBuilder) =>
                {
                    configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Postgres"] =
                            "Host=unused;Database=unused;Username=unused;Password=unused",
                        ["ForwardedHeaders:KnownProxies:0"] = "127.0.0.1",
                        ["Authentication:ApiKey:Keys:test-key:Key"] = "test-api-key",
                        ["Authentication:ApiKey:Keys:test-key:PrincipalId"] = "11111111-1111-1111-1111-111111111111",
                        ["Authentication:ApiKey:Keys:test-key:DisplayName"] = "Test API caller"
                    });
                });
            });

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("http://localhost")
        });

        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("X-Api-Key", "test-api-key");

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("HTTPS is required for this API.", body);
    }

    [Fact]
    public void Non_testing_environment_requires_forwarded_header_trust_configuration()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Staging");
                builder.ConfigureAppConfiguration((_, configurationBuilder) =>
                {
                    configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Postgres"] =
                            "Host=unused;Database=unused;Username=unused;Password=unused",
                        ["Authentication:ApiKey:Keys:test-key:Key"] = "test-api-key",
                        ["Authentication:ApiKey:Keys:test-key:PrincipalId"] = "11111111-1111-1111-1111-111111111111",
                        ["Authentication:ApiKey:Keys:test-key:DisplayName"] = "Test API caller"
                    });
                });
            });

        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("ForwardedHeaders:KnownProxies", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Non_testing_forwarded_https_requests_from_known_proxy_are_accepted()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Staging");
                builder.ConfigureAppConfiguration((_, configurationBuilder) =>
                {
                    configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Postgres"] =
                            "Host=unused;Database=unused;Username=unused;Password=unused",
                        ["ForwardedHeaders:KnownProxies:0"] = "127.0.0.1",
                        ["Authentication:ApiKey:Keys:test-key:Key"] = "test-api-key",
                        ["Authentication:ApiKey:Keys:test-key:PrincipalId"] = "11111111-1111-1111-1111-111111111111",
                        ["Authentication:ApiKey:Keys:test-key:DisplayName"] = "Test API caller"
                    });
                });
            });

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("http://localhost")
        });

        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("X-Forwarded-Proto", "https");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Non_testing_semantic_search_requires_production_embedding_provider()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Staging");
                builder.ConfigureAppConfiguration((_, configurationBuilder) =>
                {
                    configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Postgres"] =
                            "Host=unused;Database=unused;Username=unused;Password=unused",
                        ["ForwardedHeaders:KnownProxies:0"] = "127.0.0.1",
                        ["Authentication:ApiKey:Keys:test-key:Key"] = "test-api-key",
                        ["Authentication:ApiKey:Keys:test-key:PrincipalId"] = "11111111-1111-1111-1111-111111111111",
                        ["Authentication:ApiKey:Keys:test-key:DisplayName"] = "Test API caller"
                    });
                });
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton<IApiKeyPrincipalValidator>(new AlwaysActiveApiKeyPrincipalValidator());
                });
            });

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("http://localhost")
        });

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/memory/search/semantic?q=alpha");
        request.Headers.Add("X-Api-Key", "test-api-key");
        request.Headers.Add("X-Forwarded-Proto", "https");

        using var response = await client.SendAsync(request);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Semantic memory retrieval is not configured.", document.RootElement.GetProperty("title").GetString());
    }

    private sealed class AlwaysActiveApiKeyPrincipalValidator : IApiKeyPrincipalValidator
    {
        public Task<bool> IsActiveAsync(Guid principalId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }
    }
}
