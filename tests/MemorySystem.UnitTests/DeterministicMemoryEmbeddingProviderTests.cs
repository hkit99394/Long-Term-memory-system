using MemorySystem.Application.MemoryEmbeddings;
using MemorySystem.Infrastructure.MemoryEmbeddings;
using Microsoft.Extensions.Options;

namespace MemorySystem.UnitTests;

public sealed class DeterministicMemoryEmbeddingProviderTests
{
    [Fact]
    public async Task EmbedAsync_returns_configured_model_dimension_and_normalized_values()
    {
        var provider = CreateProvider();

        var embedding = await provider.EmbedAsync(new MemoryEmbeddingRequest("prefers concise decision logs"));
        var norm = Math.Sqrt(embedding.Values.Sum(value => value * value));

        Assert.Equal(MemoryEmbeddingOptions.DeterministicProvider, provider.ProviderName);
        Assert.Equal("memory-test-model", embedding.Model);
        Assert.Equal(12, embedding.Dimension);
        Assert.Equal(12, embedding.Values.Count);
        Assert.All(embedding.Values, value => Assert.InRange(value, -1.0f, 1.0f));
        Assert.InRange(norm, 0.999d, 1.001d);
    }

    [Fact]
    public async Task EmbedAsync_returns_same_values_for_same_input()
    {
        var provider = CreateProvider();

        var first = await provider.EmbedAsync(new MemoryEmbeddingRequest("same chunk text"));
        var second = await provider.EmbedAsync(new MemoryEmbeddingRequest("same chunk text"));

        Assert.Equal(first.Values, second.Values);
    }

    [Fact]
    public async Task EmbedAsync_changes_values_when_input_changes()
    {
        var provider = CreateProvider();

        var first = await provider.EmbedAsync(new MemoryEmbeddingRequest("first chunk text"));
        var second = await provider.EmbedAsync(new MemoryEmbeddingRequest("second chunk text"));

        Assert.Contains(first.Values.Zip(second.Values), pair => pair.First != pair.Second);
    }

    [Fact]
    public async Task EmbedAsync_rejects_blank_input()
    {
        var provider = CreateProvider();

        await Assert.ThrowsAsync<ArgumentException>(() => provider.EmbedAsync(new MemoryEmbeddingRequest(" ")));
    }

    private static DeterministicMemoryEmbeddingProvider CreateProvider()
    {
        return new DeterministicMemoryEmbeddingProvider(Options.Create(new MemoryEmbeddingOptions
        {
            Model = "memory-test-model",
            Dimension = 12
        }));
    }
}
