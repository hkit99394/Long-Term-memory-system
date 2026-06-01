namespace MemorySystem.UnitTests;

public sealed class GovernanceComplianceGateTests
{
    [Fact]
    public void Governance_compliance_gate_is_linked_and_boundary_first()
    {
        var root = FindRepositoryRoot();
        var planPath = Path.Combine(root, "docs", "governance-compliance-gate-lr06.md");
        var decisionPath = Path.Combine(root, "docs", "decisions", "0048-governance-compliance-gate.md");
        var backlogPath = Path.Combine(root, "docs", "backlog.md");
        var indexPath = Path.Combine(root, "docs", "README.md");
        var folderStructurePath = Path.Combine(root, "docs", "folder-structure.md");
        var roadmapPath = Path.Combine(root, "docs", "roadmap.md");
        var productPlanPath = Path.Combine(root, "docs", "product-improvement-plan.md");

        Assert.True(File.Exists(planPath));
        Assert.True(File.Exists(decisionPath));

        var plan = File.ReadAllText(planPath);
        Assert.Contains("Data Residency Contract", plan, StringComparison.Ordinal);
        Assert.Contains("Backup Erasure Replay", plan, StringComparison.Ordinal);
        Assert.Contains("Permission-Drift Reporting", plan, StringComparison.Ordinal);
        Assert.Contains("Environment Retention Policy", plan, StringComparison.Ordinal);
        Assert.Contains("Compliance Evidence Package", plan, StringComparison.Ordinal);
        Assert.Contains("GC-01", plan, StringComparison.Ordinal);
        Assert.Contains("no schema, endpoint, or runtime behavior changes", plan, StringComparison.Ordinal);

        var decision = File.ReadAllText(decisionPath);
        Assert.Contains("environment data residency and retention policy", decision, StringComparison.Ordinal);
        Assert.Contains("backup erasure replay", decision, StringComparison.Ordinal);
        Assert.Contains("permission-drift reporting", decision, StringComparison.Ordinal);
        Assert.Contains("payload-safe", decision, StringComparison.Ordinal);

        var backlog = File.ReadAllText(backlogPath);
        Assert.Contains("| LR-06 | P1 | Done | Scope governance and compliance gate.", backlog, StringComparison.Ordinal);
        Assert.Contains("| GC-01 | P0 | Done | Define environment governance policy contract.", backlog, StringComparison.Ordinal);
        Assert.Contains("| GC-08 | P1 | Todo | Add governance/compliance release smoke.", backlog, StringComparison.Ordinal);

        var index = File.ReadAllText(indexPath);
        Assert.Contains("[Governance And Compliance Gate LR-06](governance-compliance-gate-lr06.md)", index, StringComparison.Ordinal);
        Assert.Contains("[Decision 0048: Governance And Compliance Gate](decisions/0048-governance-compliance-gate.md)", index, StringComparison.Ordinal);

        var folderStructure = File.ReadAllText(folderStructurePath);
        Assert.Contains("governance-compliance-gate-lr06.md", folderStructure, StringComparison.Ordinal);

        var roadmap = File.ReadAllText(roadmapPath);
        Assert.Contains("[Decision 0048](decisions/0048-governance-compliance-gate.md)", roadmap, StringComparison.Ordinal);

        var productPlan = File.ReadAllText(productPlanPath);
        Assert.Contains("The next move should be `GC-07`", productPlan, StringComparison.Ordinal);
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
