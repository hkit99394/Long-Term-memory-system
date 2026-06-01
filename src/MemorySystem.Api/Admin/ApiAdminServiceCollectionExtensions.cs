using MemorySystem.Api.Events;
using MemorySystem.Application.Admin;
using MemorySystem.Infrastructure.Admin;

namespace MemorySystem.Api.Admin;

public static class ApiAdminServiceCollectionExtensions
{
    public static IServiceCollection AddMemorySystemAdminConsole(this IServiceCollection services)
    {
        services.AddMemorySystemSourceEventLinks();
        services.AddSingleton<IAdminMemoryInspectionStore, PostgresAdminMemoryInspectionStore>();
        services.AddSingleton<IAdminGovernanceStore, PostgresAdminGovernanceStore>();
        services.AddSingleton<IAdminAccessManagementStore, PostgresAdminAccessManagementStore>();
        services.AddSingleton<IAdminPermissionDriftReportStore, PostgresAdminPermissionDriftReportStore>();
        services.AddSingleton<IAdminAuditExportStore, PostgresAdminAuditExportStore>();

        return services;
    }
}
