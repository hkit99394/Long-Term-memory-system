namespace MemorySystem.UnitTests;

public sealed class ExternalManagedPostgresProfileTests
{
    [Fact]
    public void External_managed_postgres_profile_is_documented_configured_and_smokeable()
    {
        var root = FindRepositoryRoot();
        var productPlan = File.ReadAllText(Path.Combine(root, "docs", "product-improvement-plan.md"));
        var docsIndex = File.ReadAllText(Path.Combine(root, "docs", "README.md"));
        var profileDoc = File.ReadAllText(Path.Combine(root, "docs", "external-managed-postgres-profile.md"));
        var productionContainerDoc = File.ReadAllText(Path.Combine(root, "docs", "production-container.md"));
        var productionSecretsDoc = File.ReadAllText(Path.Combine(root, "docs", "production-secrets.md"));
        var testingDoc = File.ReadAllText(Path.Combine(root, "docs", "testing.md"));
        var compose = File.ReadAllText(Path.Combine(root, "docker-compose.production.yml"));
        var externalCompose = File.ReadAllText(Path.Combine(root, "docker-compose.production.external-postgres.yml"));
        var envExample = File.ReadAllText(Path.Combine(root, ".env.production.example"));
        var productionScript = File.ReadAllText(Path.Combine(root, "scripts", "production-container.sh"));
        var boundarySeed = File.ReadAllText(Path.Combine(root, "scripts", "seed-production-memory-boundary.sh"));
        var smoke = File.ReadAllText(Path.Combine(root, "scripts", "external-postgres-profile-smoke.sh"));

        Assert.Contains("| IP-03 | P0 | Done | IT/Ops + CTO | External / Managed PostgreSQL Production Profile |", productPlan, StringComparison.Ordinal);
        Assert.Contains("[External / Managed PostgreSQL Production Profile](external-managed-postgres-profile.md)", docsIndex, StringComparison.Ordinal);
        Assert.Contains("Status: active production profile for improvement plan item IP-03.", profileDoc, StringComparison.Ordinal);
        Assert.Contains("MEMORYSYSTEM_POSTGRES_PROFILE=external", profileDoc, StringComparison.Ordinal);
        Assert.Contains("MEMORYSYSTEM_POSTGRES_CONNECTION_STRING", profileDoc, StringComparison.Ordinal);
        Assert.Contains("scripts/production-container.sh migrate", profileDoc, StringComparison.Ordinal);
        Assert.Contains("/app/scripts/platform-restore-validation.sh", profileDoc, StringComparison.Ordinal);
        Assert.Contains("./scripts/external-postgres-profile-smoke.sh", profileDoc, StringComparison.Ordinal);

        Assert.Contains("MEMORYSYSTEM_POSTGRES_PROFILE=local", envExample, StringComparison.Ordinal);
        Assert.Contains("MEMORYSYSTEM_POSTGRES_CONNECTION_STRING=", envExample, StringComparison.Ordinal);
        Assert.Contains("SSL Mode=VerifyFull", envExample, StringComparison.Ordinal);
        Assert.Contains("MEMORYSYSTEM_POSTGRES_URL=", envExample, StringComparison.Ordinal);

        Assert.Contains("MEMORYSYSTEM_POSTGRES_CONNECTION_STRING: ${MEMORYSYSTEM_POSTGRES_CONNECTION_STRING:-}", compose, StringComparison.Ordinal);
        Assert.Contains("docker-compose.production.external-postgres.yml", productionScript, StringComparison.Ordinal);
        Assert.Contains("validate_external_postgres_connection_string", productionScript, StringComparison.Ordinal);
        Assert.Contains("runtime_services", productionScript, StringComparison.Ordinal);
        Assert.Contains("SSL Mode=Require, VerifyCA, or VerifyFull", productionScript, StringComparison.Ordinal);

        Assert.Contains("profiles:", externalCompose, StringComparison.Ordinal);
        Assert.Contains("local-postgres", externalCompose, StringComparison.Ordinal);
        Assert.Contains("depends_on: !reset []", externalCompose, StringComparison.Ordinal);
        Assert.Contains("Set MEMORYSYSTEM_POSTGRES_CONNECTION_STRING for MEMORYSYSTEM_POSTGRES_PROFILE=external", externalCompose, StringComparison.Ordinal);

        Assert.Contains("is_external_postgres_profile", boundarySeed, StringComparison.Ordinal);
        Assert.Contains("psql \"$(read_env_value MEMORYSYSTEM_POSTGRES_CONNECTION_STRING \"\")\"", boundarySeed, StringComparison.Ordinal);
        Assert.Contains("MEMORYSYSTEM_POSTGRES_PROFILE=external", productionContainerDoc, StringComparison.Ordinal);
        Assert.Contains("External / Managed PostgreSQL Production Profile", productionSecretsDoc, StringComparison.Ordinal);
        Assert.Contains("bash -n scripts/external-postgres-profile-smoke.sh", testingDoc, StringComparison.Ordinal);

        Assert.Contains("MEMORYSYSTEM_POSTGRES_PROFILE=external", smoke, StringComparison.Ordinal);
        Assert.Contains("production-container.sh\" config", smoke, StringComparison.Ordinal);
        Assert.Contains("External PostgreSQL profile smoke passed", smoke, StringComparison.Ordinal);
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
