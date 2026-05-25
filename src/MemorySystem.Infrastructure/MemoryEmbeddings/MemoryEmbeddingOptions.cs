using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace MemorySystem.Infrastructure.MemoryEmbeddings;

public sealed class MemoryEmbeddingOptions
{
    public const string SectionName = "Embeddings";
    public const string DeterministicProvider = "deterministic";
    public const string OpenAiProvider = "openai";
    public const string DefaultOpenAiEndpoint = "https://api.openai.com/v1/embeddings";
    public const string DefaultOpenAiModel = "text-embedding-3-small";
    public const int DefaultOpenAiDimension = 1536;

    public string Provider { get; init; } = DeterministicProvider;

    public string Model { get; init; } = "memory-deterministic-v1";

    public int Dimension { get; init; } = 32;

    public string Endpoint { get; init; } = DefaultOpenAiEndpoint;

    public string? ApiKey { get; init; }

    public static MemoryEmbeddingOptions Read(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(SectionName);
        var defaults = new MemoryEmbeddingOptions();
        var provider = NormalizeProvider(section[nameof(Provider)] ?? defaults.Provider);
        var dimensionValue = section[nameof(Dimension)];
        var options = new MemoryEmbeddingOptions
        {
            Provider = provider,
            Model = section[nameof(Model)] ?? DefaultModelFor(provider, defaults),
            Dimension = string.IsNullOrWhiteSpace(dimensionValue)
                ? DefaultDimensionFor(provider, defaults)
                : ParseDimension(dimensionValue),
            Endpoint = section[nameof(Endpoint)] ?? defaults.Endpoint,
            ApiKey = section[nameof(ApiKey)] ?? configuration["OPENAI_API_KEY"]
        };

        options.Validate();

        return options;
    }

    public void Validate()
    {
        if (!string.Equals(Provider, DeterministicProvider, StringComparison.Ordinal)
            && !string.Equals(Provider, OpenAiProvider, StringComparison.Ordinal))
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

        if (string.Equals(Provider, OpenAiProvider, StringComparison.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(ApiKey))
            {
                throw new InvalidOperationException("OpenAI embedding provider requires Embeddings:ApiKey or OPENAI_API_KEY.");
            }

            if (!Uri.TryCreate(Endpoint, UriKind.Absolute, out var endpoint)
                || endpoint.Scheme is not "https")
            {
                throw new InvalidOperationException("OpenAI embedding endpoint must be an absolute HTTPS URI.");
            }
        }
    }

    private static string NormalizeProvider(string provider)
    {
        return provider.Trim().ToLowerInvariant();
    }

    private static string DefaultModelFor(string provider, MemoryEmbeddingOptions defaults)
    {
        return string.Equals(provider, OpenAiProvider, StringComparison.Ordinal)
            ? DefaultOpenAiModel
            : defaults.Model;
    }

    private static int DefaultDimensionFor(string provider, MemoryEmbeddingOptions defaults)
    {
        return string.Equals(provider, OpenAiProvider, StringComparison.Ordinal)
            ? DefaultOpenAiDimension
            : defaults.Dimension;
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
