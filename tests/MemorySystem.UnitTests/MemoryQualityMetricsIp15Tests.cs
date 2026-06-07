namespace MemorySystem.UnitTests;

public sealed class MemoryQualityMetricsIp15Tests
{
    [Fact]
    public void Ip15_contract_is_documented_and_wired()
    {
        var root = FindRepositoryRoot();
        var productPlan = File.ReadAllText(Path.Combine(root, "docs", "product-improvement-plan.md"));
        var contract = File.ReadAllText(Path.Combine(root, "docs", "memory-quality-metrics-ip15.md"));
        var docsIndex = File.ReadAllText(Path.Combine(root, "docs", "README.md"));
        var folderStructure = File.ReadAllText(Path.Combine(root, "docs", "folder-structure.md"));
        var testing = File.ReadAllText(Path.Combine(root, "docs", "testing.md"));
        var summaryModel = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Application", "Operations", "IOperationalSummaryStore.cs"));
        var postgresStore = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Infrastructure", "Operations", "PostgresOperationalSummaryStore.cs"));
        var metricsRenderer = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "Operations", "OperationalMetricsTextRenderer.cs"));
        var adminConsole = File.ReadAllText(Path.Combine(root, "tools", "ui", "src", "admin-console.ts"));
        var alertInputs = File.ReadAllText(Path.Combine(root, "observability", "alert-inputs", "api-metrics.txt"));

        Assert.Contains("| IP-15 | P2 | Done | Tester/QA + Knowledge Steward | Memory Quality Metrics |", productPlan, StringComparison.Ordinal);
        Assert.Contains("# Memory Quality Metrics IP-15", contract, StringComparison.Ordinal);
        Assert.Contains("[Memory Quality Metrics IP-15](memory-quality-metrics-ip15.md)", docsIndex, StringComparison.Ordinal);
        Assert.Contains("docs/memory-quality-metrics-ip15.md", folderStructure, StringComparison.Ordinal);
        Assert.Contains("OperationalSummaryEndpointTests", testing, StringComparison.Ordinal);

        foreach (var required in new[]
        {
            "OperationalMemoryQualitySummary",
            "SourceLinkCoverage",
            "StaleMemoryRate",
            "UsefulFeedbackRate",
            "MissingMemoryReports",
            "RoleBoundaryMisses",
            "DuplicateRatio"
        })
        {
            Assert.Contains(required, summaryModel, StringComparison.Ordinal);
        }

        foreach (var required in new[]
        {
            "memory_facts",
            "role_memory_lenses",
            "memory_retrieval_feedback",
            "role_mismatch",
            "duplicate_groups"
        })
        {
            Assert.Contains(required, postgresStore, StringComparison.Ordinal);
        }

        foreach (var metricName in new[]
        {
            "memorysystem_memory_quality_source_link_coverage",
            "memorysystem_memory_quality_stale_memory_rate",
            "memorysystem_memory_quality_useful_feedback_rate",
            "memorysystem_memory_quality_missing_memory_reports_total",
            "memorysystem_memory_quality_role_boundary_misses_total",
            "memorysystem_memory_quality_duplicate_ratio"
        })
        {
            Assert.Contains(metricName, metricsRenderer, StringComparison.Ordinal);
            Assert.Contains(metricName, alertInputs, StringComparison.Ordinal);
            Assert.Contains(metricName, contract, StringComparison.Ordinal);
        }

        Assert.Contains("memoryQuality", adminConsole, StringComparison.Ordinal);
        Assert.Contains("Memory quality", adminConsole, StringComparison.Ordinal);
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

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
