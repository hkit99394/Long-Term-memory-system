using System.Text.Json;

namespace MemorySystem.UnitTests;

public sealed class ProjectRegistrationReg06Tests
{
    [Fact]
    public void Reg06_registration_success_benchmark_is_documented_bundled_and_payload_safe()
    {
        var root = FindRepositoryRoot();
        var productPlan = File.ReadAllText(Path.Combine(root, "docs", "product-improvement-plan.md"));
        var backlog = File.ReadAllText(Path.Combine(root, "docs", "backlog.md"));
        var registrationPlan = File.ReadAllText(Path.Combine(root, "docs", "project-registration-ux-plan.md"));
        var testing = File.ReadAllText(Path.Combine(root, "docs", "testing.md"));
        var benchmarkReadme = File.ReadAllText(Path.Combine(root, "benchmarks", "project-success", "README.md"));
        var registrationPanel = File.ReadAllText(Path.Combine(root, "tools", "ui", "src", "admin", "registration-panel.ts"));
        var script = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "wwwroot", "admin", "admin-console.js"));

        Assert.Contains("| REG-06 | P1 | Done | Product Owner + Tester/QA | Registration Success Benchmark |", productPlan, StringComparison.Ordinal);
        Assert.Contains("| REG-06 | P1 | Done | Measure registration success.", backlog, StringComparison.Ordinal);
        Assert.Contains("Status: REG-01 through REG-06 implemented.", registrationPlan, StringComparison.Ordinal);
        Assert.Contains("## REG-06 Registration Success Benchmark", registrationPlan, StringComparison.Ordinal);
        Assert.Contains("ProjectRegistrationReg06Tests", testing, StringComparison.Ordinal);
        Assert.Contains("Project registration success is tracked as supporting Product Owner evidence", benchmarkReadme, StringComparison.Ordinal);

        foreach (var fragment in new[]
                 {
                     "registrationSuccessBenchmarkControls",
                     "registrationSuccessBenchmarkItems",
                     "registrationSuccessBenchmarkEvidence",
                     "registrationSuccessBenchmarkStatusText",
                     "registrationDurationSeconds",
                     "formatRegistrationDuration",
                     "registrationValidationFailureCount",
                     "validationFailureCount",
                     "accessDriftFindingCount",
                     "accessDriftStatus",
                     "registrationSourceLinkCoverageRatio",
                     "sourceLinkCoverage",
                     "registrationSourceLinkCoveragePercent",
                     "sourceLinkCoveragePercent",
                     "projectRegistration",
                     "userConfidence",
                     "userConfidenceScore",
                     "projectSuccessEvidenceLoop",
                     "registration-access-drift",
                     "registration-confidence-score",
                     "Raw source payloads",
                     "not included"
                 })
        {
            Assert.Contains(fragment, registrationPanel, StringComparison.Ordinal);
            Assert.Contains(fragment, script, StringComparison.Ordinal);
        }

        Assert.Contains("sourceDocuments: compactRegistrationSources().map(source => ({", registrationPanel, StringComparison.Ordinal);
        Assert.DoesNotContain("registrationSuccessBenchmark: registrationSuccessBenchmarkEvidence()", registrationPanel, StringComparison.Ordinal);
    }

    [Fact]
    public void Reg06_project_success_templates_track_registration_success()
    {
        var root = FindRepositoryRoot();
        var scorecardTemplatePath = Path.Combine(root, "benchmarks", "project-success", "scorecard-template.json");
        var weeklyTemplatePath = Path.Combine(root, "benchmarks", "project-success", "weekly-cycle-template.json");
        var closeoutTemplatePath = Path.Combine(root, "benchmarks", "project-success", "closeout-template.json");

        using var scorecard = JsonDocument.Parse(File.ReadAllText(scorecardTemplatePath));
        using var weekly = JsonDocument.Parse(File.ReadAllText(weeklyTemplatePath));
        using var closeout = JsonDocument.Parse(File.ReadAllText(closeoutTemplatePath));

        var targets = scorecard.RootElement.GetProperty("targets");
        Assert.Equal(30, targets.GetProperty("registrationDurationMinutesMaximum").GetInt32());
        Assert.Equal(0, targets.GetProperty("registrationValidationFailuresMaximum").GetInt32());
        Assert.Equal(0, targets.GetProperty("registrationAccessDriftMaximum").GetInt32());
        Assert.Equal(1.0, targets.GetProperty("registrationSourceLinkCoverageMinimum").GetDouble());
        Assert.Equal(4.0, targets.GetProperty("registrationUserConfidenceMinimum").GetDouble());

        AssertRegistrationMetricBlock(scorecard.RootElement.GetProperty("metrics").GetProperty("projectRegistration"));
        AssertRegistrationMetricBlock(weekly.RootElement.GetProperty("measurements").GetProperty("projectRegistration"));
        AssertRegistrationMetricBlock(closeout.RootElement.GetProperty("finalMetrics").GetProperty("projectRegistration"));

        Assert.True(scorecard.RootElement.GetProperty("evidence").TryGetProperty("registrationSuccessBenchmark", out _));
        Assert.True(weekly.RootElement.GetProperty("evidence").TryGetProperty("registrationSuccessBenchmark", out _));
        Assert.True(closeout.RootElement.GetProperty("requiredEvidence").TryGetProperty("registrationSuccessBenchmark", out _));
    }

    private static void AssertRegistrationMetricBlock(JsonElement block)
    {
        foreach (var field in new[]
                 {
                     "registrationDurationSeconds",
                     "validationFailureCount",
                     "accessDriftFindingCount",
                     "sourceLinkCoverage",
                     "userConfidence"
                 })
        {
            Assert.True(block.TryGetProperty(field, out _), $"Missing project registration metric {field}.");
        }
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
