using MemorySystem.Application.MemoryFacts;
using MemorySystem.Infrastructure.MemoryFacts;
using Npgsql;

namespace MemorySystem.Api.MemoryFacts;

public static class ApiMemoryFactServiceCollectionExtensions
{
    public static IServiceCollection AddMemorySystemMemoryFacts(this IServiceCollection services)
    {
        services.AddSingleton<IMemoryFactReadService, MemoryFactReadService>();
        services.AddSingleton<IMemoryFactReadStore>(serviceProvider =>
            new PostgresMemoryFactReadStore(serviceProvider.GetRequiredService<NpgsqlDataSource>()));

        return services;
    }
}
