namespace MemorySystem.UnitTests;

public sealed class TargetEnvironmentPilotRehearsalTests
{
    [Fact]
    public void Target_environment_pilot_rehearsal_tracks_p0_external_invite_gate()
    {
        var root = FindRepositoryRoot();
        var rehearsal = File.ReadAllText(Path.Combine(root, "docs", "target-environment-pilot-rehearsal-p0.md"));
        var index = File.ReadAllText(Path.Combine(root, "docs", "README.md"));
        var backlog = File.ReadAllText(Path.Combine(root, "docs", "backlog.md"));
        var roadmap = File.ReadAllText(Path.Combine(root, "docs", "roadmap.md"));
        var readinessReview = File.ReadAllText(Path.Combine(root, "docs", "pilot-readiness-evidence-review-2026-06-01.md"));
        var releaseChecklist = File.ReadAllText(Path.Combine(root, "docs", "production-release-checklists-pi07.md"));
        var evidence = File.ReadAllText(Path.Combine(root, "docs", "pilot-release-evidence-epr03-2026-06-04.md"));
        var goNoGo = File.ReadAllText(Path.Combine(root, "docs", "external-pilot-go-no-go-epr04-2026-06-04.md"));
        var go = File.ReadAllText(Path.Combine(root, "docs", "external-pilot-go-epr04-v1.0.0-2026-06-04.md"));

        Assert.Contains("# Target-Environment Pilot Rehearsal P0", rehearsal, StringComparison.Ordinal);
        Assert.Contains("Status: local pilot-equivalent evidence accepted for version 1.0.0 GO", rehearsal, StringComparison.Ordinal);
        Assert.Contains("release_evidence_bucket", rehearsal, StringComparison.Ordinal);
        Assert.Contains("scripts/production-pilot-deployment-smoke.sh", rehearsal, StringComparison.Ordinal);
        Assert.Contains("scripts/benchmark-release-gate.sh", rehearsal, StringComparison.Ordinal);
        Assert.Contains("scripts/governance-compliance-release-smoke.sh", rehearsal, StringComparison.Ordinal);
        Assert.Contains("Alert receiver acknowledgement", rehearsal, StringComparison.Ordinal);
        Assert.Contains("Pilot Release Evidence EPR-03", rehearsal, StringComparison.Ordinal);
        Assert.Contains("External Pilot Go/No-Go EPR-04", rehearsal, StringComparison.Ordinal);
        Assert.Contains("Release owner signature", rehearsal, StringComparison.Ordinal);
        Assert.Contains("Rollback owner signature", rehearsal, StringComparison.Ordinal);

        Assert.Contains("[Target-Environment Pilot Rehearsal P0](target-environment-pilot-rehearsal-p0.md)", index, StringComparison.Ordinal);
        Assert.Contains("[Pilot Release Evidence EPR-03](pilot-release-evidence-epr03-2026-06-04.md)", index, StringComparison.Ordinal);
        Assert.Contains("[External Pilot Go/No-Go EPR-04](external-pilot-go-no-go-epr04-2026-06-04.md)", index, StringComparison.Ordinal);
        Assert.Contains("[External Pilot GO EPR-04 v1.0.0](external-pilot-go-epr04-v1.0.0-2026-06-04.md)", index, StringComparison.Ordinal);
        Assert.Contains("## External Pilot Readiness P0", backlog, StringComparison.Ordinal);
        Assert.Contains("| EPR-01 | P0 | Done | Define the target-environment pilot rehearsal runbook.", backlog, StringComparison.Ordinal);
        Assert.Contains("| EPR-02 | P0 | Done | Run target-environment deployment smoke.", backlog, StringComparison.Ordinal);
        Assert.Contains("| EPR-03 | P0 | Done | Attach fresh pilot release evidence.", backlog, StringComparison.Ordinal);
        Assert.Contains("| EPR-04 | P0 | Done | Sign the external-pilot go/no-go record.", backlog, StringComparison.Ordinal);
        Assert.Contains("Target-Environment Pilot Rehearsal P0", roadmap, StringComparison.Ordinal);
        Assert.Contains("External Pilot Go/No-Go EPR-04", roadmap, StringComparison.Ordinal);
        Assert.Contains("Target-Environment Pilot Rehearsal P0", readinessReview, StringComparison.Ordinal);
        Assert.Contains("Pilot Release Evidence EPR-03", readinessReview, StringComparison.Ordinal);
        Assert.Contains("External Pilot Go/No-Go EPR-04", readinessReview, StringComparison.Ordinal);
        Assert.Contains("Target-Environment Pilot Rehearsal P0", releaseChecklist, StringComparison.Ordinal);

        Assert.Contains("# Pilot Release Evidence EPR-03", evidence, StringComparison.Ordinal);
        Assert.Contains("EPR-02 and EPR-03 local pilot-equivalent evidence attached", evidence, StringComparison.Ordinal);
        Assert.Contains("scripts/production-pilot-deployment-smoke.sh", evidence, StringComparison.Ordinal);
        Assert.Contains("benchmarks/outputs/epr03-pilot-release/latest.json", evidence, StringComparison.Ordinal);
        Assert.Contains("agent-contract-live-smoke.run.json", evidence, StringComparison.Ordinal);
        Assert.Contains("Fresh live agent-contract smoke | Passed, `8` tasks passed and `0` failed", evidence, StringComparison.Ordinal);
        Assert.Contains("real receiver acknowledgement remains post-GO evidence hardening", evidence, StringComparison.Ordinal);
        Assert.Contains("EPR-04 is no longer the current external-pilot blocker", evidence, StringComparison.Ordinal);

        Assert.Contains("# External Pilot Go/No-Go EPR-04", goNoGo, StringComparison.Ordinal);
        Assert.Contains("Status: historical NO-GO record", goNoGo, StringComparison.Ordinal);
        Assert.Contains("Decision: NO-GO", goNoGo, StringComparison.Ordinal);
        Assert.Contains("superseded for the current decision", goNoGo, StringComparison.Ordinal);
        Assert.Contains("Required To Flip To GO", goNoGo, StringComparison.Ordinal);
        Assert.Contains("Recommended Next Work", goNoGo, StringComparison.Ordinal);

        Assert.Contains("# External Pilot GO EPR-04 v1.0.0", go, StringComparison.Ordinal);
        Assert.Contains("Version: 1.0.0", go, StringComparison.Ordinal);
        Assert.Contains("Decision: GO", go, StringComparison.Ordinal);
        Assert.Contains("Release owner signature: Owner approval recorded in this workspace", go, StringComparison.Ordinal);
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
