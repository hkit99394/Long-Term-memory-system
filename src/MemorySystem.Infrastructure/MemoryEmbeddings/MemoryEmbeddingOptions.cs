using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace MemorySystem.Infrastructure.MemoryEmbeddings;

public sealed class MemoryEmbeddingOptions
{
    public const string SectionName = "Embeddings";
    public const string DeterministicProvider = "deterministic";

    public string Provider { get; init; } = DeterministicProvider;

    public string Model { get; init; } = "memory-deterministic-v1";

    public int Dimension { get; init; } = 32;

    public static MemoryEmbeddingOptions Read(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(SectionName);
        var defaults = new MemoryEmbeddingOptions();
        var dimensionValue = section[nameof(Dimension)];
        var options = new MemoryEmbeddingOptions
        {
            Provider = section[nameof(Provider)] ?? defaults.Provider,
            Model = section[nameof(Model)] ?? defaults.Model,
            Dimension = string.IsNullOrWhiteSpace(dimensionValue)
                ? defaults.Dimension
                : ParseDimension(dimensionValue)
        };

        options.Validate();

        return options;
    }

    public void Validate()
    {
        if (!string.Equals(Provider, DeterministicProvider, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Embedding provider '{Provider}' is not supported.");
        }

        if (string.IsNullOrWhiteSpace(Model))
        {
            throw new InvalidOperationException("Embedding model must be configured.");
        }

        if (Dimension is < 1 or > 4096)
        {
            throw new InvalidOperationException("Embedding dimension must be between 1 and 4096.");
        }
    }

    private static int ParseDimension(string value)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var dimension))
        {
            throw new InvalidOperationException("Embedding dimension must be an integer.");
        }

        return dimension;
    }
}
