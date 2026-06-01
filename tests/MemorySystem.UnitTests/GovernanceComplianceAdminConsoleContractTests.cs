namespace MemorySystem.UnitTests;

public sealed class GovernanceComplianceAdminConsoleContractTests
{
    [Fact]
    public void Gc07_governance_compliance_admin_console_is_documented_api_backed_and_payload_safe()
    {
        var root = FindRepositoryRoot();
        var contract = File.ReadAllText(Path.Combine(root, "docs", "governance-compliance-admin-console-gc07.md"));
        var backlog = File.ReadAllText(Path.Combine(root, "docs", "backlog.md"));
        var index = File.ReadAllText(Path.Combine(root, "docs", "README.md"));
        var folderStructure = File.ReadAllText(Path.Combine(root, "docs", "folder-structure.md"));
        var productPlan = File.ReadAllText(Path.Combine(root, "docs", "product-improvement-plan.md"));
        var governancePlan = File.ReadAllText(Path.Combine(root, "docs", "governance-compliance-gate-lr06.md"));
        var endpoint = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "Admin", "AdminConsoleEndpointExtensions.cs"));
        var responses = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "Admin", "AdminMemoryConsoleResponses.cs"));
        var html = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "wwwroot", "admin", "index.html"));
        var script = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "wwwroot", "admin", "admin-console.js"));

        Assert.Contains("# GC-07 Governance/Compliance Admin Console", contract, StringComparison.Ordinal);
        Assert.Contains("/api/admin/compliance/status", contract, StringComparison.Ordinal);
        Assert.Contains("retention_report", contract, StringComparison.Ordinal);
        Assert.Contains("legal_hold_summary", contract, StringComparison.Ordinal);
        Assert.Contains("erasure_replay", contract, StringComparison.Ordinal);
        Assert.Contains("permission_drift_report", contract, StringComparison.Ordinal);
        Assert.Contains("compliance_evidence_package", contract, StringComparison.Ordinal);
        Assert.Contains("raw source payloads are not included", contract, StringComparison.Ordinal);

        Assert.Contains("| GC-07 | P1 | Done | Add governance/compliance admin console view.", backlog, StringComparison.Ordinal);
        Assert.Contains("| GC-08 | P1 | Todo | Add governance/compliance release smoke.", backlog, StringComparison.Ordinal);
        Assert.Contains("[Governance/Compliance Admin Console GC-07](governance-compliance-admin-console-gc07.md)", index, StringComparison.Ordinal);
        Assert.Contains("governance-compliance-admin-console-gc07.md", folderStructure, StringComparison.Ordinal);
        Assert.Contains("The next move should be `GC-08`", productPlan, StringComparison.Ordinal);
        Assert.Contains("product planning points to `GC-08`", governancePlan, StringComparison.Ordinal);

        Assert.Contains("\"/api/admin/compliance/status\"", endpoint, StringComparison.Ordinal);
        Assert.Contains("ReadComplianceStatusAsync", endpoint, StringComparison.Ordinal);
        Assert.Contains("AdminComplianceStatusResponse", responses, StringComparison.Ordinal);
        Assert.Contains("RawSourcePayloadsIncluded", responses, StringComparison.Ordinal);
        Assert.Contains("""<option value="compliance">Compliance</option>""", html, StringComparison.Ordinal);
        Assert.Contains("/api/admin/compliance/status", script, StringComparison.Ordinal);
        Assert.Contains("Compliance", script, StringComparison.Ordinal);
        Assert.Contains("Evidence Links", script, StringComparison.Ordinal);
        Assert.Contains("rawSourcePayloadsIncluded", script, StringComparison.Ordinal);
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
