namespace MemorySystem.UnitTests;

public sealed class OrganizationProjectManagementOpm03Tests
{
    [Fact]
    public void Opm03_lifecycle_scope_settings_are_documented_migrated_guarded_and_ui_backed()
    {
        var root = FindRepositoryRoot();
        var productPlan = File.ReadAllText(Path.Combine(root, "docs", "product-improvement-plan.md"));
        var backlog = File.ReadAllText(Path.Combine(root, "docs", "backlog.md"));
        var docsIndex = File.ReadAllText(Path.Combine(root, "docs", "README.md"));
        var testing = File.ReadAllText(Path.Combine(root, "docs", "testing.md"));
        var opm01 = File.ReadAllText(Path.Combine(root, "docs", "organization-project-management-opm01.md"));
        var opm03 = File.ReadAllText(Path.Combine(root, "docs", "organization-project-management-opm03.md"));
        var migration = File.ReadAllText(Path.Combine(root, "migrations", "032_project_lifecycle_scope_settings.sql"));
        var actionTypes = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Application", "AccessAuditing", "AccessAuditActionTypes.cs"));
        var endpoint = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "Admin", "AdminOrganizationProjectManagementEndpointExtensions.cs"));
        var store = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Infrastructure", "Admin", "PostgresAdminProjectLifecycleSettingsStore.cs"));
        var sourcePanel = File.ReadAllText(Path.Combine(root, "tools", "ui", "src", "admin", "management-panel.ts"));
        var script = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "wwwroot", "admin", "admin-console.js"));
        var css = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "wwwroot", "admin", "admin-console.css"));

        Assert.Contains("| OPM-03 | P0 | Done | Product Owner + Security Professional + Developer | Lifecycle And Scope Settings |", productPlan, StringComparison.Ordinal);
        Assert.Contains("| OPM-03 | P0 | Done | Add lifecycle and scope settings. |", backlog, StringComparison.Ordinal);
        Assert.Contains("[Organization And Project Management OPM-03](organization-project-management-opm03.md)", docsIndex, StringComparison.Ordinal);
        Assert.Contains("Status: OPM-01 through OPM-08 implemented.", opm01, StringComparison.Ordinal);
        Assert.Contains("# Organization And Project Management OPM-03", opm03, StringComparison.Ordinal);
        Assert.Contains("OrganizationProjectManagementOpm03Tests", testing, StringComparison.Ordinal);

        Assert.Contains("CREATE TABLE IF NOT EXISTS project_scope_settings", migration, StringComparison.Ordinal);
        Assert.Contains("'project_lifecycle_change'", migration, StringComparison.Ordinal);
        Assert.Contains("'project_scope_settings_change'", migration, StringComparison.Ordinal);
        Assert.Contains("ProjectLifecycleChange = \"project_lifecycle_change\"", actionTypes, StringComparison.Ordinal);
        Assert.Contains("ProjectScopeSettingsChange = \"project_scope_settings_change\"", actionTypes, StringComparison.Ordinal);

        foreach (var fragment in new[]
                 {
                     "/api/admin/projects/{projectId:guid}/scope-settings",
                     "/api/admin/projects/{projectId:guid}/lifecycle",
                     "AuthorizeProjectManagementAsync",
                     "IAdminProjectLifecycleSettingsStore"
                 })
        {
            Assert.Contains(fragment, endpoint, StringComparison.Ordinal);
        }

        Assert.Contains("NormalizeProjectNamespace", store, StringComparison.Ordinal);
        Assert.Contains("AccessAuditActionTypes.ProjectLifecycleChange", store, StringComparison.Ordinal);
        Assert.Contains("AccessAuditActionTypes.ProjectScopeSettingsChange", store, StringComparison.Ordinal);
        Assert.Contains("PostgresAccessAuditEventStore", store, StringComparison.Ordinal);
        Assert.Contains("BeginTransactionAsync", store, StringComparison.Ordinal);
        Assert.Contains("transaction.CommitAsync", store, StringComparison.Ordinal);
        Assert.Contains("MaxRequiredTextLength = 500", store, StringComparison.Ordinal);

        foreach (var fragment in new[]
                 {
                     "managementLifecycleForm",
                     "managementScopeSettingsForm",
                     "Update Lifecycle",
                     "Update Settings",
                     "Source hash required",
                     "/api/admin/projects/${projectId}/lifecycle",
                     "/api/admin/projects/${projectId}/scope-settings"
                 })
        {
            Assert.Contains(fragment, sourcePanel, StringComparison.Ordinal);
            Assert.Contains(fragment, script, StringComparison.Ordinal);
        }

        Assert.Contains(".management-check-field", css, StringComparison.Ordinal);
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
