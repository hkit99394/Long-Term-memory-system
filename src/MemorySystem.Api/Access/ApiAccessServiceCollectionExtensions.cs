using MemorySystem.Application.Access;
using MemorySystem.Application.AccessAuditing;
using MemorySystem.Application.ServiceAccounts;
using MemorySystem.Infrastructure.Access;
using MemorySystem.Infrastructure.AccessAuditing;
using MemorySystem.Infrastructure.ServiceAccounts;

namespace MemorySystem.Api.Access;

public static class ApiAccessServiceCollectionExtensions
{
    public static IServiceCollection AddMemorySystemAccess(this IServiceCollection services)
    {
        services.AddSingleton<IMemoryAccessAuthorizer, MemoryAccessAuthorizer>();
        services.AddSingleton<IMemoryAccessReferenceStore, PostgresMemoryAccessReferenceStore>();
        services.AddSingleton<PostgresAccessAuditEventStore>();
        services.AddSingleton<IAccessAuditEventStore>(serviceProvider =>
            serviceProvider.GetRequiredService<PostgresAccessAuditEventStore>());
        services.AddSingleton<IServiceAccountLifecycleStore, PostgresServiceAccountLifecycleStore>();

        return services;
    }
}
