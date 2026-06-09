namespace MemorySystem.UnitTests;

public sealed class OrganizationProjectManagementOpm08Tests
{
    [Fact]
    public void Opm08_project_role_definition_management_is_documented_guarded_and_ui_backed()
    {
        var root = FindRepositoryRoot();
        var productPlan = File.ReadAllText(Path.Combine(root, "docs", "product-improvement-plan.md"));
        var backlog = File.ReadAllText(Path.Combine(root, "docs", "backlog.md"));
        var docsIndex = File.ReadAllText(Path.Combine(root, "docs", "README.md"));
        var testing = File.ReadAllText(Path.Combine(root, "docs", "testing.md"));
        var opm01 = File.ReadAllText(Path.Combine(root, "docs", "organization-project-management-opm01.md"));
        var opm08 = File.ReadAllText(Path.Combine(root, "docs", "organization-project-management-opm08.md"));
        var endpoint = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "Admin", "AdminOrganizationProjectManagementEndpointExtensions.cs"));
        var requests = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "Admin", "AdminProjectRoleDefinitionManagementRequests.cs"));
        var responses = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "Admin", "AdminOrganizationProjectManagementResponses.cs"));
        var contract = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Application", "Admin", "IAdminProjectRoleDefinitionManagementStore.cs"));
        var registration = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "Admin", "ApiAdminServiceCollectionExtensions.cs"));
        var store = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Infrastructure", "Admin", "PostgresAdminProjectRoleDefinitionManagementStore.cs"));
        var activityStore = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Infrastructure", "Admin", "PostgresAdminManagementActivityStore.cs"));
        var sourcePanel = File.ReadAllText(Path.Combine(root, "tools", "ui", "src", "admin", "management-panel.ts"));
        var script = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "wwwroot", "admin", "admin-console.js"));
        var css = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "wwwroot", "admin", "admin-console.css"));

        Assert.Contains("| OPM-08 | P1 | Done | Product Owner + Knowledge Steward + Security Professional + Developer | Project Role Definition Management |", productPlan, StringComparison.Ordinal);
        Assert.Contains("| OPM-08 | P1 | Done | Add project role definition management. |", backlog, StringComparison.Ordinal);
        Assert.Contains("[Organization And Project Management OPM-08](organization-project-management-opm08.md)", docsIndex, StringComparison.Ordinal);
        Assert.Contains("Status: OPM-01 through OPM-08 implemented.", opm01, StringComparison.Ordinal);
        Assert.Contains("# Organization And Project Management OPM-08", opm08, StringComparison.Ordinal);
        Assert.Contains("OrganizationProjectManagementOpm08Tests", testing, StringComparison.Ordinal);
        Assert.Contains("ApiAdminProjectRoleDefinitionManagementTests", testing, StringComparison.Ordinal);

        foreach (var fragment in new[]
                 {
                     "/api/admin/projects/{projectId:guid}/role-definitions",
                     "/api/admin/projects/{projectId:guid}/role-definitions/{roleId}",
                     "IAdminProjectRoleDefinitionManagementStore",
                     "AuthorizeProjectManagementAsync",
                     "RoleIdsMatch"
                 })
        {
            Assert.Contains(fragment, endpoint, StringComparison.Ordinal);
        }

        Assert.Contains("AdminProjectRoleDefinitionManagementUpdateRequest", requests, StringComparison.Ordinal);
        Assert.Contains("AdminProjectRoleDefinitionListResponse", responses, StringComparison.Ordinal);
        Assert.Contains("AdminProjectRoleDefinitionUpdateResponse", responses, StringComparison.Ordinal);
        Assert.Contains("AdminProjectRoleDefinitionManagementResponse", responses, StringComparison.Ordinal);
        Assert.Contains("GetProjectRoleDefinitionsAsync", contract, StringComparison.Ordinal);
        Assert.Contains("UpsertProjectRoleDefinitionAsync", contract, StringComparison.Ordinal);
        Assert.Contains("IAdminProjectRoleDefinitionManagementStore, PostgresAdminProjectRoleDefinitionManagementStore", registration, StringComparison.Ordinal);

        foreach (var fragment in new[]
                 {
                     "private const string ContractId = \"OPM-08\"",
                     "project_role_definitions",
                     "Default template roles are not project-defined roles.",
                     "Disable project role definitions only after role assignments and role-targeted grants are removed.",
                     "AccessAuditActionTypes.ProjectRoleDefinitionChange",
                     "project_role_definition",
                     "roleAssignmentCount",
                     "roleGrantCount",
                     "auditEvidenceId",
                     "PostgresAccessAuditEventStore",
                     "BeginTransactionAsync",
                     "transaction.CommitAsync",
                     "MaxRequiredTextLength = 500"
                 })
        {
            Assert.Contains(fragment, store, StringComparison.Ordinal);
        }

        foreach (var fragment in new[]
                 {
                     "'roleStatus'",
                     "'previousRoleStatus'",
                     "'templateRoleId'",
                     "'roleAssignmentCount'",
                     "'roleGrantCount'"
                 })
        {
            Assert.Contains(fragment, activityStore, StringComparison.Ordinal);
        }

        foreach (var fragment in new[]
                 {
                     "roleDefinitionsDetail",
                     "managementProjectRoleDefinitionsSection",
                     "managementProjectRoleDefinitionCard",
                     "updateManagementProjectRoleDefinition",
                     "Project role definitions",
                     "/api/admin/projects/${selected.id}/role-definitions",
                     "/api/admin/projects/${projectId}/role-definitions/${encodeURIComponent(roleId)}",
                     "managedProjectRoleDefinitionCount"
                 })
        {
            Assert.Contains(fragment, sourcePanel, StringComparison.Ordinal);
            Assert.Contains(fragment, script, StringComparison.Ordinal);
        }

        Assert.Contains(".management-role-definitions", css, StringComparison.Ordinal);
        Assert.Contains(".management-role-definition", css, StringComparison.Ordinal);
        Assert.Contains(".management-role-form", css, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MemorySystem.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
