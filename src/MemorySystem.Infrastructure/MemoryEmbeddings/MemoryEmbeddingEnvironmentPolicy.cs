using MemorySystem.Infrastructure.Configuration;
using Microsoft.Extensions.Hosting;

namespace MemorySystem.Infrastructure.MemoryEmbeddings;

public static class MemoryEmbeddingEnvironmentPolicy
{
    public static bool HasUsableProvider(
        IHostEnvironment environment,
        MemoryEmbeddingOptions options)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(options);

        return GetUnusableReason(environment, options) is null;
    }

    public static void ThrowIfProviderIsNotUsable(
        IHostEnvironment environment,
        MemoryEmbeddingOptions options,
        string componentName)
    {
        var unusableReason = GetUnusableReason(environment, options);

        if (unusableReason is null)
        {
            return;
        }

        throw new InvalidOperationException(
            $"{componentName} {unusableReason}");
    }

    public static void ThrowIfProductionCredentialIsNotSafe(
        IHostEnvironment environment,
        MemoryEmbeddingOptions options,
        string componentName)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(options);

        if (!RequiresProductionProvider(environment)
            || !string.Equals(options.Provider, MemoryEmbeddingOptions.OpenAiProvider, StringComparison.Ordinal)
            || ProductionSecretSafety.IsProductionSafeSecretValue(options.ApiKey))
        {
            return;
        }

        throw new InvalidOperationException(
            $"{componentName} requires a production-safe OpenAI API key outside Development and Testing. "
            + "Configure Embeddings:ApiKey or OPENAI_API_KEY from a secret store.");
    }

    public static string? GetUnusableReason(
        IHostEnvironment environment,
        MemoryEmbeddingOptions options)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(options);

        if (!RequiresProductionProvider(environment))
        {
            return null;
        }

        if (string.Equals(options.Provider, MemoryEmbeddingOptions.DeterministicProvider, StringComparison.Ordinal))
        {
            return "requires a production embedding provider outside Development and Testing. "
                + "Configure Embeddings:Provider=openai with a production API key, or disable semantic indexing.";
        }

        if (string.Equals(options.Provider, MemoryEmbeddingOptions.OpenAiProvider, StringComparison.Ordinal)
            && !ProductionSecretSafety.IsProductionSafeSecretValue(options.ApiKey))
        {
            return "requires a production-safe OpenAI API key outside Development and Testing. "
                + "Configure Embeddings:ApiKey or OPENAI_API_KEY from a secret store.";
        }

        return null;
    }

    private static bool RequiresProductionProvider(IHostEnvironment environment)
    {
        return !environment.IsDevelopment()
            && !environment.IsEnvironment("Testing");
    }
}
