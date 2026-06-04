namespace MemorySystem.UnitTests;

public sealed class PilotReadinessEvidenceReviewTests
{
    [Fact]
    public void Pilot_readiness_evidence_review_records_local_evidence_and_v1_go_update()
    {
        var root = FindRepositoryRoot();
        var review = File.ReadAllText(Path.Combine(root, "docs", "pilot-readiness-evidence-review-2026-06-01.md"));
        var index = File.ReadAllText(Path.Combine(root, "docs", "README.md"));
        var productPlan = File.ReadAllText(Path.Combine(root, "docs", "product-improvement-plan.md"));
        var roadmap = File.ReadAllText(Path.Combine(root, "docs", "roadmap.md"));

        Assert.Contains("# Pilot Readiness Evidence Review", review, StringComparison.Ordinal);
        Assert.Contains("Status: Local evidence reviewed; external pilot invite is not approved yet.", review, StringComparison.Ordinal);
        Assert.Contains("No-go for inviting the first external pilot user today.", review, StringComparison.Ordinal);
        Assert.Contains("External Pilot GO EPR-04 v1.0.0", review, StringComparison.Ordinal);
        Assert.Contains("Memory Lift | `+1.500`", review, StringComparison.Ordinal);
        Assert.Contains("Contract Lift | `+2.000`", review, StringComparison.Ordinal);
        Assert.Contains("Scoped-safety leak count | `0`", review, StringComparison.Ordinal);
        Assert.Contains("Stale-memory usage count | `0`", review, StringComparison.Ordinal);
        Assert.Contains("Source-link coverage | `1.000`", review, StringComparison.Ordinal);
        Assert.Contains("./scripts/governance-compliance-release-smoke.sh", review, StringComparison.Ordinal);
        Assert.Contains("release_evidence_bucket", review, StringComparison.Ordinal);
        Assert.Contains("Run a target-environment pilot rehearsal.", review, StringComparison.Ordinal);
        Assert.Contains("post-GO hardening work", review, StringComparison.Ordinal);

        Assert.Contains(
            "[Pilot Readiness Evidence Review](pilot-readiness-evidence-review-2026-06-01.md)",
            index,
            StringComparison.Ordinal);
        Assert.Contains(
            "The next move should be a target-environment pilot rehearsal",
            productPlan,
            StringComparison.Ordinal);
        Assert.Contains(
            "Next milestone: version 1.0.0 external pilot execution",
            roadmap,
            StringComparison.Ordinal);
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
