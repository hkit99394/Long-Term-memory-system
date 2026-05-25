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
