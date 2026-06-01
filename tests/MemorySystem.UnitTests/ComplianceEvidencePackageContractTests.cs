namespace MemorySystem.UnitTests;

public sealed class ComplianceEvidencePackageContractTests
{
    [Fact]
    public void Gc06_compliance_evidence_package_is_documented_scripted_observable_and_gc07_is_closed()
    {
        var root = FindRepositoryRoot();
        var contract = File.ReadAllText(Path.Combine(root, "docs", "compliance-evidence-package-gc06.md"));
        var backlog = File.ReadAllText(Path.Combine(root, "docs", "backlog.md"));
        var index = File.ReadAllText(Path.Combine(root, "docs", "README.md"));
        var folderStructure = File.ReadAllText(Path.Combine(root, "docs", "folder-structure.md"));
        var productPlan = File.ReadAllText(Path.Combine(root, "docs", "product-improvement-plan.md"));
        var governancePlan = File.ReadAllText(Path.Combine(root, "docs", "governance-compliance-gate-lr06.md"));
        var script = File.ReadAllText(Path.Combine(root, "scripts", "platform-compliance-evidence-package.sh"));
        var runtimeMain = File.ReadAllText(Path.Combine(root, "infra", "terraform", "modules", "memorysystem-runtime", "main.tf"));
        var dockerfile = File.ReadAllText(Path.Combine(root, "Dockerfile"));
        var externalMetrics = File.ReadAllText(Path.Combine(root, "observability", "alert-inputs", "external-pilot-metrics.txt"));
        var alertRules = File.ReadAllText(Path.Combine(root, "observability", "prometheus", "memorysystem-pilot-alerts.yml"));
        var dashboard = File.ReadAllText(Path.Combine(root, "observability", "grafana", "memorysystem-pilot-dashboard.json"));

        Assert.Contains("# GC-06 Compliance Evidence Package", contract, StringComparison.Ordinal);
        Assert.Contains("MEMORYSYSTEM_COMPLIANCE_EVIDENCE_PACKAGE_MODE=draft", contract, StringComparison.Ordinal);
        Assert.Contains("MEMORYSYSTEM_COMPLIANCE_EVIDENCE_PACKAGE_MODE=strict", contract, StringComparison.Ordinal);
        Assert.Contains("NDJSON artifact index", contract, StringComparison.Ordinal);
        Assert.Contains("SHA-256 sidecar", contract, StringComparison.Ordinal);
        Assert.Contains("memorysystem_compliance_evidence_package_success", contract, StringComparison.Ordinal);

        Assert.Contains("| GC-06 | P1 | Done | Generate compliance evidence package.", backlog, StringComparison.Ordinal);
        Assert.Contains("| GC-07 | P1 | Done | Add governance/compliance admin console view.", backlog, StringComparison.Ordinal);
        Assert.Contains("[Compliance Evidence Package GC-06](compliance-evidence-package-gc06.md)", index, StringComparison.Ordinal);
        Assert.Contains("compliance-evidence-package-gc06.md", folderStructure, StringComparison.Ordinal);
        Assert.Contains("The next move should be `GC-08`", productPlan, StringComparison.Ordinal);
        Assert.Contains("product planning points to `GC-08`", governancePlan, StringComparison.Ordinal);

        Assert.Contains("\"kind\": \"memorysystem.compliance_evidence_package\"", script, StringComparison.Ordinal);
        Assert.Contains("MEMORYSYSTEM_COMPLIANCE_EVIDENCE_PACKAGE_MODE", script, StringComparison.Ordinal);
        Assert.Contains("audit_export", script, StringComparison.Ordinal);
        Assert.Contains("retention_report", script, StringComparison.Ordinal);
        Assert.Contains("legal_hold_summary", script, StringComparison.Ordinal);
        Assert.Contains("permission_drift_report", script, StringComparison.Ordinal);
        Assert.Contains("retention_minimization", script, StringComparison.Ordinal);
        Assert.Contains("external_payload_retention", script, StringComparison.Ordinal);
        Assert.Contains("erasure_replay", script, StringComparison.Ordinal);
        Assert.Contains("backup_export", script, StringComparison.Ordinal);
        Assert.Contains("restore_validation", script, StringComparison.Ordinal);
        Assert.Contains("release_checklist", script, StringComparison.Ordinal);
        Assert.Contains("benchmark_release_gate", script, StringComparison.Ordinal);
        Assert.Contains("alert_route_smoke", script, StringComparison.Ordinal);
        Assert.Contains("sha256_file \"$MANIFEST_FILE\"", script, StringComparison.Ordinal);
        Assert.Contains("memorysystem_compliance_evidence_package_success", script, StringComparison.Ordinal);
        Assert.DoesNotContain("cat \"$artifact_path\"", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SELECT event.content", script, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("compliance_evidence_package = {", runtimeMain, StringComparison.Ordinal);
        Assert.Contains("target_slice         = \"GC-06\"", runtimeMain, StringComparison.Ordinal);
        Assert.Contains("[\"/app/scripts/platform-compliance-evidence-package.sh\"]", runtimeMain, StringComparison.Ordinal);
        Assert.Contains("MEMORYSYSTEM_COMPLIANCE_EVIDENCE_PACKAGE_MODE", runtimeMain, StringComparison.Ordinal);
        Assert.Contains("MEMORYSYSTEM_COMPLIANCE_PACKAGE_FILE", runtimeMain, StringComparison.Ordinal);
        Assert.Contains("governance_compliance_metrics", runtimeMain, StringComparison.Ordinal);
        Assert.Contains("memorysystem_compliance_evidence_package_success", runtimeMain, StringComparison.Ordinal);

        Assert.Contains("platform-compliance-evidence-package.sh", dockerfile, StringComparison.Ordinal);
        Assert.Contains("memorysystem_compliance_evidence_package_success", externalMetrics, StringComparison.Ordinal);
        Assert.Contains("memorysystem_compliance_evidence_package_missing_required_artifacts", externalMetrics, StringComparison.Ordinal);
        Assert.Contains("absent(memorysystem_compliance_evidence_package_success)", alertRules, StringComparison.Ordinal);
        Assert.Contains("MemorySystemComplianceEvidencePackageFailed", alertRules, StringComparison.Ordinal);
        Assert.Contains("memorysystem_compliance_evidence_package_success", dashboard, StringComparison.Ordinal);
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
