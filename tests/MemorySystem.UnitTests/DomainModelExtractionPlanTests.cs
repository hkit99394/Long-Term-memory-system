namespace MemorySystem.UnitTests;

public sealed class DomainModelExtractionPlanTests
{
    [Fact]
    public void Domain_model_extraction_plan_is_linked_and_compatibility_first()
    {
        var root = FindRepositoryRoot();
        var planPath = Path.Combine(root, "docs", "domain-model-extraction-lr04.md");
        var decisionPath = Path.Combine(root, "docs", "decisions", "0044-domain-model-extraction-slice.md");
        var backlogPath = Path.Combine(root, "docs", "backlog.md");
        var indexPath = Path.Combine(root, "docs", "README.md");
        var folderStructurePath = Path.Combine(root, "docs", "folder-structure.md");

        Assert.True(File.Exists(planPath));
        Assert.True(File.Exists(decisionPath));

        var plan = File.ReadAllText(planPath);
        Assert.Contains("MemoryScope", plan, StringComparison.Ordinal);
        Assert.Contains("MemoryNamespace", plan, StringComparison.Ordinal);
        Assert.Contains("MemoryLifecycleStatus", plan, StringComparison.Ordinal);
        Assert.Contains("MemoryRetentionClass", plan, StringComparison.Ordinal);
        Assert.Contains("MemorySensitivity", plan, StringComparison.Ordinal);
        Assert.Contains("SourceEvidenceReference", plan, StringComparison.Ordinal);
        Assert.Contains("Compatibility Test Matrix", plan, StringComparison.Ordinal);
        Assert.Contains("ApiMemoryProposalTests", plan, StringComparison.Ordinal);
        Assert.Contains("ApiMemorySearchTests.ContextPacket", plan, StringComparison.Ordinal);
        Assert.Contains("Scenario 0001", plan, StringComparison.Ordinal);

        var decision = File.ReadAllText(decisionPath);
        Assert.Contains("planning-first extraction scope", decision, StringComparison.Ordinal);
        Assert.Contains("database columns", decision, StringComparison.Ordinal);
        Assert.Contains("SQL migrations", decision, StringComparison.Ordinal);
        Assert.Contains("behavior stable during extraction", decision, StringComparison.Ordinal);

        var backlog = File.ReadAllText(backlogPath);
        Assert.Contains("| LR-04 | P1 | Done | Define Domain model extraction slice.", backlog, StringComparison.Ordinal);
        Assert.Contains("| DM-01 | P0 | Done | Add pure Domain value objects.", backlog, StringComparison.Ordinal);

        var index = File.ReadAllText(indexPath);
        Assert.Contains("[Domain Model Extraction LR-04](domain-model-extraction-lr04.md)", index, StringComparison.Ordinal);
        Assert.Contains("[Decision 0044: Domain Model Extraction Slice](decisions/0044-domain-model-extraction-slice.md)", index, StringComparison.Ordinal);

        var folderStructure = File.ReadAllText(folderStructurePath);
        Assert.Contains("Domain Model Extraction LR-04", folderStructure, StringComparison.Ordinal);
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
