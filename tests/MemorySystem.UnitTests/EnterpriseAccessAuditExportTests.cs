namespace MemorySystem.UnitTests;

public sealed partial class EnterpriseAccessImplementationTests
{
    [Fact]
    public void Admin_audit_export_contract_is_documented_and_guarded()
    {
        var root = FindRepositoryRoot();
        var endpoint = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "Admin", "AdminAuditExportEndpointExtensions.cs"));
        var request = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "Admin", "AdminAuditExportRequests.cs"));
        var contract = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Application", "Admin", "IAdminAuditExportStore.cs"));
        var store = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Infrastructure", "Admin", "PostgresAdminAuditExportStore.cs"));
        var accessManagementStore = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Infrastructure", "Admin", "PostgresAdminAccessManagementStore.cs"));
        var registration = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "Admin", "ApiAdminServiceCollectionExtensions.cs"));
        var program = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "Program.cs"));
        var script = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "wwwroot", "admin", "admin-console.js"));
        var backlog = File.ReadAllText(Path.Combine(root, "docs", "backlog.md"));
        var enterpriseGate = File.ReadAllText(Path.Combine(root, "docs", "enterprise-access-gate.md"));
        var productPlan = File.ReadAllText(Path.Combine(root, "docs", "product-improvement-plan.md"));

        Assert.Contains("/api/admin/audit-exports", endpoint, StringComparison.Ordinal);
        Assert.Contains("application/x-ndjson", endpoint, StringComparison.Ordinal);
        Assert.Contains("contentSha256", endpoint, StringComparison.Ordinal);
        Assert.Contains("SHA256.HashData", endpoint, StringComparison.Ordinal);
        Assert.Contains("AccessAuditActionTypes.AuditExport", endpoint, StringComparison.Ordinal);
        Assert.Contains("MemoryAccessPermissions.Admin", endpoint, StringComparison.Ordinal);
        Assert.Contains("Audit export currently supports org and project scopes.", endpoint, StringComparison.Ordinal);
        Assert.Contains("AdminAuditExportRequest", request, StringComparison.Ordinal);

        Assert.Contains("IAdminAuditExportStore", contract, StringComparison.Ordinal);
        Assert.Contains("AdminAuditExportQuery", contract, StringComparison.Ordinal);
        Assert.Contains("AdminAuditExportEventRecord", contract, StringComparison.Ordinal);
        Assert.Contains("ListAccessAuditEventsAsync", store, StringComparison.Ordinal);
        Assert.Contains("access_audit_events", store, StringComparison.Ordinal);
        Assert.Contains("namespace_prefix", store, StringComparison.Ordinal);
        Assert.Contains("project_memberships", store, StringComparison.Ordinal);
        Assert.Contains("organization_memberships", store, StringComparison.Ordinal);
        Assert.Contains("scopeType, command.ScopeId", accessManagementStore, StringComparison.Ordinal);
        Assert.Contains("IAdminAuditExportStore, PostgresAdminAuditExportStore", registration, StringComparison.Ordinal);
        Assert.Contains("MapMemorySystemAdminAuditExportEndpoints", program, StringComparison.Ordinal);

        Assert.Contains("/api/admin/audit-exports", script, StringComparison.Ordinal);
        Assert.Contains("Audit export", script, StringComparison.Ordinal);
        Assert.Contains("downloadText", script, StringComparison.Ordinal);
        Assert.Contains("content-disposition", script, StringComparison.Ordinal);

        Assert.Contains("| EA-07 | P0 | Done | Add audit export.", backlog, StringComparison.Ordinal);
        Assert.Contains("| EA-07 | P0 | Done | Add audit export.", enterpriseGate, StringComparison.Ordinal);
        Assert.Contains("The next move should be `GC-06`", productPlan, StringComparison.Ordinal);
    }
}
