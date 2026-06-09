namespace MemorySystem.UnitTests;

public sealed class OrganizationProjectManagementOpm05Tests
{
    [Fact]
    public void Opm05_grant_matrix_is_documented_guarded_and_ui_backed()
    {
        var root = FindRepositoryRoot();
        var productPlan = File.ReadAllText(Path.Combine(root, "docs", "product-improvement-plan.md"));
        var backlog = File.ReadAllText(Path.Combine(root, "docs", "backlog.md"));
        var docsIndex = File.ReadAllText(Path.Combine(root, "docs", "README.md"));
        var testing = File.ReadAllText(Path.Combine(root, "docs", "testing.md"));
        var opm01 = File.ReadAllText(Path.Combine(root, "docs", "organization-project-management-opm01.md"));
        var opm05 = File.ReadAllText(Path.Combine(root, "docs", "organization-project-management-opm05.md"));
        var endpoint = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "Admin", "AdminOrganizationProjectManagementEndpointExtensions.cs"));
        var requests = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "Admin", "AdminGrantMatrixRequests.cs"));
        var responses = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "Admin", "AdminOrganizationProjectManagementResponses.cs"));
        var contract = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Application", "Admin", "IAdminGrantMatrixStore.cs"));
        var registration = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "Admin", "ApiAdminServiceCollectionExtensions.cs"));
        var store = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Infrastructure", "Admin", "PostgresAdminGrantMatrixStore.cs"));
        var sourcePanel = File.ReadAllText(Path.Combine(root, "tools", "ui", "src", "admin", "management-panel.ts"));
        var script = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "wwwroot", "admin", "admin-console.js"));
        var css = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "wwwroot", "admin", "admin-console.css"));

        Assert.Contains("| OPM-05 | P1 | Done | Product Owner + Knowledge Steward + Developer | Grant Matrix Management |", productPlan, StringComparison.Ordinal);
        Assert.Contains("| OPM-05 | P1 | Done | Add grant matrix management. |", backlog, StringComparison.Ordinal);
        Assert.Contains("[Organization And Project Management OPM-05](organization-project-management-opm05.md)", docsIndex, StringComparison.Ordinal);
        Assert.Contains("Status: OPM-01 through OPM-08 implemented.", opm01, StringComparison.Ordinal);
        Assert.Contains("# Organization And Project Management OPM-05", opm05, StringComparison.Ordinal);
        Assert.Contains("OrganizationProjectManagementOpm05Tests", testing, StringComparison.Ordinal);

        foreach (var fragment in new[]
                 {
                     "/api/admin/projects/{projectId:guid}/grant-matrix",
                     "/api/admin/projects/{projectId:guid}/grant-matrix/roles/{roleId}",
                     "AuthorizeProjectManagementAsync",
                     "IAdminGrantMatrixStore"
                 })
        {
            Assert.Contains(fragment, endpoint, StringComparison.Ordinal);
        }

        Assert.Contains("AdminGrantMatrixUpdateRequest", requests, StringComparison.Ordinal);
        Assert.Contains("AdminGrantMatrixResponse", responses, StringComparison.Ordinal);
        Assert.Contains("AdminGrantMatrixUpdateResponse", responses, StringComparison.Ordinal);
        Assert.Contains("GetProjectGrantMatrixAsync", contract, StringComparison.Ordinal);
        Assert.Contains("ReplaceRoleGrantsAsync", contract, StringComparison.Ordinal);
        Assert.Contains("IAdminGrantMatrixStore, PostgresAdminGrantMatrixStore", registration, StringComparison.Ordinal);

        foreach (var fragment in new[]
                 {
                     "private const string ContractId = \"OPM-05\"",
                     "project_product_owner",
                     "project_knowledge_steward",
                     "grant_matrix_replaced",
                     "Project root namespace grants must stay absent",
                     "Role lens namespace grants must match the selected role id",
                     "MatrixPermissions"
                 })
        {
            Assert.Contains(fragment, store, StringComparison.Ordinal);
        }

        foreach (var fragment in new[]
                 {
                     "managementGrantMatrixSection",
                     "updateManagementGrantMatrixRole",
                     "Grant matrix",
                     "Update Matrix",
                     "/api/admin/projects/${selected.id}/grant-matrix",
                     "/api/admin/projects/${projectId}/grant-matrix/roles/${encodeURIComponent(role.roleId)}"
                 })
        {
            Assert.Contains(fragment, sourcePanel, StringComparison.Ordinal);
            Assert.Contains(fragment, script, StringComparison.Ordinal);
        }

        Assert.Contains(".management-grant-matrix", css, StringComparison.Ordinal);
        Assert.Contains(".management-grant-role", css, StringComparison.Ordinal);
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
