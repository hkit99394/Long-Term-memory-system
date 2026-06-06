using System.Text.RegularExpressions;

namespace MemorySystem.UnitTests;

public sealed partial class ProductionPlatformTerraformSkeletonTests
{
    [Fact]
    public void Terraform_skeleton_defines_runtime_contract_without_secret_values()
    {
        var root = FindRepositoryRoot();
        var infraRoot = Path.Combine(root, "infra", "terraform");
        var dockerfile = Path.Combine(root, "Dockerfile");
        var dockerIgnore = Path.Combine(root, ".dockerignore");

        Assert.True(Directory.Exists(infraRoot));
        Assert.True(File.Exists(dockerfile));
        Assert.True(File.Exists(dockerIgnore));

        AssertEnvironment(infraRoot, "pilot");
        AssertEnvironment(infraRoot, "production");
        AssertModule(infraRoot, "memorysystem-runtime");
        AssertModule(infraRoot, "memorysystem-postgres");
        AssertModule(infraRoot, "memorysystem-observability");

        var dockerfileText = File.ReadAllText(dockerfile);
        Assert.Contains("mcr.microsoft.com/dotnet/sdk:10.0", dockerfileText, StringComparison.Ordinal);
        Assert.Contains("mcr.microsoft.com/dotnet/aspnet:10.0", dockerfileText, StringComparison.Ordinal);
        Assert.Contains("MemorySystem.Api.csproj", dockerfileText, StringComparison.Ordinal);
        Assert.Contains("MemorySystem.Worker.csproj", dockerfileText, StringComparison.Ordinal);
        Assert.Contains("MemorySystem.Migrator.csproj", dockerfileText, StringComparison.Ordinal);
        Assert.Contains("MemorySystem.DemoSeeder.csproj", dockerfileText, StringComparison.Ordinal);
        Assert.Contains("postgresql-client", dockerfileText, StringComparison.Ordinal);
        Assert.Contains("platform-backup-export.sh", dockerfileText, StringComparison.Ordinal);
        Assert.Contains("platform-compliance-evidence-package.sh", dockerfileText, StringComparison.Ordinal);
        Assert.Contains("platform-erasure-replay-ledger-export.sh", dockerfileText, StringComparison.Ordinal);
        Assert.Contains("platform-external-payload-retention-check.sh", dockerfileText, StringComparison.Ordinal);
        Assert.Contains("platform-retention-minimization.sh", dockerfileText, StringComparison.Ordinal);
        Assert.Contains("platform-restore-validation.sh", dockerfileText, StringComparison.Ordinal);
        Assert.Contains("COPY migrations/ migrations/", dockerfileText, StringComparison.Ordinal);
        Assert.Contains("CMD [\"dotnet\", \"/app/api/MemorySystem.Api.dll\"]", dockerfileText, StringComparison.Ordinal);
        Assert.DoesNotContain("ENTRYPOINT [\"dotnet\", \"/app/api/MemorySystem.Api.dll\"]", dockerfileText, StringComparison.Ordinal);

        var allTerraformText = string.Join(
            "\n",
            Directory.EnumerateFiles(infraRoot, "*.tf", SearchOption.AllDirectories)
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));

        Assert.Contains("module \"runtime\"", allTerraformText, StringComparison.Ordinal);
        Assert.Contains("module \"postgres\"", allTerraformText, StringComparison.Ordinal);
        Assert.Contains("module \"observability\"", allTerraformText, StringComparison.Ordinal);
        Assert.Contains("@sha256:[a-f0-9]{64}", allTerraformText, StringComparison.Ordinal);
        Assert.Contains("secret_arns", allTerraformText, StringComparison.Ordinal);
        Assert.Contains("application_secret_arns", allTerraformText, StringComparison.Ordinal);
        Assert.Contains("MemorySystem.Migrator.dll", allTerraformText, StringComparison.Ordinal);
        Assert.Contains("MemorySystem.Worker.dll", allTerraformText, StringComparison.Ordinal);
        Assert.Contains("required_extensions", allTerraformText, StringComparison.Ordinal);
        Assert.Contains("\"vector\"", allTerraformText, StringComparison.Ordinal);

        foreach (var terraformFile in Directory.EnumerateFiles(infraRoot, "*.tf", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(terraformFile);
            Assert.DoesNotMatch(RawSecretVariableRegex(), text);
            Assert.DoesNotContain("private-alpha-local-key", text, StringComparison.Ordinal);
            Assert.DoesNotContain("memory_system_dev_password", text, StringComparison.Ordinal);
        }

        var index = File.ReadAllText(Path.Combine(root, "docs", "README.md"));
        Assert.Contains("[Terraform Platform PI-02/PI-04](../infra/terraform/README.md)", index, StringComparison.Ordinal);

        var backlog = File.ReadAllText(Path.Combine(root, "docs", "backlog.md"));
        Assert.Contains("| PI-02 | P0 | Done | Add Terraform IaC skeleton for runtime roles.", backlog, StringComparison.Ordinal);
        Assert.Contains("| PI-03 | P0 | Done | Provision Amazon RDS PostgreSQL with pgvector.", backlog, StringComparison.Ordinal);
        Assert.Contains("| PI-04 | P0 | Done | Add backup exporter and restore validation automation.", backlog, StringComparison.Ordinal);
    }

    private static void AssertEnvironment(string infraRoot, string name)
    {
        var environmentRoot = Path.Combine(infraRoot, "environments", name);

        Assert.True(File.Exists(Path.Combine(environmentRoot, "main.tf")));
        Assert.True(File.Exists(Path.Combine(environmentRoot, "variables.tf")));
        Assert.True(File.Exists(Path.Combine(environmentRoot, "outputs.tf")));
        Assert.True(File.Exists(Path.Combine(environmentRoot, "terraform.tfvars.example")));

        var main = File.ReadAllText(Path.Combine(environmentRoot, "main.tf"));
        Assert.Contains("source = \"../../modules/memorysystem-runtime\"", main, StringComparison.Ordinal);
        Assert.Contains("source = \"../../modules/memorysystem-postgres\"", main, StringComparison.Ordinal);
        Assert.Contains("source = \"../../modules/memorysystem-observability\"", main, StringComparison.Ordinal);

        var example = File.ReadAllText(Path.Combine(environmentRoot, "terraform.tfvars.example"));
        Assert.Contains("@sha256:0000000000000000000000000000000000000000000000000000000000000000", example, StringComparison.Ordinal);
        Assert.Contains("arn:aws:secretsmanager", example, StringComparison.Ordinal);
        Assert.DoesNotContain("password", example, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private-alpha-local-key", example, StringComparison.Ordinal);
    }

    private static void AssertModule(string infraRoot, string name)
    {
        var moduleRoot = Path.Combine(infraRoot, "modules", name);

        Assert.True(File.Exists(Path.Combine(moduleRoot, "README.md")));
        Assert.True(File.Exists(Path.Combine(moduleRoot, "main.tf")));
        Assert.True(File.Exists(Path.Combine(moduleRoot, "variables.tf")));
        Assert.True(File.Exists(Path.Combine(moduleRoot, "outputs.tf")));
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

    [GeneratedRegex("variable\\s+\\\"[^\\\"]*(password|api_key|connection_string|token|secret_value)[^\\\"]*\\\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RawSecretVariableRegex();
}
