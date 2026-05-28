using MemorySystem.Api.Events;
using MemorySystem.Application.VaultExports;
using MemorySystem.Infrastructure.VaultExports;

namespace MemorySystem.Api.VaultExports;

public static class ApiVaultExportServiceCollectionExtensions
{
    public static IServiceCollection AddMemorySystemVaultExports(this IServiceCollection services)
    {
        services.AddMemorySystemSourceEventLinks();
        services.AddSingleton<IObsidianExportService, ObsidianExportService>();
        services.AddSingleton<IObsidianExportCandidateStore, PostgresObsidianExportCandidateStore>();

        return services;
    }
}
