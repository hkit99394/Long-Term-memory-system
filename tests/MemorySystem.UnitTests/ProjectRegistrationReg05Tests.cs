namespace MemorySystem.UnitTests;

public sealed class ProjectRegistrationReg05Tests
{
    [Fact]
    public void Reg05_seed_readiness_ux_is_documented_bundled_and_payload_safe()
    {
        var root = FindRepositoryRoot();
        var productPlan = File.ReadAllText(Path.Combine(root, "docs", "product-improvement-plan.md"));
        var backlog = File.ReadAllText(Path.Combine(root, "docs", "backlog.md"));
        var registrationPlan = File.ReadAllText(Path.Combine(root, "docs", "project-registration-ux-plan.md"));
        var testing = File.ReadAllText(Path.Combine(root, "docs", "testing.md"));
        var registrationPanel = File.ReadAllText(Path.Combine(root, "tools", "ui", "src", "admin", "registration-panel.ts"));
        var script = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "wwwroot", "admin", "admin-console.js"));
        var css = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "wwwroot", "admin", "admin-console.css"));

        Assert.Contains("| REG-05 | P1 | Done | Knowledge Steward + Product Owner | Source-Backed Seed Readiness UX |", productPlan, StringComparison.Ordinal);
        Assert.Contains("| REG-05 | P1 | Done | Add source-backed seed readiness UX.", backlog, StringComparison.Ordinal);
        Assert.Contains("Status: REG-01 through REG-06 implemented.", registrationPlan, StringComparison.Ordinal);
        Assert.Contains("## REG-05 Source-Backed Seed Readiness UX", registrationPlan, StringComparison.Ordinal);
        Assert.Contains("ProjectRegistrationReg05Tests", testing, StringComparison.Ordinal);

        foreach (var fragment in new[]
                 {
                     "registrationMemoryTypes",
                     "\"role_lens\"",
                     "registrationMemoryTypeChecklist",
                     "registrationSeedReadinessItems",
                     "registrationSeedReadinessEvidence",
                     "registrationSourceHashCoveragePercent",
                     "registrationRoleLensReadinessLabel",
                     "seedContextCheckStatus",
                     "seedFeedbackStatus",
                     "Memory types",
                     "Role-lens readiness",
                     "Raw source payloads",
                     "not included"
                 })
        {
            Assert.Contains(fragment, registrationPanel, StringComparison.Ordinal);
            Assert.Contains(fragment, script, StringComparison.Ordinal);
        }

        Assert.Contains("sourceDocuments: compactRegistrationSources().map(source => ({", registrationPanel, StringComparison.Ordinal);
        Assert.DoesNotContain("sourceDocuments: compactRegistrationSources(),", registrationPanel, StringComparison.Ordinal);
        Assert.Contains(".registration-check-group", css, StringComparison.Ordinal);
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
