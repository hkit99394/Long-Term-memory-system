using MemorySystem.Application.Scopes;
using MemorySystem.Infrastructure.Scopes;

namespace MemorySystem.Api.Scopes;

public static class ApiScopeServiceCollectionExtensions
{
    public static IServiceCollection AddMemorySystemScopes(this IServiceCollection services)
    {
        services.AddSingleton<IMemoryScopeResolver, MemoryScopeResolver>();
        services.AddSingleton<IMemoryScopeReferenceStore, PostgresMemoryScopeReferenceStore>();

        return services;
    }
}
