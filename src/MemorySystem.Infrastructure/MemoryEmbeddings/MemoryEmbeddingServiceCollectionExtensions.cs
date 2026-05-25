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
        var options = MemoryEmbeddingOptions.Read(configuration);
        services.AddSingleton(Options.Create(options));
        services.AddSingleton<IMemoryEmbeddingProvider, DeterministicMemoryEmbeddingProvider>();
        services.AddSingleton<IMemoryChunkEmbeddingStore, PostgresMemoryChunkEmbeddingStore>();

        return services;
    }
}
