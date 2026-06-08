namespace MemorySystem.UnitTests;

public sealed class ProjectSuccessBenchmarkPs02Tests
{
    [Fact]
    public void Ps02_pilot_selection_and_baseline_are_documented()
    {
        var root = FindRepositoryRoot();
        var productPlan = File.ReadAllText(Path.Combine(root, "docs", "product-improvement-plan.md"));
        var backlog = File.ReadAllText(Path.Combine(root, "docs", "backlog.md"));
        var ps01 = File.ReadAllText(Path.Combine(root, "docs", "project-success-benchmark-ps01.md"));
        var ps02 = File.ReadAllText(Path.Combine(root, "docs", "project-success-pilot-baseline-ps02.md"));
        var docsIndex = File.ReadAllText(Path.Combine(root, "docs", "README.md"));
        var folderStructure = File.ReadAllText(Path.Combine(root, "docs", "folder-structure.md"));
        var benchmarkReadme = File.ReadAllText(Path.Combine(root, "benchmarks", "project-success", "README.md"));

        Assert.Contains("| PS-02 | P0 | Done | Product Owner + Knowledge Steward + Pilot Operator | First Pilot Selection And Baseline |", productPlan, StringComparison.Ordinal);
        Assert.Contains("| PS-02 | P0 | Done | Select first pilot project and baseline. |", backlog, StringComparison.Ordinal);
        Assert.Contains("| PS-02 | P0 | Done | Select first pilot project and baseline. |", ps01, StringComparison.Ordinal);
        Assert.Contains("# Project Success Pilot Baseline PS-02", ps02, StringComparison.Ordinal);
        Assert.Contains("[Project Success Pilot Baseline PS-02](project-success-pilot-baseline-ps02.md)", docsIndex, StringComparison.Ordinal);
        Assert.Contains("docs/project-success-pilot-baseline-ps02.md", folderStructure, StringComparison.Ordinal);
        Assert.Contains("project-success-pilot-baseline-ps02.md", benchmarkReadme, StringComparison.Ordinal);

        foreach (var required in new[]
        {
            "Long-Term Memory System",
            "Internal dogfood pilot",
            "9f8e7d6c-5b4a-4321-9123-abcdef123001",
            "9f8e7d6c-5b4a-4321-9123-abcdef123002",
            "2026-06-15 to 2026-06-28",
            "2026-07-05"
        })
        {
            Assert.Contains(required, ps02, StringComparison.Ordinal);
        }

        foreach (var roleId in ExpectedRoleIds)
        {
            Assert.Contains($"`{roleId}`", ps02, StringComparison.Ordinal);
        }

        foreach (var baselineArea in new[]
        {
            "Repeated status reconstruction",
            "Technical-success versus project-success ambiguity",
            "Release-truth drift risk",
            "Manual review burden",
            "Feedback loop incompleteness"
        })
        {
            Assert.Contains(baselineArea, ps02, StringComparison.Ordinal);
        }

        foreach (var payloadSafetyRule in new[]
        {
            "raw source event payloads",
            "memory bodies",
            "review notes",
            "queries",
            "API keys",
            "database connection strings",
            "secret values"
        })
        {
            Assert.Contains(payloadSafetyRule, ps02, StringComparison.Ordinal);
        }

        Assert.Contains("PS-03 may start when", ps02, StringComparison.Ordinal);
        Assert.Contains("weekly memory review and access-boundary review owners are available", ps02, StringComparison.Ordinal);
    }

    private static readonly string[] ExpectedRoleIds =
    [
        "product_owner",
        "cto",
        "security_professional",
        "it_manager",
        "developer",
        "tester_qa",
        "release_manager",
        "knowledge_steward"
    ];

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
