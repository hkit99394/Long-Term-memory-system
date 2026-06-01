namespace MemorySystem.UnitTests;

public sealed class RetentionMinimizationContractTests
{
    [Fact]
    public void Gc04_retention_minimization_is_documented_scripted_observable_and_next_slice_is_gc06()
    {
        var root = FindRepositoryRoot();
        var contract = File.ReadAllText(Path.Combine(root, "docs", "standard-audit-retention-minimization-gc04.md"));
        var retentionPolicy = File.ReadAllText(Path.Combine(root, "docs", "retention-policy.md"));
        var backlog = File.ReadAllText(Path.Combine(root, "docs", "backlog.md"));
        var index = File.ReadAllText(Path.Combine(root, "docs", "README.md"));
        var folderStructure = File.ReadAllText(Path.Combine(root, "docs", "folder-structure.md"));
        var productPlan = File.ReadAllText(Path.Combine(root, "docs", "product-improvement-plan.md"));
        var governancePlan = File.ReadAllText(Path.Combine(root, "docs", "governance-compliance-gate-lr06.md"));
        var script = File.ReadAllText(Path.Combine(root, "scripts", "platform-retention-minimization.sh"));
        var runtimeMain = File.ReadAllText(Path.Combine(root, "infra", "terraform", "modules", "memorysystem-runtime", "main.tf"));
        var externalMetrics = File.ReadAllText(Path.Combine(root, "observability", "alert-inputs", "external-pilot-metrics.txt"));

        Assert.Contains("# GC-04 Standard And Audit Retention Minimization", contract, StringComparison.Ordinal);
        Assert.Contains("MEMORYSYSTEM_RETENTION_MINIMIZATION_MODE=dry-run", contract, StringComparison.Ordinal);
        Assert.Contains("MEMORYSYSTEM_RETENTION_MINIMIZATION_MODE=execute", contract, StringComparison.Ordinal);
        Assert.Contains("legalHoldSkippedEvents", contract, StringComparison.Ordinal);
        Assert.Contains("externalPayloadSkippedEvents", contract, StringComparison.Ordinal);
        Assert.Contains("preservedDerivedCopies", contract, StringComparison.Ordinal);
        Assert.Contains("memorysystem_retention_minimization_success", contract, StringComparison.Ordinal);

        Assert.Contains("scripts/platform-retention-minimization.sh", retentionPolicy, StringComparison.Ordinal);
        Assert.Contains("| GC-04 | P0 | Done | Implement standard and audit retention minimization.", backlog, StringComparison.Ordinal);
        Assert.Contains("| GC-05 | P1 | Done | Check external payload-store retention.", backlog, StringComparison.Ordinal);
        Assert.Contains("| GC-06 | P1 | Done | Generate compliance evidence package.", backlog, StringComparison.Ordinal);
        Assert.Contains("| GC-07 | P1 | Todo | Add governance/compliance admin console view.", backlog, StringComparison.Ordinal);
        Assert.Contains("[Standard And Audit Retention Minimization GC-04](standard-audit-retention-minimization-gc04.md)", index, StringComparison.Ordinal);
        Assert.Contains("standard-audit-retention-minimization-gc04.md", folderStructure, StringComparison.Ordinal);
        Assert.Contains("The next move should be `GC-07`", productPlan, StringComparison.Ordinal);
        Assert.Contains("product planning points to `GC-07`", governancePlan, StringComparison.Ordinal);

        Assert.Contains("\"kind\": \"memorysystem.retention_minimization\"", script, StringComparison.Ordinal);
        Assert.Contains("MEMORYSYSTEM_RETENTION_MINIMIZATION_MODE", script, StringComparison.Ordinal);
        Assert.Contains("dry-run", script, StringComparison.Ordinal);
        Assert.Contains("execute", script, StringComparison.Ordinal);
        Assert.Contains("events AS event", script, StringComparison.Ordinal);
        Assert.Contains("event.retention_class IN ('standard', 'audit')", script, StringComparison.Ordinal);
        Assert.Contains("governance_legal_hold_events", script, StringComparison.Ordinal);
        Assert.Contains("external_payload_uri IS NOT NULL", script, StringComparison.Ordinal);
        Assert.Contains("UPDATE memory_reviews", script, StringComparison.Ordinal);
        Assert.Contains("UPDATE events AS event", script, StringComparison.Ordinal);
        Assert.Contains("memory_facts", script, StringComparison.Ordinal);
        Assert.Contains("memory_chunks", script, StringComparison.Ordinal);
        Assert.Contains("memory_embeddings", script, StringComparison.Ordinal);
        Assert.Contains("vault_exports", script, StringComparison.Ordinal);
        Assert.Contains("memorysystem_retention_minimization_success", script, StringComparison.Ordinal);
        Assert.DoesNotContain("SELECT event.content", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("redaction_status = 'redacted'", script, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("retention_minimization = {", runtimeMain, StringComparison.Ordinal);
        Assert.Contains("target_slice         = \"GC-04\"", runtimeMain, StringComparison.Ordinal);
        Assert.Contains("[\"/app/scripts/platform-retention-minimization.sh\"]", runtimeMain, StringComparison.Ordinal);
        Assert.Contains("MEMORYSYSTEM_RETENTION_MINIMIZATION_MODE", runtimeMain, StringComparison.Ordinal);
        Assert.Contains("memorysystem_retention_minimization_success", runtimeMain, StringComparison.Ordinal);

        Assert.Contains("memorysystem_retention_minimization_success", externalMetrics, StringComparison.Ordinal);
        Assert.Contains("memorysystem_retention_minimization_failures", externalMetrics, StringComparison.Ordinal);
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
