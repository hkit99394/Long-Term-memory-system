namespace MemorySystem.UnitTests;

public sealed class ProductionPlatformIntegrationPlanTests
{
    [Fact]
    public void Production_platform_integration_plan_is_linked_and_boundary_first()
    {
        var root = FindRepositoryRoot();
        var planPath = Path.Combine(root, "docs", "production-platform-integration-lr05.md");
        var decisionPath = Path.Combine(root, "docs", "decisions", "0045-production-platform-integration.md");
        var backlogPath = Path.Combine(root, "docs", "backlog.md");
        var indexPath = Path.Combine(root, "docs", "README.md");
        var folderStructurePath = Path.Combine(root, "docs", "folder-structure.md");
        var roadmapPath = Path.Combine(root, "docs", "roadmap.md");

        Assert.True(File.Exists(planPath));
        Assert.True(File.Exists(decisionPath));

        var plan = File.ReadAllText(planPath);
        Assert.Contains("Infrastructure-As-Code Boundary", plan, StringComparison.Ordinal);
        Assert.Contains("Managed PostgreSQL Assumptions", plan, StringComparison.Ordinal);
        Assert.Contains("Backup Exporter Assumptions", plan, StringComparison.Ordinal);
        Assert.Contains("Runtime OpenTelemetry And Exporter Wiring", plan, StringComparison.Ordinal);
        Assert.Contains("Alert Routing", plan, StringComparison.Ordinal);
        Assert.Contains("Environment Release Checklists", plan, StringComparison.Ordinal);
        Assert.Contains("PI-01", plan, StringComparison.Ordinal);
        Assert.Contains("no schema, endpoint, or runtime behavior changes", plan, StringComparison.Ordinal);

        var decision = File.ReadAllText(decisionPath);
        Assert.Contains("infrastructure-as-code boundary", decision, StringComparison.Ordinal);
        Assert.Contains("managed PostgreSQL and backup exporter", decision, StringComparison.Ordinal);
        Assert.Contains("runtime OpenTelemetry", decision, StringComparison.Ordinal);
        Assert.Contains("alert routing", decision, StringComparison.Ordinal);
        Assert.Contains("environment-specific release checklists", decision, StringComparison.Ordinal);

        var backlog = File.ReadAllText(backlogPath);
        Assert.Contains("| LR-05 | P1 | Done | Scope production platform integration.", backlog, StringComparison.Ordinal);
        Assert.Contains("| PI-01 | P0 | Done | Choose production platform and IaC baseline.", backlog, StringComparison.Ordinal);

        var index = File.ReadAllText(indexPath);
        Assert.Contains("[Production Platform Integration LR-05](production-platform-integration-lr05.md)", index, StringComparison.Ordinal);
        Assert.Contains("[Decision 0045: Production Platform Integration](decisions/0045-production-platform-integration.md)", index, StringComparison.Ordinal);

        var folderStructure = File.ReadAllText(folderStructurePath);
        Assert.Contains("Production Platform Integration LR-05", folderStructure, StringComparison.Ordinal);

        var roadmap = File.ReadAllText(roadmapPath);
        Assert.Contains("[Decision 0045](decisions/0045-production-platform-integration.md)", roadmap, StringComparison.Ordinal);
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
