using System.Net;
using System.Text.Json;
using MemorySystem.Application.MemoryEmbeddings;
using MemorySystem.Infrastructure.MemoryEmbeddings;
using Microsoft.Extensions.Options;

namespace MemorySystem.UnitTests;

public sealed class OpenAiMemoryEmbeddingProviderTests
{
    [Fact]
    public async Task EmbedAsync_sends_configured_request_and_returns_embedding()
    {
        string? requestBody = null;
        using var httpClient = new HttpClient(new RecordingHandler(async (request, cancellationToken) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://example.test/v1/embeddings", request.RequestUri?.ToString());
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("test-openai-key", request.Headers.Authorization?.Parameter);

            requestBody = await request.Content!.ReadAsStringAsync(cancellationToken);

            return JsonResponse("""
                {
                  "data": [
                    {
                      "embedding": [0.25, -0.5, 0.75]
                    }
                  ]
                }
                """);
        }));
        var provider = CreateProvider(httpClient);

        var embedding = await provider.EmbedAsync(new MemoryEmbeddingRequest("project memory text"));

        using var document = JsonDocument.Parse(requestBody!);
        var root = document.RootElement;

        Assert.Equal(MemoryEmbeddingOptions.OpenAiProvider, provider.ProviderName);
        Assert.Equal("text-embedding-test", root.GetProperty("model").GetString());
        Assert.Equal("project memory text", root.GetProperty("input").GetString());
        Assert.Equal(3, root.GetProperty("dimensions").GetInt32());
        Assert.Equal("text-embedding-test", embedding.Model);
        Assert.Equal(3, embedding.Dimension);
        Assert.Equal([0.25f, -0.5f, 0.75f], embedding.Values);
    }

    [Fact]
    public async Task EmbedAsync_rejects_response_with_unexpected_dimension()
    {
        using var httpClient = new HttpClient(new RecordingHandler((_, _) =>
            Task.FromResult(JsonResponse("""
                {
                  "data": [
                    {
                      "embedding": [0.25, -0.5]
                    }
                  ]
                }
                """))));
        var provider = CreateProvider(httpClient);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.EmbedAsync(new MemoryEmbeddingRequest("project memory text")));

        Assert.Contains("dimension", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EmbedAsync_reports_failed_status_without_returning_response_body_verbatim()
    {
        using var httpClient = new HttpClient(new RecordingHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(new string('x', 700))
            })));
        var provider = CreateProvider(httpClient);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.EmbedAsync(new MemoryEmbeddingRequest("project memory text")));

        Assert.Contains("status 400", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(exception.Message.Length < 650);
    }

    private static OpenAiMemoryEmbeddingProvider CreateProvider(HttpClient httpClient)
    {
        return new OpenAiMemoryEmbeddingProvider(
            Options.Create(new MemoryEmbeddingOptions
            {
                Provider = MemoryEmbeddingOptions.OpenAiProvider,
                Model = "text-embedding-test",
                Dimension = 3,
                Endpoint = "https://example.test/v1/embeddings",
                ApiKey = "test-openai-key"
            }),
            httpClient);
    }

    private static HttpResponseMessage JsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
    }

    private sealed class RecordingHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handleAsync)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return handleAsync(request, cancellationToken);
        }
    }
}
