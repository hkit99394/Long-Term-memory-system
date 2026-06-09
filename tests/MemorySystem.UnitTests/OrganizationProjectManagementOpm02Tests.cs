namespace MemorySystem.UnitTests;

public sealed class OrganizationProjectManagementOpm02Tests
{
    [Fact]
    public void Opm02_admin_management_ui_shell_is_documented_bundled_and_api_backed()
    {
        var root = FindRepositoryRoot();
        var productPlan = File.ReadAllText(Path.Combine(root, "docs", "product-improvement-plan.md"));
        var backlog = File.ReadAllText(Path.Combine(root, "docs", "backlog.md"));
        var docsIndex = File.ReadAllText(Path.Combine(root, "docs", "README.md"));
        var testing = File.ReadAllText(Path.Combine(root, "docs", "testing.md"));
        var opm01 = File.ReadAllText(Path.Combine(root, "docs", "organization-project-management-opm01.md"));
        var opm02 = File.ReadAllText(Path.Combine(root, "docs", "organization-project-management-opm02.md"));
        var html = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "wwwroot", "admin", "index.html"));
        var sourcePanel = File.ReadAllText(Path.Combine(root, "tools", "ui", "src", "admin", "management-panel.ts"));
        var adminConsole = File.ReadAllText(Path.Combine(root, "tools", "ui", "src", "admin-console.ts"));
        var script = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "wwwroot", "admin", "admin-console.js"));
        var css = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "wwwroot", "admin", "admin-console.css"));
        var buildScript = File.ReadAllText(Path.Combine(root, "tools", "ui", "build-review-dashboard.mjs"));

        Assert.Contains("| OPM-02 | P0 | Done | Product Owner + Designer + Developer | Admin Management UI Shell |", productPlan, StringComparison.Ordinal);
        Assert.Contains("| OPM-02 | P0 | Done | Add admin management UI shell. |", backlog, StringComparison.Ordinal);
        Assert.Contains("[Organization And Project Management OPM-02](organization-project-management-opm02.md)", docsIndex, StringComparison.Ordinal);
        Assert.Contains("Status: OPM-01 through OPM-08 implemented.", opm01, StringComparison.Ordinal);
        Assert.Contains("# Organization And Project Management OPM-02", opm02, StringComparison.Ordinal);
        Assert.Contains("OrganizationProjectManagementOpm02Tests", testing, StringComparison.Ordinal);

        Assert.Contains("""<option value="management">Management</option>""", html, StringComparison.Ordinal);
        Assert.Contains("id=\"project-status-filter\"", html, StringComparison.Ordinal);
        Assert.Contains("data-management-filter", html, StringComparison.Ordinal);
        Assert.Contains("src/admin/management-panel.ts", buildScript, StringComparison.Ordinal);
        Assert.Contains("tools/ui/src/admin/management-panel.ts", script, StringComparison.Ordinal);

        foreach (var fragment in new[]
                 {
                     "/api/admin/organizations",
                     "/api/admin/organizations/${selected.id}",
                     "/api/admin/projects",
                     "/api/admin/projects/${selected.id}",
                     "renderManagementList",
                     "renderManagementDetail",
                     "renderManagementSourceDetail",
                     "loadOptionalManagementDetail",
                     "refreshManagementActivityDetailBestEffort",
                     "Loading organization/project management",
                     "No visible organizations or projects",
                     "Management detail is not visible to the caller",
                     "Project Registration",
                     "Access Management",
                     "Payload-safe detail"
                 })
        {
            Assert.Contains(fragment, sourcePanel, StringComparison.Ordinal);
            Assert.Contains(fragment, script, StringComparison.Ordinal);
        }

        Assert.Contains("mode: \"memory\" | \"events\" | \"operations\" | \"management\" | \"access\" | \"registration\" | \"compliance\" | \"pilot\"", adminConsole, StringComparison.Ordinal);
        Assert.Contains("loadManagement", adminConsole, StringComparison.Ordinal);
        Assert.Contains("[data-management-filter]", adminConsole, StringComparison.Ordinal);
        Assert.Contains(".management-count-grid", css, StringComparison.Ordinal);
        Assert.Contains(".management-section-title", css, StringComparison.Ordinal);
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
