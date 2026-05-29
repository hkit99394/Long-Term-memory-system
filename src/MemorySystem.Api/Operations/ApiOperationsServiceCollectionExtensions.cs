using MemorySystem.Application.Operations;
using MemorySystem.Infrastructure.Health;
using MemorySystem.Infrastructure.Operations;

namespace MemorySystem.Api.Operations;

public static class ApiOperationsServiceCollectionExtensions
{
    public static IServiceCollection AddMemorySystemOperations(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton(WorkerHeartbeatHealthOptions.Read(configuration));
        services.AddSingleton<IOperationalSummaryStore, PostgresOperationalSummaryStore>();

        return services;
    }
}
