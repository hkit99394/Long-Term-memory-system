using System.Net;
using System.Text.Json;
using MemorySystem.Application.Authentication;
using MemorySystem.Application.MemoryChunks;
using MemorySystem.Application.MemoryContext;
using MemorySystem.Infrastructure.MemoryEmbeddings;
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
    public void Non_testing_openai_embedding_provider_requires_api_key_at_startup()
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
                        ["Authentication:ApiKey:Keys:test-key:DisplayName"] = "Test API caller",
                        ["Embeddings:Provider"] = MemoryEmbeddingOptions.OpenAiProvider,
                        ["OPENAI_API_KEY"] = ""
                    });
                });
            });

        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("OpenAI embedding provider", exception.Message, StringComparison.Ordinal);
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
    public async Task Non_testing_semantic_retrieval_routes_require_production_embedding_provider()
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

        foreach (var uri in new[]
        {
            "/api/memory/search/semantic?q=alpha",
            "/api/memory/search/hybrid?q=alpha",
            "/api/memory/context?q=alpha"
        })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Add("X-Api-Key", "test-api-key");
            request.Headers.Add("X-Forwarded-Proto", "https");

            using var response = await client.SendAsync(request);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Equal("Semantic memory retrieval is not configured.", document.RootElement.GetProperty("title").GetString());
        }
    }

    [Fact]
    public async Task Non_testing_semantic_retrieval_routes_allow_configured_production_embedding_provider()
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
                        ["Authentication:ApiKey:Keys:test-key:DisplayName"] = "Test API caller",
                        ["Embeddings:Provider"] = MemoryEmbeddingOptions.OpenAiProvider,
                        ["Embeddings:Model"] = "text-embedding-3-small",
                        ["Embeddings:Dimension"] = "1536",
                        ["Embeddings:ApiKey"] = "test-openai-key"
                    });
                });
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton<IApiKeyPrincipalValidator>(new AlwaysActiveApiKeyPrincipalValidator());
                    services.AddSingleton<IMemoryChunkSemanticSearch, EmptySemanticSearch>();
                    services.AddSingleton<IMemoryChunkHybridSearch, EmptyHybridSearch>();
                    services.AddSingleton<IContextPacketBuilder, EmptyContextPacketBuilder>();
                });
            });

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("http://localhost")
        });

        foreach (var uri in new[]
        {
            "/api/memory/search/semantic?q=alpha",
            "/api/memory/search/hybrid?q=alpha",
            "/api/memory/context?q=alpha"
        })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Add("X-Api-Key", "test-api-key");
            request.Headers.Add("X-Forwarded-Proto", "https");

            using var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            Assert.True(
                response.StatusCode == HttpStatusCode.OK,
                $"{uri} expected OK but returned {(int)response.StatusCode} {response.StatusCode}: {body}");
        }
    }

    private sealed class AlwaysActiveApiKeyPrincipalValidator : IApiKeyPrincipalValidator
    {
        public Task<bool> IsActiveAsync(Guid principalId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }
    }

    private sealed class EmptySemanticSearch : IMemoryChunkSemanticSearch
    {
        public Task<IReadOnlyList<MemoryChunkSearchResult>> SearchAsync(
            MemoryChunkSemanticSearchQuery query,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<MemoryChunkSearchResult>>(Array.Empty<MemoryChunkSearchResult>());
        }
    }

    private sealed class EmptyHybridSearch : IMemoryChunkHybridSearch
    {
        public Task<IReadOnlyList<MemoryChunkHybridSearchResult>> SearchAsync(
            MemoryChunkHybridSearchQuery query,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<MemoryChunkHybridSearchResult>>(Array.Empty<MemoryChunkHybridSearchResult>());
        }
    }

    private sealed class EmptyContextPacketBuilder : IContextPacketBuilder
    {
        public Task<MemoryContextPacket> BuildAsync(
            MemoryContextPacketQuery query,
            CancellationToken cancellationToken = default)
        {
            var targetScope = string.IsNullOrWhiteSpace(query.TargetScopeType) || string.IsNullOrWhiteSpace(query.TargetScopeId)
                ? null
                : new MemoryContextTargetScope(query.TargetScopeType, query.TargetScopeId);

            return Task.FromResult(new MemoryContextPacket(
                query.PrincipalId,
                query.Query,
                targetScope,
                query.RoleId,
                new MemoryContextCurrentTask(query.Query, targetScope, query.RoleId),
                Array.Empty<MemoryContextPacketItem>(),
                Array.Empty<MemoryContextPacketItem>(),
                Array.Empty<MemoryContextPacketItem>(),
                Array.Empty<MemoryContextPacketItem>(),
                Array.Empty<MemoryContextSourceEvent>()));
        }
    }
}
