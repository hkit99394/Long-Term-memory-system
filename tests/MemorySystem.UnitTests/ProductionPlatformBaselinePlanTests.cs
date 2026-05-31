namespace MemorySystem.UnitTests;

public sealed class ProductionPlatformBaselinePlanTests
{
    [Fact]
    public void Production_platform_baseline_selects_platform_iac_and_artifact_contract()
    {
        var root = FindRepositoryRoot();
        var planPath = Path.Combine(root, "docs", "production-platform-baseline-pi01.md");
        var decisionPath = Path.Combine(root, "docs", "decisions", "0046-production-platform-and-iac-baseline.md");
        var backlogPath = Path.Combine(root, "docs", "backlog.md");
        var indexPath = Path.Combine(root, "docs", "README.md");

        Assert.True(File.Exists(planPath));
        Assert.True(File.Exists(decisionPath));

        var plan = File.ReadAllText(planPath);
        Assert.Contains("Amazon ECS on Fargate", plan, StringComparison.Ordinal);
        Assert.Contains("Amazon RDS for PostgreSQL", plan, StringComparison.Ordinal);
        Assert.Contains("pgvector", plan, StringComparison.Ordinal);
        Assert.Contains("Amazon ECR", plan, StringComparison.Ordinal);
        Assert.Contains("Terraform under `infra/terraform`", plan, StringComparison.Ordinal);
        Assert.Contains("single immutable OCI image", plan, StringComparison.Ordinal);
        Assert.Contains("deploy by image digest", plan, StringComparison.Ordinal);
        Assert.Contains("Terraform state must not contain", plan, StringComparison.Ordinal);
        Assert.Contains("PI-02", plan, StringComparison.Ordinal);

        var decision = File.ReadAllText(decisionPath);
        Assert.Contains("AWS as the first platform target", decision, StringComparison.Ordinal);
        Assert.Contains("single immutable multi-role OCI image", decision, StringComparison.Ordinal);
        Assert.Contains("Terraform may create cloud resources and secret references", decision, StringComparison.Ordinal);

        var backlog = File.ReadAllText(backlogPath);
        Assert.Contains("| PI-01 | P0 | Done | Choose production platform and IaC baseline.", backlog, StringComparison.Ordinal);
        Assert.Contains("AWS ECS Fargate, Amazon RDS PostgreSQL with pgvector, Amazon ECR, Terraform", backlog, StringComparison.Ordinal);

        var index = File.ReadAllText(indexPath);
        Assert.Contains("[Production Platform Baseline PI-01](production-platform-baseline-pi01.md)", index, StringComparison.Ordinal);
        Assert.Contains("[Decision 0046: Production Platform And IaC Baseline](decisions/0046-production-platform-and-iac-baseline.md)", index, StringComparison.Ordinal);
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
