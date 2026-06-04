namespace MemorySystem.UnitTests;

public sealed class PermissionDriftReportContractTests
{
    [Fact]
    public void Gc02_permission_drift_report_is_documented_wired_and_next_slice_is_gc03()
    {
        var root = FindRepositoryRoot();
        var contract = File.ReadAllText(Path.Combine(root, "docs", "permission-drift-report-gc02.md"));
        var backlog = File.ReadAllText(Path.Combine(root, "docs", "backlog.md"));
        var index = File.ReadAllText(Path.Combine(root, "docs", "README.md"));
        var folderStructure = File.ReadAllText(Path.Combine(root, "docs", "folder-structure.md"));
        var productPlan = File.ReadAllText(Path.Combine(root, "docs", "product-improvement-plan.md"));
        var endpoint = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "Admin", "AdminAccessManagementEndpointExtensions.cs"));
        var services = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "Admin", "ApiAdminServiceCollectionExtensions.cs"));
        var store = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Infrastructure", "Admin", "PostgresAdminPermissionDriftReportStore.cs"));
        var reportContract = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Application", "Admin", "IAdminPermissionDriftReportStore.cs"));

        Assert.Contains("# GC-02 Permission-Drift Report", contract, StringComparison.Ordinal);
        Assert.Contains("POST /api/admin/access/permission-drift", contract, StringComparison.Ordinal);
        Assert.Contains("issuerHash", contract, StringComparison.Ordinal);
        Assert.Contains("service credential fingerprints", contract, StringComparison.Ordinal);
        Assert.Contains("IMemoryAccessAuthorizer.PreviewAsync", contract, StringComparison.Ordinal);
        Assert.Contains("The report is diagnostic evidence. It must not become an authorization path.", contract, StringComparison.Ordinal);

        Assert.Contains("| GC-02 | P0 | Done | Add permission-drift report.", backlog, StringComparison.Ordinal);
        Assert.Contains("| GC-03 | P0 | Done | Add backup erasure replay validation.", backlog, StringComparison.Ordinal);
        Assert.Contains("[Permission-Drift Report GC-02](permission-drift-report-gc02.md)", index, StringComparison.Ordinal);
        Assert.Contains("permission-drift-report-gc02.md", folderStructure, StringComparison.Ordinal);
        Assert.Contains("next move should be a target-environment pilot rehearsal", productPlan, StringComparison.Ordinal);

        Assert.Contains("/api/admin/access/permission-drift", endpoint, StringComparison.Ordinal);
        Assert.Contains("AuthorizeOperatorAsync", endpoint, StringComparison.Ordinal);
        Assert.Contains("IAdminPermissionDriftReportStore", endpoint, StringComparison.Ordinal);
        Assert.Contains("PostgresAdminPermissionDriftReportStore", services, StringComparison.Ordinal);
        Assert.Contains("IAdminPermissionDriftReportStore", reportContract, StringComparison.Ordinal);
        Assert.Contains("HashText(reader.GetString(3))", store, StringComparison.Ordinal);
        Assert.DoesNotContain("external_email", store, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("credential_fingerprint,", store, StringComparison.OrdinalIgnoreCase);
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
