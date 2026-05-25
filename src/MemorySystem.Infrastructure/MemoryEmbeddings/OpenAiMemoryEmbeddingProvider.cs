using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MemorySystem.Application.MemoryEmbeddings;
using Microsoft.Extensions.Options;

namespace MemorySystem.Infrastructure.MemoryEmbeddings;

public sealed class OpenAiMemoryEmbeddingProvider : IMemoryEmbeddingProvider, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly MemoryEmbeddingOptions options;
    private readonly HttpClient httpClient;

    public OpenAiMemoryEmbeddingProvider(IOptions<MemoryEmbeddingOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        this.options = options.Value;
        this.options.Validate();
        this.httpClient = new HttpClient();
    }

    public string ProviderName => options.Provider;

    public string Model => options.Model;

    public int Dimension => options.Dimension;

    public async Task<MemoryEmbeddingVector> EmbedAsync(
        MemoryEmbeddingRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Input);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, options.Endpoint);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        httpRequest.Content = JsonContent.Create(
            new OpenAiEmbeddingRequest(options.Model, request.Input, options.Dimension),
            options: JsonOptions);

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"OpenAI embedding request failed with status {(int)response.StatusCode}. {Truncate(responseBody, 512)}");
        }

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var embeddingResponse = await JsonSerializer.DeserializeAsync<OpenAiEmbeddingResponse>(
            responseStream,
            JsonOptions,
            cancellationToken);
        var embedding = embeddingResponse?.Data.FirstOrDefault()?.Embedding;

        if (embedding is null || embedding.Length == 0)
        {
            throw new InvalidOperationException("OpenAI embedding response did not include an embedding vector.");
        }

        if (embedding.Length != options.Dimension)
        {
            throw new InvalidOperationException(
                $"OpenAI embedding response dimension {embedding.Length} did not match configured dimension {options.Dimension}.");
        }

        return new MemoryEmbeddingVector(options.Model, options.Dimension, embedding);
    }

    public void Dispose()
    {
        httpClient.Dispose();
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private sealed record OpenAiEmbeddingRequest(string Model, string Input, int Dimensions);

    private sealed record OpenAiEmbeddingResponse(IReadOnlyList<OpenAiEmbeddingData> Data);

    private sealed record OpenAiEmbeddingData(float[] Embedding);
}
