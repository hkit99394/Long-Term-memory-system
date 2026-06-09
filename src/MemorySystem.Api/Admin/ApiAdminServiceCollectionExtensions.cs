using MemorySystem.Api.Events;
using MemorySystem.Application.Admin;
using MemorySystem.Application.Roles;
using MemorySystem.Infrastructure.Admin;
using MemorySystem.Infrastructure.Roles;

namespace MemorySystem.Api.Admin;

public static class ApiAdminServiceCollectionExtensions
{
    public static IServiceCollection AddMemorySystemAdminConsole(this IServiceCollection services)
    {
        services.AddMemorySystemSourceEventLinks();
        services.AddSingleton<IAdminMemoryInspectionStore, PostgresAdminMemoryInspectionStore>();
        services.AddSingleton<IAdminGovernanceStore, PostgresAdminGovernanceStore>();
        services.AddSingleton<IAdminAccessManagementStore, PostgresAdminAccessManagementStore>();
        services.AddSingleton<IAdminProjectRegistrationStore, PostgresAdminProjectRegistrationStore>();
        services.AddSingleton<IAdminOrganizationProjectManagementStore, PostgresAdminOrganizationProjectManagementStore>();
        services.AddSingleton<IAdminManagementActivityStore, PostgresAdminManagementActivityStore>();
        services.AddSingleton<IAdminProjectRoleDefinitionManagementStore, PostgresAdminProjectRoleDefinitionManagementStore>();
        services.AddSingleton<IAdminProjectLifecycleSettingsStore, PostgresAdminProjectLifecycleSettingsStore>();
        services.AddSingleton<IAdminAccessInventoryStore, PostgresAdminAccessInventoryStore>();
        services.AddSingleton<IAdminGrantMatrixStore, PostgresAdminGrantMatrixStore>();
        services.AddSingleton<IProjectRoleDefinitionStore, PostgresProjectRoleDefinitionStore>();
        services.AddSingleton<IAdminPermissionDriftReportStore, PostgresAdminPermissionDriftReportStore>();
        services.AddSingleton<IAdminAuditExportStore, PostgresAdminAuditExportStore>();

        return services;
    }
}
