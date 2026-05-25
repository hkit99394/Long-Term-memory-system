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

        return !RequiresProductionProvider(environment)
            || !string.Equals(
                options.Provider,
                MemoryEmbeddingOptions.DeterministicProvider,
                StringComparison.Ordinal);
    }

    public static void ThrowIfProviderIsNotUsable(
        IHostEnvironment environment,
        MemoryEmbeddingOptions options,
        string componentName)
    {
        if (HasUsableProvider(environment, options))
        {
            return;
        }

        throw new InvalidOperationException(
            $"{componentName} requires a production embedding provider outside Development and Testing. "
            + "Configure Embeddings:Provider=openai with a production API key, or disable semantic indexing.");
    }

    private static bool RequiresProductionProvider(IHostEnvironment environment)
    {
        return !environment.IsDevelopment()
            && !environment.IsEnvironment("Testing");
    }
}
