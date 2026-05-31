using MemorySystem.Api.Events;
using MemorySystem.Application.MemoryChunks;
using MemorySystem.Application.MemoryContext;
using MemorySystem.Application.MemoryEvaluations;
using MemorySystem.Application.MemoryFacts;
using MemorySystem.Infrastructure.MemoryChunks;
using MemorySystem.Infrastructure.MemoryContext;
using MemorySystem.Infrastructure.MemoryEvaluations;
using MemorySystem.Application.RoleMemoryLenses;
using MemorySystem.Infrastructure.MemoryFacts;
using MemorySystem.Infrastructure.RoleMemoryLenses;

namespace MemorySystem.Api.MemoryFacts;

public static class ApiMemoryFactServiceCollectionExtensions
{
    public static IServiceCollection AddMemorySystemMemoryFacts(this IServiceCollection services)
    {
        services.AddMemorySystemSourceEventLinks();
        services.AddSingleton<IMemoryChunkFullTextSearch, PostgresMemoryChunkFullTextSearch>();
        services.AddSingleton<IMemoryChunkSemanticSearch, PostgresMemoryChunkSemanticSearch>();
        services.AddSingleton<IMemoryChunkHybridSearch, PostgresMemoryChunkHybridSearch>();
        services.AddSingleton<IContextPacketBuilder, MemoryContextPacketBuilder>();
        services.AddSingleton<IMemoryContextPacketObservationStore, PostgresMemoryContextPacketObservationStore>();
        services.AddSingleton<IMemoryRetrievalFeedbackStore, PostgresMemoryRetrievalFeedbackStore>();
        services.AddSingleton<IMemoryRetrievalFeedbackSourceAuthorizer, PostgresMemoryRetrievalFeedbackSourceAuthorizer>();
        services.AddSingleton<IMemoryFactReadService, MemoryFactReadService>();
        services.AddSingleton<IMemoryFactFindingService, MemoryFactFindingService>();
        services.AddSingleton<IMemoryFactRepository, PostgresMemoryFactRepository>();
        services.AddSingleton<IMemoryFactFindingStore, PostgresMemoryFactFindingStore>();
        services.AddSingleton<IRoleMemoryLensRepository, PostgresRoleMemoryLensRepository>();

        return services;
    }
}
