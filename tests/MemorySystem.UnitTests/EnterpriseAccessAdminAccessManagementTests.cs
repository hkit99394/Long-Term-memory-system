namespace MemorySystem.UnitTests;

public sealed partial class EnterpriseAccessImplementationTests
{
    [Fact]
    public void Admin_access_management_ui_contract_is_documented_and_guarded()
    {
        var root = FindRepositoryRoot();
        var endpoint = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "Admin", "AdminAccessManagementEndpointExtensions.cs"));
        var requests = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "Admin", "AdminAccessManagementRequests.cs"));
        var responses = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "Admin", "AdminAccessManagementResponses.cs"));
        var contract = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Application", "Admin", "IAdminAccessManagementStore.cs"));
        var store = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Infrastructure", "Admin", "PostgresAdminAccessManagementStore.cs"));
        var authorizer = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Application", "Access", "IMemoryAccessAuthorizer.cs"));
        var registration = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "Admin", "ApiAdminServiceCollectionExtensions.cs"));
        var program = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "Program.cs"));
        var html = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "wwwroot", "admin", "index.html"));
        var script = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "wwwroot", "admin", "admin-console.js"));
        var backlog = File.ReadAllText(Path.Combine(root, "docs", "backlog.md"));
        var enterpriseGate = File.ReadAllText(Path.Combine(root, "docs", "enterprise-access-gate.md"));
        var productPlan = File.ReadAllText(Path.Combine(root, "docs", "product-improvement-plan.md"));

        Assert.Contains("/api/admin/access/organization-memberships", endpoint, StringComparison.Ordinal);
        Assert.Contains("/api/admin/access/project-memberships", endpoint, StringComparison.Ordinal);
        Assert.Contains("/api/admin/access/role-assignments", endpoint, StringComparison.Ordinal);
        Assert.Contains("/api/admin/access/namespace-grants", endpoint, StringComparison.Ordinal);
        Assert.Contains("/api/admin/access/effective-preview", endpoint, StringComparison.Ordinal);
        Assert.Contains("AuthorizeOperatorAsync", endpoint, StringComparison.Ordinal);
        Assert.Contains("PreviewAsync", endpoint, StringComparison.Ordinal);
        Assert.Contains("Operators cannot grant themselves admin", endpoint, StringComparison.Ordinal);
        Assert.Contains("AdminOrganizationMembershipRequest", requests, StringComparison.Ordinal);
        Assert.Contains("AdminEffectiveAccessPreviewResponse", responses, StringComparison.Ordinal);

        Assert.Contains("IAdminAccessManagementStore", contract, StringComparison.Ordinal);
        Assert.Contains("UpsertOrganizationMembershipAsync", contract, StringComparison.Ordinal);
        Assert.Contains("UpsertProjectMembershipAsync", contract, StringComparison.Ordinal);
        Assert.Contains("UpsertRoleAssignmentAsync", contract, StringComparison.Ordinal);
        Assert.Contains("UpsertNamespaceGrantAsync", contract, StringComparison.Ordinal);
        Assert.Contains("AccessAuditActionTypes.OrganizationMembershipChange", store, StringComparison.Ordinal);
        Assert.Contains("AccessAuditActionTypes.ProjectMembershipChange", store, StringComparison.Ordinal);
        Assert.Contains("AccessAuditActionTypes.RoleAssignmentChange", store, StringComparison.Ordinal);
        Assert.Contains("AccessAuditActionTypes.NamespaceGrantChange", store, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO memory_access_grants", store, StringComparison.Ordinal);
        Assert.Contains("Task<MemoryAccessDecision> PreviewAsync", authorizer, StringComparison.Ordinal);
        Assert.Contains("IAdminAccessManagementStore, PostgresAdminAccessManagementStore", registration, StringComparison.Ordinal);
        Assert.Contains("MapMemorySystemAdminAccessManagementEndpoints", program, StringComparison.Ordinal);

        Assert.Contains("""<option value="access">Access</option>""", html, StringComparison.Ordinal);
        Assert.Contains("/api/admin/access/project-memberships", script, StringComparison.Ordinal);
        Assert.Contains("/api/admin/access/effective-preview", script, StringComparison.Ordinal);
        Assert.Contains("Access management", script, StringComparison.Ordinal);

        Assert.Contains("| EA-06 | P0 | Done | Add admin access-management UI.", backlog, StringComparison.Ordinal);
        Assert.Contains("| EA-06 | P0 | Done | Add admin access-management UI.", enterpriseGate, StringComparison.Ordinal);
        Assert.Contains("The next move should be `GC-08`", productPlan, StringComparison.Ordinal);
    }
}
