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

        Assert.Contains("# Target-Environment Pilot Rehearsal P0", rehearsal, StringComparison.Ordinal);
        Assert.Contains("Status: planned; execution is pending the intended pilot environment.", rehearsal, StringComparison.Ordinal);
        Assert.Contains("release_evidence_bucket", rehearsal, StringComparison.Ordinal);
        Assert.Contains("scripts/production-pilot-deployment-smoke.sh", rehearsal, StringComparison.Ordinal);
        Assert.Contains("scripts/benchmark-release-gate.sh", rehearsal, StringComparison.Ordinal);
        Assert.Contains("scripts/governance-compliance-release-smoke.sh", rehearsal, StringComparison.Ordinal);
        Assert.Contains("Alert receiver acknowledgement", rehearsal, StringComparison.Ordinal);
        Assert.Contains("Release owner signature", rehearsal, StringComparison.Ordinal);
        Assert.Contains("Rollback owner signature", rehearsal, StringComparison.Ordinal);

        Assert.Contains("[Target-Environment Pilot Rehearsal P0](target-environment-pilot-rehearsal-p0.md)", index, StringComparison.Ordinal);
        Assert.Contains("## External Pilot Readiness P0", backlog, StringComparison.Ordinal);
        Assert.Contains("| EPR-01 | P0 | Done | Define the target-environment pilot rehearsal runbook.", backlog, StringComparison.Ordinal);
        Assert.Contains("| EPR-02 | P0 | Todo | Run target-environment deployment smoke.", backlog, StringComparison.Ordinal);
        Assert.Contains("| EPR-03 | P0 | Todo | Attach fresh pilot release evidence.", backlog, StringComparison.Ordinal);
        Assert.Contains("| EPR-04 | P0 | Todo | Sign the external-pilot go/no-go record.", backlog, StringComparison.Ordinal);
        Assert.Contains("Target-Environment Pilot Rehearsal P0", roadmap, StringComparison.Ordinal);
        Assert.Contains("Target-Environment Pilot Rehearsal P0", readinessReview, StringComparison.Ordinal);
        Assert.Contains("Target-Environment Pilot Rehearsal P0", releaseChecklist, StringComparison.Ordinal);
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
