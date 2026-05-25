using System.Globalization;
using MemorySystem.Infrastructure.MemoryEmbeddings;
using Microsoft.Extensions.Configuration;

namespace MemorySystem.UnitTests;

public sealed class MemoryEmbeddingOptionsTests
{
    [Fact]
    public void Read_uses_deterministic_defaults()
    {
        var options = MemoryEmbeddingOptions.Read(new ConfigurationBuilder().Build());

        Assert.Equal(MemoryEmbeddingOptions.DeterministicProvider, options.Provider);
        Assert.Equal("memory-deterministic-v1", options.Model);
        Assert.Equal(32, options.Dimension);
    }

    [Fact]
    public void Read_parses_configured_model_and_dimension_with_invariant_culture()
    {
        var originalCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Embeddings:Provider"] = MemoryEmbeddingOptions.DeterministicProvider,
                    ["Embeddings:Model"] = "memory-test-model",
                    ["Embeddings:Dimension"] = "12"
                })
                .Build();

            var options = MemoryEmbeddingOptions.Read(configuration);

            Assert.Equal(MemoryEmbeddingOptions.DeterministicProvider, options.Provider);
            Assert.Equal("memory-test-model", options.Model);
            Assert.Equal(12, options.Dimension);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void Read_supports_openai_provider_configuration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Embeddings:Provider"] = MemoryEmbeddingOptions.OpenAiProvider,
                ["Embeddings:Model"] = "text-embedding-3-small",
                ["Embeddings:Dimension"] = "1536",
                ["Embeddings:ApiKey"] = "test-key"
            })
            .Build();

        var options = MemoryEmbeddingOptions.Read(configuration);

        Assert.Equal(MemoryEmbeddingOptions.OpenAiProvider, options.Provider);
        Assert.Equal("text-embedding-3-small", options.Model);
        Assert.Equal(1536, options.Dimension);
        Assert.Equal("test-key", options.ApiKey);
        Assert.Equal(MemoryEmbeddingOptions.DefaultOpenAiEndpoint, options.Endpoint);
    }

    [Fact]
    public void Read_uses_openai_api_key_environment_fallback()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Embeddings:Provider"] = "OPENAI",
                ["OPENAI_API_KEY"] = "environment-test-key"
            })
            .Build();

        var options = MemoryEmbeddingOptions.Read(configuration);

        Assert.Equal(MemoryEmbeddingOptions.OpenAiProvider, options.Provider);
        Assert.Equal(MemoryEmbeddingOptions.DefaultOpenAiModel, options.Model);
        Assert.Equal(MemoryEmbeddingOptions.DefaultOpenAiDimension, options.Dimension);
        Assert.Equal("environment-test-key", options.ApiKey);
    }

    [Fact]
    public void Read_rejects_unsupported_provider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Embeddings:Provider"] = "external"
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(() => MemoryEmbeddingOptions.Read(configuration));

        Assert.Contains("not supported", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Read_rejects_blank_model()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Embeddings:Model"] = " "
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(() => MemoryEmbeddingOptions.Read(configuration));

        Assert.Contains("model", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Read_rejects_openai_provider_without_api_key()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Embeddings:Provider"] = MemoryEmbeddingOptions.OpenAiProvider
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(() => MemoryEmbeddingOptions.Read(configuration));

        Assert.Contains("api_key", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Read_rejects_openai_provider_with_plaintext_endpoint()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Embeddings:Provider"] = MemoryEmbeddingOptions.OpenAiProvider,
                ["Embeddings:Endpoint"] = "http://localhost:9999/v1/embeddings",
                ["Embeddings:ApiKey"] = "test-key"
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(() => MemoryEmbeddingOptions.Read(configuration));

        Assert.Contains("HTTPS", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("4097")]
    [InlineData("not-a-number")]
    public void Read_rejects_invalid_dimension(string dimension)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Embeddings:Dimension"] = dimension
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(() => MemoryEmbeddingOptions.Read(configuration));

        Assert.Contains("dimension", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
