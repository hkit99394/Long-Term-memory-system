namespace MemorySystem.UnitTests;

public sealed class TargetEnvironmentEvidenceHardeningTests
{
    [Fact]
    public void Ip04_target_environment_evidence_hardening_is_documented_scripted_and_marked_doing()
    {
        var root = FindRepositoryRoot();
        var productPlan = File.ReadAllText(Path.Combine(root, "docs", "product-improvement-plan.md"));
        var hardening = File.ReadAllText(Path.Combine(root, "docs", "target-environment-evidence-hardening-ip04.md"));
        var runbook = File.ReadAllText(Path.Combine(root, "docs", "target-environment-pilot-rehearsal-p0.md"));
        var releaseChecklist = File.ReadAllText(Path.Combine(root, "docs", "production-release-checklists-pi07.md"));
        var testing = File.ReadAllText(Path.Combine(root, "docs", "testing.md"));
        var docsIndex = File.ReadAllText(Path.Combine(root, "docs", "README.md"));
        var folderStructure = File.ReadAllText(Path.Combine(root, "docs", "folder-structure.md"));
        var script = File.ReadAllText(Path.Combine(root, "scripts", "target-environment-evidence-verify.sh"));
        var schema = File.ReadAllText(Path.Combine(root, "docs", "target-environment-evidence-manifest.schema.json"));
        var example = File.ReadAllText(Path.Combine(root, "docs", "target-environment-evidence-manifest.example.json"));

        Assert.Contains("| IP-04 | P0 | Doing | Release Manager + Tester/QA + Ops | Target-Environment Evidence Hardening |", productPlan, StringComparison.Ordinal);
        Assert.Contains("Status: started; target evidence manifest contract is implemented", hardening, StringComparison.Ordinal);
        Assert.Contains("memorysystem.target_environment_evidence", hardening, StringComparison.Ordinal);
        Assert.Contains("scripts/target-environment-evidence-verify.sh", hardening, StringComparison.Ordinal);
        Assert.Contains("real target evidence is not attached", hardening, StringComparison.Ordinal);

        foreach (var gate in RequiredGateIds())
        {
            Assert.Contains(gate, hardening, StringComparison.Ordinal);
            Assert.Contains(gate, script, StringComparison.Ordinal);
            Assert.Contains(gate, schema, StringComparison.Ordinal);
            Assert.Contains(gate, example, StringComparison.Ordinal);
        }

        Assert.Contains("Target-Environment Evidence Hardening IP-04", docsIndex, StringComparison.Ordinal);
        Assert.Contains("target-environment-evidence-hardening-ip04.md", folderStructure, StringComparison.Ordinal);
        Assert.Contains("target-environment-evidence-manifest.schema.json", folderStructure, StringComparison.Ordinal);
        Assert.Contains("target-environment-evidence-manifest.example.json", folderStructure, StringComparison.Ordinal);

        Assert.Contains("target-environment-evidence-verify.sh", runbook, StringComparison.Ordinal);
        Assert.Contains("target-environment-evidence-verify.sh", releaseChecklist, StringComparison.Ordinal);
        Assert.Contains("bash -n scripts/target-environment-evidence-verify.sh", testing, StringComparison.Ordinal);

        Assert.Contains("memorysystem.target_environment_evidence", schema, StringComparison.Ordinal);
        Assert.Contains("\"payloadSafe\":", schema, StringComparison.Ordinal);
        Assert.Contains("sha256:<64 hex chars>", hardening, StringComparison.Ordinal);
        Assert.Contains("sha256_file \"$artifact_file\"", script, StringComparison.Ordinal);
        Assert.Contains("payloadSafe true", script, StringComparison.Ordinal);
        Assert.Contains("must be a local file path for hash verification", script, StringComparison.Ordinal);
        Assert.DoesNotContain("cat \"$artifact_file\"", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SELECT event.content", script, StringComparison.OrdinalIgnoreCase);
    }

    private static string[] RequiredGateIds()
    {
        return new[]
        {
            "environment_preflight",
            "terraform_validation",
            "deployment_smoke",
            "metrics_and_tracing",
            "benchmark_scorecards",
            "governance_smoke",
            "backup_restore",
            "alert_receiver_acknowledgement",
            "evidence_upload",
            "rollback_notes",
            "go_no_go"
        };
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
