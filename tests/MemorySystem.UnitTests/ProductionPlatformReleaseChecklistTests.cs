namespace MemorySystem.UnitTests;

public sealed partial class ProductionPlatformTerraformSkeletonTests
{
    [Fact]
    public void Pi07_release_checklists_cover_required_gates_for_each_environment()
    {
        var root = FindRepositoryRoot();
        var checklistPath = Path.Combine(root, "docs", "production-release-checklists-pi07.md");
        var checklist = File.ReadAllText(checklistPath);

        Assert.Contains("# Production Release Checklists PI-07", checklist, StringComparison.Ordinal);
        Assert.Contains("release_evidence_bucket", checklist, StringComparison.Ordinal);
        Assert.Contains("memorysystem_alert_route_test", checklist, StringComparison.Ordinal);

        var requiredGates = new[]
        {
            "migration",
            "health",
            "metrics",
            "benchmark gate",
            "backup/restore",
            "rollback owner",
            "alert routing",
            "audit evidence"
        };

        foreach (var sectionName in new[]
                 {
                     "Local Release Checklist",
                     "CI Release Checklist",
                     "Pilot Release Checklist",
                     "Production Release Checklist"
                 })
        {
            var section = ExtractMarkdownSection(checklist, sectionName);

            foreach (var requiredGate in requiredGates)
            {
                Assert.Contains(requiredGate, section, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void Pi07_release_checklists_reference_executable_evidence_paths()
    {
        var root = FindRepositoryRoot();
        var checklist = File.ReadAllText(Path.Combine(root, "docs", "production-release-checklists-pi07.md"));

        foreach (var requiredCommand in new[]
                 {
                     "dotnet restore MemorySystem.sln",
                     "dotnet build MemorySystem.sln --no-restore",
                     "dotnet test tests/MemorySystem.UnitTests/MemorySystem.UnitTests.csproj --no-restore",
                     "dotnet test tests/MemorySystem.IntegrationTests/MemorySystem.IntegrationTests.csproj --no-build",
                     "terraform fmt -check -recursive infra/terraform",
                     "terraform -chdir=infra/terraform/environments/pilot validate",
                     "terraform -chdir=infra/terraform/environments/production validate",
                     "scripts/observability-artifacts-smoke.sh",
                     "scripts/operations-metrics-smoke.sh",
                     "scripts/backup-restore-smoke.sh",
                     "scripts/benchmark-release-gate.sh",
                     "scripts/production-pilot-deployment-smoke.sh"
                 })
        {
            Assert.Contains(requiredCommand, checklist, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Pi07_release_checklist_is_exposed_in_docs_and_runtime_contract()
    {
        var root = FindRepositoryRoot();
        var runtimeMain = File.ReadAllText(Path.Combine(root, "infra", "terraform", "modules", "memorysystem-runtime", "main.tf"));
        var docsIndex = File.ReadAllText(Path.Combine(root, "docs", "README.md"));
        var platformPlan = File.ReadAllText(Path.Combine(root, "docs", "production-platform-integration-lr05.md"));
        var terraformReadme = File.ReadAllText(Path.Combine(root, "infra", "terraform", "README.md"));
        var backlog = File.ReadAllText(Path.Combine(root, "docs", "backlog.md"));
        var productPlan = File.ReadAllText(Path.Combine(root, "docs", "product-improvement-plan.md"));

        Assert.Contains("release_checklist = {", runtimeMain, StringComparison.Ordinal);
        Assert.Contains("target_slice                  = \"PI-07\"", runtimeMain, StringComparison.Ordinal);
        Assert.Contains("docs/production-release-checklists-pi07.md", runtimeMain, StringComparison.Ordinal);
        Assert.Contains("environments                  = [\"local\", \"ci\", \"pilot\", \"production\"]", runtimeMain, StringComparison.Ordinal);
        Assert.Contains("release_evidence_bucket       = var.release_evidence_bucket", runtimeMain, StringComparison.Ordinal);
        Assert.Contains("rollback_owner_required       = true", runtimeMain, StringComparison.Ordinal);
        Assert.Contains("alert_route_test_metric       = \"memorysystem_alert_route_test\"", runtimeMain, StringComparison.Ordinal);
        Assert.Contains("release_checklist            = local.release_checklist", runtimeMain, StringComparison.Ordinal);

        foreach (var requiredGate in new[]
                 {
                     "\"migration\"",
                     "\"health\"",
                     "\"metrics\"",
                     "\"benchmark_gate\"",
                     "\"backup_restore\"",
                     "\"rollback_owner\"",
                     "\"alert_routing\"",
                     "\"audit_evidence\""
                 })
        {
            Assert.Contains(requiredGate, runtimeMain, StringComparison.Ordinal);
        }

        Assert.Contains("[Production Release Checklists PI-07](production-release-checklists-pi07.md)", docsIndex, StringComparison.Ordinal);
        Assert.Contains("[Production Release Checklists PI-07](production-release-checklists-pi07.md)", platformPlan, StringComparison.Ordinal);
        Assert.Contains("PI-07 adds `release_checklist`", terraformReadme, StringComparison.Ordinal);
        Assert.Contains("| PI-07 | P1 | Done | Add environment-specific release checklists.", backlog, StringComparison.Ordinal);
        Assert.Contains("| PI-08 | P1 | Done | Run first platform rehearsal.", backlog, StringComparison.Ordinal);
        Assert.Contains("| EA-05 | P0 | Done | Add service-account lifecycle.", backlog, StringComparison.Ordinal);
        Assert.Contains("The next move should be `GC-06`", productPlan, StringComparison.Ordinal);
        Assert.Contains("| DM-06 | P2 | Done | Remove duplicate string normalization helpers.", backlog, StringComparison.Ordinal);
    }

    private static string ExtractMarkdownSection(string markdown, string sectionName)
    {
        var marker = $"## {sectionName}";
        var start = markdown.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Missing section '{sectionName}'.");

        var nextSection = markdown.IndexOf("\n## ", start + marker.Length, StringComparison.Ordinal);
        return nextSection < 0
            ? markdown[start..]
            : markdown[start..nextSection];
    }
}
