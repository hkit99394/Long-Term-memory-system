namespace MemorySystem.UnitTests;

public sealed class ExternalPayloadRetentionCheckContractTests
{
    [Fact]
    public void Gc05_external_payload_retention_check_is_documented_scripted_observable_and_next_slice_is_gc06()
    {
        var root = FindRepositoryRoot();
        var contract = File.ReadAllText(Path.Combine(root, "docs", "external-payload-retention-check-gc05.md"));
        var retentionPolicy = File.ReadAllText(Path.Combine(root, "docs", "retention-policy.md"));
        var backlog = File.ReadAllText(Path.Combine(root, "docs", "backlog.md"));
        var index = File.ReadAllText(Path.Combine(root, "docs", "README.md"));
        var folderStructure = File.ReadAllText(Path.Combine(root, "docs", "folder-structure.md"));
        var productPlan = File.ReadAllText(Path.Combine(root, "docs", "product-improvement-plan.md"));
        var governancePlan = File.ReadAllText(Path.Combine(root, "docs", "governance-compliance-gate-lr06.md"));
        var script = File.ReadAllText(Path.Combine(root, "scripts", "platform-external-payload-retention-check.sh"));
        var runtimeMain = File.ReadAllText(Path.Combine(root, "infra", "terraform", "modules", "memorysystem-runtime", "main.tf"));
        var dockerfile = File.ReadAllText(Path.Combine(root, "Dockerfile"));
        var externalMetrics = File.ReadAllText(Path.Combine(root, "observability", "alert-inputs", "external-pilot-metrics.txt"));
        var alertRules = File.ReadAllText(Path.Combine(root, "observability", "prometheus", "memorysystem-pilot-alerts.yml"));
        var dashboard = File.ReadAllText(Path.Combine(root, "observability", "grafana", "memorysystem-pilot-dashboard.json"));

        Assert.Contains("# GC-05 External Payload Retention Check", contract, StringComparison.Ordinal);
        Assert.Contains("MEMORYSYSTEM_EXTERNAL_PAYLOAD_CHECK_MODE=audit-only", contract, StringComparison.Ordinal);
        Assert.Contains("MEMORYSYSTEM_EXTERNAL_PAYLOAD_CHECK_MODE=verify", contract, StringComparison.Ordinal);
        Assert.Contains("provider-specific probes", contract, StringComparison.Ordinal);
        Assert.Contains("deletion evidence", contract, StringComparison.Ordinal);
        Assert.Contains("memorysystem_external_payload_retention_check_success", contract, StringComparison.Ordinal);

        Assert.Contains("scripts/platform-external-payload-retention-check.sh", retentionPolicy, StringComparison.Ordinal);
        Assert.Contains("| GC-05 | P1 | Done | Check external payload-store retention.", backlog, StringComparison.Ordinal);
        Assert.Contains("| GC-06 | P1 | Done | Generate compliance evidence package.", backlog, StringComparison.Ordinal);
        Assert.Contains("| GC-07 | P1 | Done | Add governance/compliance admin console view.", backlog, StringComparison.Ordinal);
        Assert.Contains("[External Payload Retention Check GC-05](external-payload-retention-check-gc05.md)", index, StringComparison.Ordinal);
        Assert.Contains("external-payload-retention-check-gc05.md", folderStructure, StringComparison.Ordinal);
        Assert.Contains("The next move should be `GC-08`", productPlan, StringComparison.Ordinal);
        Assert.Contains("product planning points to `GC-08`", governancePlan, StringComparison.Ordinal);

        Assert.Contains("\"kind\": \"memorysystem.external_payload_retention_check\"", script, StringComparison.Ordinal);
        Assert.Contains("MEMORYSYSTEM_EXTERNAL_PAYLOAD_CHECK_MODE", script, StringComparison.Ordinal);
        Assert.Contains("MEMORYSYSTEM_EXTERNAL_PAYLOAD_POLICY_MODE", script, StringComparison.Ordinal);
        Assert.Contains("audit-only", script, StringComparison.Ordinal);
        Assert.Contains("verify", script, StringComparison.Ordinal);
        Assert.Contains("events AS event", script, StringComparison.Ordinal);
        Assert.Contains("event.external_payload_uri IS NOT NULL", script, StringComparison.Ordinal);
        Assert.Contains("governance_legal_hold_events", script, StringComparison.Ordinal);
        Assert.Contains("probe_file_uri", script, StringComparison.Ordinal);
        Assert.Contains("probe_s3_uri", script, StringComparison.Ordinal);
        Assert.Contains("aws s3api head-object", script, StringComparison.Ordinal);
        Assert.Contains("disabled policy mode rejects every external payload pointer", script, StringComparison.Ordinal);
        Assert.Contains("events.external_payload_uri", script, StringComparison.Ordinal);
        Assert.Contains("provider_credentials", script, StringComparison.Ordinal);
        Assert.Contains("payload_bytes", script, StringComparison.Ordinal);
        Assert.DoesNotContain("SELECT event.content", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cat \"$external_payload_uri\"", script, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("external_payload_retention_check = {", runtimeMain, StringComparison.Ordinal);
        Assert.Contains("target_slice         = \"GC-05\"", runtimeMain, StringComparison.Ordinal);
        Assert.Contains("[\"/app/scripts/platform-external-payload-retention-check.sh\"]", runtimeMain, StringComparison.Ordinal);
        Assert.Contains("MEMORYSYSTEM_EXTERNAL_PAYLOAD_CHECK_MODE", runtimeMain, StringComparison.Ordinal);
        Assert.Contains("MEMORYSYSTEM_EXTERNAL_PAYLOAD_POLICY_MODE", runtimeMain, StringComparison.Ordinal);
        Assert.Contains("memorysystem_external_payload_retention_check_success", runtimeMain, StringComparison.Ordinal);

        Assert.Contains("platform-external-payload-retention-check.sh", dockerfile, StringComparison.Ordinal);
        Assert.Contains("memorysystem_external_payload_retention_check_success", externalMetrics, StringComparison.Ordinal);
        Assert.Contains("memorysystem_external_payload_retention_failures", externalMetrics, StringComparison.Ordinal);
        Assert.Contains("absent(memorysystem_external_payload_retention_check_success)", alertRules, StringComparison.Ordinal);
        Assert.Contains("MemorySystemExternalPayloadRetentionCheckFailed", alertRules, StringComparison.Ordinal);
        Assert.Contains("memorysystem_external_payload_retention_check_success", dashboard, StringComparison.Ordinal);
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
