namespace MemorySystem.UnitTests;

public sealed class MemoryVsMarkdownPolicyTests
{
    [Fact]
    public void Memory_vs_markdown_policy_is_canonical_linked_and_seeded()
    {
        var root = FindRepositoryRoot();
        var policyPath = Path.Combine(root, "docs", "memory-vs-markdown-policy.md");
        var productPlanPath = Path.Combine(root, "docs", "product-improvement-plan.md");
        var docsIndexPath = Path.Combine(root, "docs", "README.md");
        var folderStructurePath = Path.Combine(root, "docs", "folder-structure.md");
        var rootReadmePath = Path.Combine(root, "README.md");
        var runbookPath = Path.Combine(root, "docs", "project-memory-runbook.md");
        var boundaryPath = Path.Combine(root, "docs", "project-memory-boundary.md");
        var seedPath = Path.Combine(root, "scripts", "seed-production-knowledge-base.sh");

        Assert.True(File.Exists(policyPath));

        var policy = File.ReadAllText(policyPath);
        Assert.Contains("# Memory vs Markdown Policy", policy, StringComparison.Ordinal);
        Assert.Contains("Status: active project policy for improvement plan item IP-01.", policy, StringComparison.Ordinal);
        Assert.Contains("Markdown is the canonical source for project plans", policy, StringComparison.Ordinal);
        Assert.Contains("Memory is a governed retrieval layer over source-backed facts", policy, StringComparison.Ordinal);
        Assert.Contains("Backlog And Improvement Plan Rule", policy, StringComparison.Ordinal);
        Assert.Contains("Role lenses are role-specific interpretations of shared truth", policy, StringComparison.Ordinal);
        Assert.Contains("Release decisions and pilot claims require committed, payload-safe evidence", policy, StringComparison.Ordinal);
        Assert.Contains("scripts/seed-production-knowledge-base.sh", policy, StringComparison.Ordinal);

        var productPlan = File.ReadAllText(productPlanPath);
        Assert.Contains("## Ordered Improvement Backlog", productPlan, StringComparison.Ordinal);
        Assert.Contains("| IP-01 | P0 | Done | Knowledge Steward + Product Owner | Memory vs Markdown Policy Cleanup |", productPlan, StringComparison.Ordinal);
        Assert.Contains("| IP-12 | P1 | Todo | Product Owner | Backlog And Roadmap Memory Sync |", productPlan, StringComparison.Ordinal);

        var docsIndex = File.ReadAllText(docsIndexPath);
        Assert.Contains("[Memory vs Markdown Policy](memory-vs-markdown-policy.md)", docsIndex, StringComparison.Ordinal);

        var folderStructure = File.ReadAllText(folderStructurePath);
        Assert.Contains("Source-Of-Truth Policy", folderStructure, StringComparison.Ordinal);
        Assert.Contains("docs/memory-vs-markdown-policy.md", folderStructure, StringComparison.Ordinal);

        var rootReadme = File.ReadAllText(rootReadmePath);
        Assert.Contains("docs/memory-vs-markdown-policy.md", rootReadme, StringComparison.Ordinal);

        var runbook = File.ReadAllText(runbookPath);
        Assert.Contains("[Memory vs Markdown Policy](memory-vs-markdown-policy.md)", runbook, StringComparison.Ordinal);

        var boundary = File.ReadAllText(boundaryPath);
        Assert.Contains("memory-vs-markdown-policy.md", boundary, StringComparison.Ordinal);

        var seed = File.ReadAllText(seedPath);
        Assert.Contains("\"memory-vs-markdown-policy\"", seed, StringComparison.Ordinal);
        Assert.Contains("\"subject\": \"project source-of-truth policy\"", seed, StringComparison.Ordinal);
        Assert.Contains("\"subject\": \"source-backed memory sync policy\"", seed, StringComparison.Ordinal);
        Assert.Contains("\"subject\": \"role lens boundary\"", seed, StringComparison.Ordinal);
        Assert.Contains("\"subject\": \"release evidence boundary\"", seed, StringComparison.Ordinal);
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
