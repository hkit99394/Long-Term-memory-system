namespace MemorySystem.UnitTests;

public sealed class DocumentationTruthCleanupP1Tests
{
    [Fact]
    public void Documentation_truth_cleanup_records_current_external_pilot_state()
    {
        var root = FindRepositoryRoot();
        var cleanup = File.ReadAllText(Path.Combine(root, "docs", "documentation-truth-cleanup-p1-2026-06-04.md"));
        var backlog = File.ReadAllText(Path.Combine(root, "docs", "backlog.md"));
        var index = File.ReadAllText(Path.Combine(root, "docs", "README.md"));
        var folderStructure = File.ReadAllText(Path.Combine(root, "docs", "folder-structure.md"));
        var epr04 = File.ReadAllText(Path.Combine(root, "docs", "external-pilot-go-no-go-epr04-2026-06-04.md"));
        var pi08 = File.ReadAllText(Path.Combine(root, "docs", "production-platform-rehearsal-pi08.md"));
        var gc08 = File.ReadAllText(Path.Combine(root, "docs", "governance-compliance-release-smoke-gc08.md"));
        var governanceGate = File.ReadAllText(Path.Combine(root, "docs", "governance-compliance-gate-lr06.md"));
        var lr03 = File.ReadAllText(Path.Combine(root, "docs", "benchmark-release-gate-lr03.md"));
        var releaseChecklist = File.ReadAllText(Path.Combine(root, "docs", "production-release-checklists-pi07.md"));
        var readinessReview = File.ReadAllText(Path.Combine(root, "docs", "pilot-readiness-evidence-review-2026-06-01.md"));

        Assert.Contains("# Documentation Truth Cleanup P1", cleanup, StringComparison.Ordinal);
        Assert.Contains("Status: first pass complete for external-pilot readiness docs.", cleanup, StringComparison.Ordinal);
        Assert.Contains("EPR-04 is blocked with a formal NO-GO record.", cleanup, StringComparison.Ordinal);
        Assert.Contains("Canonical Source Of Truth", cleanup, StringComparison.Ordinal);

        Assert.Contains(
            "[Documentation Truth Cleanup P1](documentation-truth-cleanup-p1-2026-06-04.md)",
            index,
            StringComparison.Ordinal);
        Assert.Contains(
            "documentation-truth-cleanup-p1-2026-06-04.md",
            folderStructure,
            StringComparison.Ordinal);
        Assert.Contains(
            "| EPR-05 | P1 | Done | Reconcile external-pilot documentation truth.",
            backlog,
            StringComparison.Ordinal);

        Assert.Contains("P1 docs truth cleanup completed", epr04, StringComparison.Ordinal);
        Assert.Contains("External Pilot Go/No-Go EPR-04", pi08, StringComparison.Ordinal);
        Assert.Contains("external-pilot decision as NO-GO", gc08, StringComparison.Ordinal);
        Assert.Contains("2026-06-04 truth update", governanceGate, StringComparison.Ordinal);
        Assert.Contains("EPR-03 reran the live agent-contract smoke and passed all", lr03, StringComparison.Ordinal);
        Assert.Contains("External Pilot Go/No-Go EPR-04", releaseChecklist, StringComparison.Ordinal);
        Assert.Contains("2026-06-04 P1 truth update", readinessReview, StringComparison.Ordinal);
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
