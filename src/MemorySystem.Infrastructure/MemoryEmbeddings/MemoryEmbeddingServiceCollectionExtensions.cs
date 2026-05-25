using MemorySystem.Application.MemoryEmbeddings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MemorySystem.Infrastructure.MemoryEmbeddings;

public static class MemoryEmbeddingServiceCollectionExtensions
{
    public static IServiceCollection AddMemorySystemEmbeddings(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<IOptions<MemoryEmbeddingOptions>>(serviceProvider =>
        {
            var resolvedConfiguration = serviceProvider.GetRequiredService<IConfiguration>();
            return Options.Create(MemoryEmbeddingOptions.Read(resolvedConfiguration));
        });
        services.AddHostedService<MemoryEmbeddingOptionsValidationHostedService>();

        services.AddSingleton<IMemoryEmbeddingProvider>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<MemoryEmbeddingOptions>>().Value;

            return string.Equals(options.Provider, MemoryEmbeddingOptions.DeterministicProvider, StringComparison.Ordinal)
                ? ActivatorUtilities.CreateInstance<DeterministicMemoryEmbeddingProvider>(serviceProvider)
                : ActivatorUtilities.CreateInstance<OpenAiMemoryEmbeddingProvider>(serviceProvider);
        });

        services.AddSingleton<IMemoryChunkEmbeddingStore, PostgresMemoryChunkEmbeddingStore>();

        return services;
    }
}
