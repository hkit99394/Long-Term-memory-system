using MemorySystem.Application.MemoryFacts;
using MemorySystem.Application.RoleMemoryLenses;
using MemorySystem.Infrastructure.MemoryFacts;
using MemorySystem.Infrastructure.RoleMemoryLenses;
using Npgsql;

namespace MemorySystem.Api.MemoryFacts;

public static class ApiMemoryFactServiceCollectionExtensions
{
    public static IServiceCollection AddMemorySystemMemoryFacts(this IServiceCollection services)
    {
        services.AddSingleton<IMemoryFactReadService, MemoryFactReadService>();
        services.AddSingleton<IMemoryFactRepository>(serviceProvider =>
            new PostgresMemoryFactRepository(serviceProvider.GetRequiredService<NpgsqlDataSource>()));
        services.AddSingleton<IRoleMemoryLensRepository>(serviceProvider =>
            new PostgresRoleMemoryLensRepository(serviceProvider.GetRequiredService<NpgsqlDataSource>()));

        return services;
    }
}
