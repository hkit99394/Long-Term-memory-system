namespace MemorySystem.UnitTests;

public sealed class ProjectRegistrationReg04Tests
{
    [Fact]
    public void Reg04_break_glass_admin_path_is_separate_documented_and_audited()
    {
        var root = FindRepositoryRoot();
        var productPlan = File.ReadAllText(Path.Combine(root, "docs", "product-improvement-plan.md"));
        var backlog = File.ReadAllText(Path.Combine(root, "docs", "backlog.md"));
        var registrationPlan = File.ReadAllText(Path.Combine(root, "docs", "project-registration-ux-plan.md"));
        var testing = File.ReadAllText(Path.Combine(root, "docs", "testing.md"));
        var requests = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "Admin", "AdminAccessManagementRequests.cs"));
        var endpoint = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "Admin", "AdminAccessManagementEndpointExtensions.cs"));
        var contract = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Application", "Admin", "IAdminAccessManagementStore.cs"));
        var store = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Infrastructure", "Admin", "PostgresAdminAccessManagementStore.cs"));
        var accessPanel = File.ReadAllText(Path.Combine(root, "tools", "ui", "src", "admin", "access-panel.ts"));
        var adminConsole = File.ReadAllText(Path.Combine(root, "tools", "ui", "src", "admin-console.ts"));
        var script = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "wwwroot", "admin", "admin-console.js"));

        Assert.Contains("| REG-04 | P0 | Done | Security Professional + Ops | Least-Privilege Bootstrap Cleanup |", productPlan, StringComparison.Ordinal);
        Assert.Contains("| REG-04 | P0 | Done | Separate bootstrap admin from normal registration.", backlog, StringComparison.Ordinal);
        Assert.Contains("Status: REG-01 through REG-06 implemented.", registrationPlan, StringComparison.Ordinal);
        Assert.Contains("## REG-04 Bootstrap Admin Separation", registrationPlan, StringComparison.Ordinal);
        Assert.Contains("ProjectRegistrationReg04Tests", testing, StringComparison.Ordinal);

        Assert.Contains("AdminBreakGlassGrantEvidenceRequest", requests, StringComparison.Ordinal);
        Assert.Contains("AdminBreakGlassGrantEvidenceCommand", contract, StringComparison.Ordinal);
        Assert.Contains("ValidateBreakGlassNamespaceAdminGrant", endpoint, StringComparison.Ordinal);
        Assert.Contains("RootNamespacePrefixes", endpoint, StringComparison.Ordinal);
        Assert.Contains("Break-glass evidence is required for namespace admin grants.", endpoint, StringComparison.Ordinal);
        Assert.Contains("Break-glass namespace admin grants must target exactly one principal id.", endpoint, StringComparison.Ordinal);
        Assert.Contains("Project root namespace admin grants must stay absent.", endpoint, StringComparison.Ordinal);
        Assert.Contains("Organization root namespace admin grants must stay absent.", endpoint, StringComparison.Ordinal);

        Assert.Contains("breakGlassOwnerRole", store, StringComparison.Ordinal);
        Assert.Contains("breakGlassAcceptedByRole", store, StringComparison.Ordinal);
        Assert.Contains("breakGlassReviewDue", store, StringComparison.Ordinal);
        Assert.Contains("breakGlassAuditEvidenceId", store, StringComparison.Ordinal);

        Assert.Contains("Break-glass namespace admin", accessPanel, StringComparison.Ordinal);
        Assert.Contains("permission: \"admin\"", accessPanel, StringComparison.Ordinal);
        Assert.Contains("breakGlassEvidence", accessPanel, StringComparison.Ordinal);
        Assert.Contains("dateField(\"reviewDue\", \"Review due\")", accessPanel, StringComparison.Ordinal);
        Assert.Contains("selectField(\"permission\", \"Permission\", [\"read\", \"write\", \"review\"])", accessPanel, StringComparison.Ordinal);
        Assert.Contains("Break-glass admin", adminConsole, StringComparison.Ordinal);

        Assert.Contains("Break-glass namespace admin", script, StringComparison.Ordinal);
        Assert.Contains("breakGlassEvidence", script, StringComparison.Ordinal);
        Assert.Contains("dateField(\"reviewDue\", \"Review due\")", script, StringComparison.Ordinal);
        Assert.Contains("selectField(\"permission\", \"Permission\", [\"read\", \"write\", \"review\"])", script, StringComparison.Ordinal);
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
