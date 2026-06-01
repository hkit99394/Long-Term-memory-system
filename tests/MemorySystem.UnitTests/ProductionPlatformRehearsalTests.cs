namespace MemorySystem.UnitTests;

public sealed partial class ProductionPlatformTerraformSkeletonTests
{
    [Fact]
    public void Pi08_rehearsal_report_records_required_release_evidence()
    {
        var root = FindRepositoryRoot();
        var report = File.ReadAllText(Path.Combine(root, "docs", "production-platform-rehearsal-pi08.md"));

        Assert.Contains("# Production Platform Rehearsal PI-08", report, StringComparison.Ordinal);
        Assert.Contains("Status: Passed local isolated rehearsal", report, StringComparison.Ordinal);
        Assert.Contains("MEMORYSYSTEM_PRODUCTION_PILOT_SMOKE_KEEP_WORK_DIR=true", report, StringComparison.Ordinal);
        Assert.Contains("./scripts/production-pilot-deployment-smoke.sh", report, StringComparison.Ordinal);
        Assert.Contains("./scripts/benchmark-release-gate.sh", report, StringComparison.Ordinal);
        Assert.Contains("./scripts/observability-artifacts-smoke.sh", report, StringComparison.Ordinal);

        foreach (var evidence in new[]
                 {
                     "Migrator role applied 25 migrations",
                     "API `/health/live` and `/health/ready` returned success",
                     "worker processed them to zero unfinished jobs and created 6 memory embeddings",
                     "memory search for `SQL-first`",
                     "`scripts/operations-metrics-smoke.sh` passed before and after restore",
                     "43 API metric inputs, 14 external metric inputs, 36 alert rules, 3 alert routes",
                     "Release gate passed with Memory Lift `+1.575`, Contract Lift `+2.025`",
                     "restored into a fresh rollback database",
                     "`pgvector` extension validation passed",
                     "Done: `EA-08` adds migration and rollback smoke",
                     "Done: `EA-09` documents the pilot operator runbook",
                     "Done: `EA-10` evaluates directory sync",
                     "Done: `DM-06` removes duplicate string normalization helpers"
                 })
        {
            Assert.Contains(evidence, report, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Pi08_rehearsal_is_linked_from_docs_and_runtime_contract()
    {
        var root = FindRepositoryRoot();
        var runtimeMain = File.ReadAllText(Path.Combine(root, "infra", "terraform", "modules", "memorysystem-runtime", "main.tf"));
        var docsIndex = File.ReadAllText(Path.Combine(root, "docs", "README.md"));
        var platformPlan = File.ReadAllText(Path.Combine(root, "docs", "production-platform-integration-lr05.md"));
        var terraformReadme = File.ReadAllText(Path.Combine(root, "infra", "terraform", "README.md"));
        var backlog = File.ReadAllText(Path.Combine(root, "docs", "backlog.md"));
        var productPlan = File.ReadAllText(Path.Combine(root, "docs", "product-improvement-plan.md"));

        Assert.Contains("platform_rehearsal = {", runtimeMain, StringComparison.Ordinal);
        Assert.Contains("target_slice             = \"PI-08\"", runtimeMain, StringComparison.Ordinal);
        Assert.Contains("report                   = \"docs/production-platform-rehearsal-pi08.md\"", runtimeMain, StringComparison.Ordinal);
        Assert.Contains("command                  = \"scripts/production-pilot-deployment-smoke.sh\"", runtimeMain, StringComparison.Ordinal);
        Assert.Contains("required_roles           = [\"migrator\", \"api\", \"worker\"]", runtimeMain, StringComparison.Ordinal);
        Assert.Contains("benchmark_gate           = \"scripts/benchmark-release-gate.sh\"", runtimeMain, StringComparison.Ordinal);
        Assert.Contains("alert_routing_smoke      = \"scripts/observability-artifacts-smoke.sh\"", runtimeMain, StringComparison.Ordinal);
        Assert.Contains("platform_rehearsal            = local.platform_rehearsal", runtimeMain, StringComparison.Ordinal);

        Assert.Contains("[Production Platform Rehearsal PI-08](production-platform-rehearsal-pi08.md)", docsIndex, StringComparison.Ordinal);
        Assert.Contains("[Production Platform Rehearsal PI-08](production-platform-rehearsal-pi08.md)", platformPlan, StringComparison.Ordinal);
        Assert.Contains("PI-08 records the first isolated platform rehearsal", terraformReadme, StringComparison.Ordinal);
        Assert.Contains("| PI-08 | P1 | Done | Run first platform rehearsal.", backlog, StringComparison.Ordinal);
        Assert.Contains("The next move should be `GC-08`", productPlan, StringComparison.Ordinal);
    }
}
