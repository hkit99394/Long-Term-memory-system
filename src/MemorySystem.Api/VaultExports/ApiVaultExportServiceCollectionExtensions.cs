using MemorySystem.Application.VaultExports;
using MemorySystem.Infrastructure.VaultExports;

namespace MemorySystem.Api.VaultExports;

public static class ApiVaultExportServiceCollectionExtensions
{
    public static IServiceCollection AddMemorySystemVaultExports(this IServiceCollection services)
    {
        services.AddSingleton<IObsidianExportService, ObsidianExportService>();
        services.AddSingleton<IObsidianExportCandidateStore, PostgresObsidianExportCandidateStore>();

        return services;
    }
}
