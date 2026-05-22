using MemorySystem.Application.Access;
using MemorySystem.Infrastructure.Access;
using Npgsql;

namespace MemorySystem.Api.Access;

public static class ApiAccessServiceCollectionExtensions
{
    public static IServiceCollection AddMemorySystemAccess(this IServiceCollection services)
    {
        services.AddSingleton<IMemoryAccessAuthorizer, MemoryAccessAuthorizer>();
        services.AddSingleton<IMemoryAccessReferenceStore>(serviceProvider =>
            new PostgresMemoryAccessReferenceStore(serviceProvider.GetRequiredService<NpgsqlDataSource>()));

        return services;
    }
}
