namespace MemorySystem.UnitTests;

public sealed class AdminUxPolishIp14Tests
{
    [Fact]
    public void Ip14_admin_ux_polish_is_documented_and_wired()
    {
        var root = FindRepositoryRoot();
        var productPlan = File.ReadAllText(Path.Combine(root, "docs", "product-improvement-plan.md"));
        var contract = File.ReadAllText(Path.Combine(root, "docs", "admin-ux-polish-ip14.md"));
        var docsIndex = File.ReadAllText(Path.Combine(root, "docs", "README.md"));
        var folderStructure = File.ReadAllText(Path.Combine(root, "docs", "folder-structure.md"));
        var testing = File.ReadAllText(Path.Combine(root, "docs", "testing.md"));
        var html = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "wwwroot", "admin", "index.html"));
        var script = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "wwwroot", "admin", "admin-console.js"));
        var endpoint = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "Admin", "AdminConsoleEndpointExtensions.cs"));
        var store = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Infrastructure", "Admin", "PostgresAdminMemoryInspectionStore.cs"));

        Assert.Contains("| IP-14 | P2 | Done | Product Owner + Developer | Admin UX Polish |", productPlan, StringComparison.Ordinal);
        Assert.Contains("# Admin UX Polish IP-14", contract, StringComparison.Ordinal);
        Assert.Contains("[Admin UX Polish IP-14](admin-ux-polish-ip14.md)", docsIndex, StringComparison.Ordinal);
        Assert.Contains("docs/admin-ux-polish-ip14.md", folderStructure, StringComparison.Ordinal);
        Assert.Contains("AdminUxPolishIp14Tests", testing, StringComparison.Ordinal);

        foreach (var htmlFragment in new[]
        {
            "<option value=\"operations\">Operations</option>",
            "id=\"credential-state\"",
            "id=\"memory-type-filter\"",
            "id=\"role-filter\"",
            "id=\"namespace-prefix-filter\""
        })
        {
            Assert.Contains(htmlFragment, html, StringComparison.Ordinal);
        }

        foreach (var scriptFragment in new[]
        {
            "/api/operations/summary",
            "/api/operations/metrics",
            "/health/ready",
            "Open reviews",
            "roleId",
            "namespacePrefix",
            "updateCredentialState",
            "renderOperationsDetail"
        })
        {
            Assert.Contains(scriptFragment, script, StringComparison.Ordinal);
        }

        Assert.Contains("NormalizeRoleIdQuery", endpoint, StringComparison.Ordinal);
        Assert.Contains("roleId", endpoint, StringComparison.Ordinal);
        Assert.Contains("@role_id", store, StringComparison.Ordinal);
        Assert.Contains("memory_required_role_id", store, StringComparison.Ordinal);
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
