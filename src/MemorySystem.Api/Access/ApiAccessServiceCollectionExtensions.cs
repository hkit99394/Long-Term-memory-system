using MemorySystem.Application.Access;
using MemorySystem.Infrastructure.Access;

namespace MemorySystem.Api.Access;

public static class ApiAccessServiceCollectionExtensions
{
    public static IServiceCollection AddMemorySystemAccess(this IServiceCollection services)
    {
        services.AddSingleton<IMemoryAccessAuthorizer, MemoryAccessAuthorizer>();
        services.AddSingleton<IMemoryAccessReferenceStore, PostgresMemoryAccessReferenceStore>();

        return services;
    }
}
