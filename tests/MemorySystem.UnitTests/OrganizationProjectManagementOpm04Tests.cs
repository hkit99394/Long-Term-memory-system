namespace MemorySystem.UnitTests;

public sealed class OrganizationProjectManagementOpm04Tests
{
    [Fact]
    public void Opm04_access_inventory_revocation_is_documented_guarded_and_ui_backed()
    {
        var root = FindRepositoryRoot();
        var productPlan = File.ReadAllText(Path.Combine(root, "docs", "product-improvement-plan.md"));
        var backlog = File.ReadAllText(Path.Combine(root, "docs", "backlog.md"));
        var docsIndex = File.ReadAllText(Path.Combine(root, "docs", "README.md"));
        var testing = File.ReadAllText(Path.Combine(root, "docs", "testing.md"));
        var opm01 = File.ReadAllText(Path.Combine(root, "docs", "organization-project-management-opm01.md"));
        var opm04 = File.ReadAllText(Path.Combine(root, "docs", "organization-project-management-opm04.md"));
        var endpoint = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "Admin", "AdminOrganizationProjectManagementEndpointExtensions.cs"));
        var requests = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "Admin", "AdminAccessInventoryRequests.cs"));
        var responses = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "Admin", "AdminOrganizationProjectManagementResponses.cs"));
        var contract = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Application", "Admin", "IAdminAccessInventoryStore.cs"));
        var registration = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "Admin", "ApiAdminServiceCollectionExtensions.cs"));
        var store = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Infrastructure", "Admin", "PostgresAdminAccessInventoryStore.cs"));
        var sourcePanel = File.ReadAllText(Path.Combine(root, "tools", "ui", "src", "admin", "management-panel.ts"));
        var script = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "wwwroot", "admin", "admin-console.js"));
        var css = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "wwwroot", "admin", "admin-console.css"));

        Assert.Contains("| OPM-04 | P0 | Done | Security Professional + Ops + Developer | Access Inventory And Revocation |", productPlan, StringComparison.Ordinal);
        Assert.Contains("| OPM-04 | P0 | Done | Add access inventory and revocation. |", backlog, StringComparison.Ordinal);
        Assert.Contains("[Organization And Project Management OPM-04](organization-project-management-opm04.md)", docsIndex, StringComparison.Ordinal);
        Assert.Contains("Status: OPM-01 through OPM-08 implemented.", opm01, StringComparison.Ordinal);
        Assert.Contains("# Organization And Project Management OPM-04", opm04, StringComparison.Ordinal);
        Assert.Contains("OrganizationProjectManagementOpm04Tests", testing, StringComparison.Ordinal);

        foreach (var fragment in new[]
                 {
                     "/api/admin/organizations/{organizationId:guid}/access-inventory",
                     "/api/admin/projects/{projectId:guid}/access-inventory",
                     "/api/admin/access/revocations",
                     "AuthorizeRevocationScopeAsync",
                     "IAdminAccessInventoryStore"
                 })
        {
            Assert.Contains(fragment, endpoint, StringComparison.Ordinal);
        }

        Assert.Contains("AdminAccessRevocationRequest", requests, StringComparison.Ordinal);
        Assert.Contains("AdminAccessInventoryResponse", responses, StringComparison.Ordinal);
        Assert.Contains("AdminAccessRevocationResponse", responses, StringComparison.Ordinal);
        Assert.Contains("GetOrganizationInventoryAsync", contract, StringComparison.Ordinal);
        Assert.Contains("RevokeAccessAsync", contract, StringComparison.Ordinal);
        Assert.Contains("IAdminAccessInventoryStore, PostgresAdminAccessInventoryStore", registration, StringComparison.Ordinal);
        Assert.Contains("Scope type is required", endpoint, StringComparison.Ordinal);
        Assert.Contains("Cannot revoke the last organization owner", store, StringComparison.Ordinal);
        Assert.Contains("Operators cannot revoke their own access record", store, StringComparison.Ordinal);
        Assert.Contains("operation\"] = \"revoked\"", store, StringComparison.Ordinal);

        foreach (var fragment in new[]
                 {
                     "managementAccessInventorySection",
                     "revokeManagementAccess",
                     "Access inventory",
                     "Revoke",
                     "/api/admin/access/revocations",
                     "/api/admin/projects/${selected.id}/access-inventory",
                     "/api/admin/organizations/${selected.id}/access-inventory"
                 })
        {
            Assert.Contains(fragment, sourcePanel, StringComparison.Ordinal);
            Assert.Contains(fragment, script, StringComparison.Ordinal);
        }

        Assert.Contains(".management-access-inventory", css, StringComparison.Ordinal);
        Assert.Contains(".management-revoke-form", css, StringComparison.Ordinal);
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
