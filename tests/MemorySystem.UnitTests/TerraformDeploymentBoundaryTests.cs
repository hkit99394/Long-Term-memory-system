namespace MemorySystem.UnitTests;

public sealed class TerraformDeploymentBoundaryTests
{
    [Fact]
    public void Runtime_and_observability_modules_expose_contract_only_deployment_boundary()
    {
        var root = FindRepositoryRoot();
        var terraformReadme = File.ReadAllText(Path.Combine(root, "infra", "terraform", "README.md"));
        var runtimeOutputs = File.ReadAllText(Path.Combine(root, "infra", "terraform", "modules", "memorysystem-runtime", "outputs.tf"));
        var observabilityOutputs = File.ReadAllText(Path.Combine(root, "infra", "terraform", "modules", "memorysystem-observability", "outputs.tf"));
        var productionOutputs = File.ReadAllText(Path.Combine(root, "infra", "terraform", "environments", "production", "outputs.tf"));
        var pilotOutputs = File.ReadAllText(Path.Combine(root, "infra", "terraform", "environments", "pilot", "outputs.tf"));

        Assert.Contains("deployment_boundary", terraformReadme, StringComparison.Ordinal);
        Assert.Contains("runtime_resources_provisioned = false", runtimeOutputs, StringComparison.Ordinal);
        Assert.Contains("apply_creates_runtime", runtimeOutputs, StringComparison.Ordinal);
        Assert.Contains("observability_resources_provisioned", observabilityOutputs, StringComparison.Ordinal);
        Assert.Contains("= false", observabilityOutputs, StringComparison.Ordinal);
        Assert.Contains("apply_creates_observability_integrations", observabilityOutputs, StringComparison.Ordinal);

        foreach (var environmentOutputs in new[] { productionOutputs, pilotOutputs })
        {
            Assert.Contains("platform_deployment_boundary", environmentOutputs, StringComparison.Ordinal);
            Assert.Contains("runtime_resources_provisioned", environmentOutputs, StringComparison.Ordinal);
            Assert.Contains("observability_resources_provisioned", environmentOutputs, StringComparison.Ordinal);
            Assert.Contains("postgres_resources_provisioned", environmentOutputs, StringComparison.Ordinal);
            Assert.Contains("= true", environmentOutputs, StringComparison.Ordinal);
        }
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
