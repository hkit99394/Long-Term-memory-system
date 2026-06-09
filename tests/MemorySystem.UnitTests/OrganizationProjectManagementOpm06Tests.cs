using System.Text.Json;

namespace MemorySystem.UnitTests;

public sealed class OrganizationProjectManagementOpm06Tests
{
    [Fact]
    public void Opm06_management_success_benchmark_is_documented_bundled_and_payload_safe()
    {
        var root = FindRepositoryRoot();
        var productPlan = File.ReadAllText(Path.Combine(root, "docs", "product-improvement-plan.md"));
        var backlog = File.ReadAllText(Path.Combine(root, "docs", "backlog.md"));
        var docsIndex = File.ReadAllText(Path.Combine(root, "docs", "README.md"));
        var testing = File.ReadAllText(Path.Combine(root, "docs", "testing.md"));
        var benchmarkReadme = File.ReadAllText(Path.Combine(root, "benchmarks", "project-success", "README.md"));
        var ps01 = File.ReadAllText(Path.Combine(root, "docs", "project-success-benchmark-ps01.md"));
        var opm01 = File.ReadAllText(Path.Combine(root, "docs", "organization-project-management-opm01.md"));
        var opm06 = File.ReadAllText(Path.Combine(root, "docs", "organization-project-management-opm06.md"));
        var sourcePanel = File.ReadAllText(Path.Combine(root, "tools", "ui", "src", "admin", "management-panel.ts"));
        var script = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "wwwroot", "admin", "admin-console.js"));
        var css = File.ReadAllText(Path.Combine(root, "src", "MemorySystem.Api", "wwwroot", "admin", "admin-console.css"));

        Assert.Contains("| OPM-06 | P1 | Done | Product Owner + Tester/QA | Management Success Benchmark |", productPlan, StringComparison.Ordinal);
        Assert.Contains("| OPM-06 | P1 | Done | Measure management success. |", backlog, StringComparison.Ordinal);
        Assert.Contains("[Organization And Project Management OPM-06](organization-project-management-opm06.md)", docsIndex, StringComparison.Ordinal);
        Assert.Contains("Status: OPM-01 through OPM-08 implemented.", opm01, StringComparison.Ordinal);
        Assert.Contains("# Organization And Project Management OPM-06", opm06, StringComparison.Ordinal);
        Assert.Contains("OrganizationProjectManagementOpm06Tests", testing, StringComparison.Ordinal);
        Assert.Contains("organizationProjectManagement", benchmarkReadme, StringComparison.Ordinal);
        Assert.Contains("Organization/project management success", ps01, StringComparison.Ordinal);

        foreach (var fragment in RequiredUiFragments)
        {
            Assert.Contains(fragment, sourcePanel, StringComparison.Ordinal);
            Assert.Contains(fragment, script, StringComparison.Ordinal);
        }

        Assert.Contains(".management-success-benchmark", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Opm06_project_success_templates_track_management_success()
    {
        var root = FindRepositoryRoot();
        var scorecardTemplatePath = Path.Combine(root, "benchmarks", "project-success", "scorecard-template.json");
        var weeklyTemplatePath = Path.Combine(root, "benchmarks", "project-success", "weekly-cycle-template.json");
        var closeoutTemplatePath = Path.Combine(root, "benchmarks", "project-success", "closeout-template.json");

        using var scorecard = JsonDocument.Parse(File.ReadAllText(scorecardTemplatePath));
        using var weekly = JsonDocument.Parse(File.ReadAllText(weeklyTemplatePath));
        using var closeout = JsonDocument.Parse(File.ReadAllText(closeoutTemplatePath));

        var targets = scorecard.RootElement.GetProperty("targets");
        Assert.Equal(120, targets.GetProperty("managementFindInspectDurationSecondsMaximum").GetInt32());
        Assert.Equal(300, targets.GetProperty("managementModificationDurationSecondsMaximum").GetInt32());
        Assert.Equal(0, targets.GetProperty("managementOperationFailuresMaximum").GetInt32());
        Assert.Equal(0, targets.GetProperty("managementAccessDriftMaximum").GetInt32());
        Assert.Equal(0, targets.GetProperty("managementSqlFallbackMaximum").GetInt32());
        Assert.Equal(0, targets.GetProperty("managementRawPayloadLeakageMaximum").GetInt32());
        Assert.Equal(4.0, targets.GetProperty("managementUserConfidenceMinimum").GetDouble());

        AssertManagementMetricBlock(scorecard.RootElement.GetProperty("metrics").GetProperty("organizationProjectManagement"));
        AssertManagementMetricBlock(weekly.RootElement.GetProperty("measurements").GetProperty("organizationProjectManagement"));
        AssertManagementMetricBlock(closeout.RootElement.GetProperty("finalMetrics").GetProperty("organizationProjectManagement"));

        Assert.True(scorecard.RootElement.GetProperty("evidence").TryGetProperty("organizationProjectManagementBenchmark", out _));
        Assert.True(weekly.RootElement.GetProperty("evidence").TryGetProperty("organizationProjectManagementBenchmark", out _));
        Assert.True(closeout.RootElement.GetProperty("requiredEvidence").TryGetProperty("organizationProjectManagementBenchmark", out _));
    }

    private static readonly string[] RequiredUiFragments =
    [
        "managementSuccessBenchmarkSection",
        "managementSuccessBenchmarkEvidence",
        "managementSuccessBenchmarkItems",
        "managementSuccessBenchmarkStatusText",
        "managementFindInspectDurationSeconds",
        "managementModificationDurationSeconds",
        "managementOperationFailureCount",
        "accessDriftFindingCount",
        "sqlFallbackCount",
        "rawPayloadLeakageCount",
        "organizationProjectManagement",
        "projectSuccessEvidenceLoop",
        "Management success benchmark",
        "Payload-safe benchmark",
        "SQL fallback",
        "Raw source payloads",
        "not included"
    ];

    private static void AssertManagementMetricBlock(JsonElement block)
    {
        foreach (var field in new[]
                 {
                     "managementFindInspectDurationSeconds",
                     "managementModificationDurationSeconds",
                     "managementOperationFailureCount",
                     "accessDriftFindingCount",
                     "sqlFallbackCount",
                     "rawPayloadLeakageCount",
                     "userConfidence"
                 })
        {
            Assert.True(block.TryGetProperty(field, out _), $"Missing organization/project management metric {field}.");
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
