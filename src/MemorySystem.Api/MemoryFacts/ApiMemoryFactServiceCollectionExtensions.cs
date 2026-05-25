using MemorySystem.Application.MemoryChunks;
using MemorySystem.Application.MemoryContext;
using MemorySystem.Application.MemoryFacts;
using MemorySystem.Infrastructure.MemoryChunks;
using MemorySystem.Application.RoleMemoryLenses;
using MemorySystem.Infrastructure.MemoryFacts;
using MemorySystem.Infrastructure.RoleMemoryLenses;

namespace MemorySystem.Api.MemoryFacts;

public static class ApiMemoryFactServiceCollectionExtensions
{
    public static IServiceCollection AddMemorySystemMemoryFacts(this IServiceCollection services)
    {
        services.AddSingleton<IMemoryChunkFullTextSearch, PostgresMemoryChunkFullTextSearch>();
        services.AddSingleton<IMemoryChunkSemanticSearch, PostgresMemoryChunkSemanticSearch>();
        services.AddSingleton<IMemoryChunkHybridSearch, PostgresMemoryChunkHybridSearch>();
        services.AddSingleton<IContextPacketBuilder, MemoryContextPacketBuilder>();
        services.AddSingleton<IMemoryFactReadService, MemoryFactReadService>();
        services.AddSingleton<IMemoryFactRepository, PostgresMemoryFactRepository>();
        services.AddSingleton<IRoleMemoryLensRepository, PostgresRoleMemoryLensRepository>();

        return services;
    }
}
