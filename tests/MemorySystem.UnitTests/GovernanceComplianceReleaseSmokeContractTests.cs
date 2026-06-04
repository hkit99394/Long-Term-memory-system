namespace MemorySystem.UnitTests;

public sealed class GovernanceComplianceReleaseSmokeContractTests
{
    [Fact]
    public void Gc08_governance_compliance_release_smoke_is_documented_scripted_and_next_move_is_pilot_readiness()
    {
        var root = FindRepositoryRoot();
        var contract = File.ReadAllText(Path.Combine(root, "docs", "governance-compliance-release-smoke-gc08.md"));
        var script = File.ReadAllText(Path.Combine(root, "scripts", "governance-compliance-release-smoke.sh"));
        var integrationTest = File.ReadAllText(Path.Combine(
            root,
            "tests",
            "MemorySystem.IntegrationTests",
            "GovernanceComplianceReleaseSmokeTests.cs"));
        var backlog = File.ReadAllText(Path.Combine(root, "docs", "backlog.md"));
        var index = File.ReadAllText(Path.Combine(root, "docs", "README.md"));
        var folderStructure = File.ReadAllText(Path.Combine(root, "docs", "folder-structure.md"));
        var testing = File.ReadAllText(Path.Combine(root, "docs", "testing.md"));
        var productPlan = File.ReadAllText(Path.Combine(root, "docs", "product-improvement-plan.md"));
        var governancePlan = File.ReadAllText(Path.Combine(root, "docs", "governance-compliance-gate-lr06.md"));
        var releaseChecklist = File.ReadAllText(Path.Combine(root, "docs", "production-release-checklists-pi07.md"));

        Assert.Contains("# GC-08 Governance/Compliance Release Smoke", contract, StringComparison.Ordinal);
        Assert.Contains("./scripts/governance-compliance-release-smoke.sh", contract, StringComparison.Ordinal);
        Assert.Contains("policy config", contract, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("permission drift", contract, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("erasure replay", contract, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("retention dry run", contract, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("audit export", contract, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("strict mode", contract, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pilot readiness evidence review", contract, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("GovernanceComplianceReleaseSmokeTests", script, StringComparison.Ordinal);
        Assert.Contains("MEMORYSYSTEM_REQUIRE_DATABASE_TESTS=true", script, StringComparison.Ordinal);
        Assert.Contains("platform-retention-minimization.sh", integrationTest, StringComparison.Ordinal);
        Assert.Contains("platform-erasure-replay-ledger-export.sh", integrationTest, StringComparison.Ordinal);
        Assert.Contains("platform-compliance-evidence-package.sh", integrationTest, StringComparison.Ordinal);
        Assert.Contains("MEMORYSYSTEM_COMPLIANCE_EVIDENCE_PACKAGE_MODE", integrationTest, StringComparison.Ordinal);
        Assert.Contains("\"strict\"", integrationTest, StringComparison.Ordinal);
        Assert.Contains("missingRequiredArtifactCount", integrationTest, StringComparison.Ordinal);
        Assert.Contains("RawPayloadCanary", integrationTest, StringComparison.Ordinal);

        Assert.Contains("| GC-08 | P1 | Done | Add governance/compliance release smoke.", backlog, StringComparison.Ordinal);
        Assert.Contains("[Governance/Compliance Release Smoke GC-08](governance-compliance-release-smoke-gc08.md)", index, StringComparison.Ordinal);
        Assert.Contains("governance-compliance-release-smoke-gc08.md", folderStructure, StringComparison.Ordinal);
        Assert.Contains("bash -n scripts/governance-compliance-release-smoke.sh", testing, StringComparison.Ordinal);
        Assert.Contains("./scripts/governance-compliance-release-smoke.sh", testing, StringComparison.Ordinal);
        Assert.Contains("scripts/governance-compliance-release-smoke.sh", releaseChecklist, StringComparison.Ordinal);
        Assert.Contains("next move should be a target-environment pilot rehearsal", productPlan, StringComparison.Ordinal);
        Assert.Contains("product planning points to a target-environment pilot rehearsal", governancePlan, StringComparison.Ordinal);
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
