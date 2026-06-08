using System.Text.Json;

namespace MemorySystem.UnitTests;

public sealed class ProjectSuccessBenchmarkPs01Tests
{
    [Fact]
    public void Ps01_contract_is_documented_and_wired()
    {
        var root = FindRepositoryRoot();
        var productPlan = File.ReadAllText(Path.Combine(root, "docs", "product-improvement-plan.md"));
        var backlog = File.ReadAllText(Path.Combine(root, "docs", "backlog.md"));
        var contract = File.ReadAllText(Path.Combine(root, "docs", "project-success-benchmark-ps01.md"));
        var docsIndex = File.ReadAllText(Path.Combine(root, "docs", "README.md"));
        var benchmarking = File.ReadAllText(Path.Combine(root, "docs", "benchmarking.md"));
        var folderStructure = File.ReadAllText(Path.Combine(root, "docs", "folder-structure.md"));
        var testing = File.ReadAllText(Path.Combine(root, "docs", "testing.md"));
        var benchmarksIndex = File.ReadAllText(Path.Combine(root, "benchmarks", "README.md"));
        var benchmarkReadme = File.ReadAllText(Path.Combine(root, "benchmarks", "project-success", "README.md"));
        var scorecardTemplatePath = Path.Combine(root, "benchmarks", "project-success", "scorecard-template.json");

        Assert.Contains("| PS-01 | P0 | Done | Product Owner + Tester/QA + Knowledge Steward | Pilot Project Success Scorecard |", productPlan, StringComparison.Ordinal);
        Assert.Contains("| PS-01 | P0 | Done | Define pilot project success scorecard. |", backlog, StringComparison.Ordinal);
        Assert.Contains("| PS-02 | P0 | Done | Select first pilot project and baseline. |", backlog, StringComparison.Ordinal);
        Assert.Contains("# Project Success Benchmark PS-01", contract, StringComparison.Ordinal);
        Assert.Contains("[Project Success Benchmark PS-01](project-success-benchmark-ps01.md)", docsIndex, StringComparison.Ordinal);
        Assert.Contains("[Project Success Benchmark PS-01](project-success-benchmark-ps01.md)", benchmarking, StringComparison.Ordinal);
        Assert.Contains("project-success/", folderStructure, StringComparison.Ordinal);
        Assert.Contains("benchmarks/project-success/scorecard-template.json", testing, StringComparison.Ordinal);
        Assert.Contains("[project-success](project-success/README.md)", benchmarksIndex, StringComparison.Ordinal);
        Assert.Contains("memorysystem.project_success_scorecard", benchmarkReadme, StringComparison.Ordinal);
        Assert.True(File.Exists(scorecardTemplatePath));

        foreach (var required in RequiredSignals)
        {
            Assert.Contains(required, contract, StringComparison.Ordinal);
            Assert.Contains(required, benchmarkReadme, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Ps01_scorecard_template_is_payload_safe_and_has_targets()
    {
        var root = FindRepositoryRoot();
        var scorecardTemplatePath = Path.Combine(root, "benchmarks", "project-success", "scorecard-template.json");

        using var document = JsonDocument.Parse(File.ReadAllText(scorecardTemplatePath));
        var rootElement = document.RootElement;

        Assert.Equal("memorysystem.project_success_scorecard", rootElement.GetProperty("kind").GetString());
        Assert.Equal(1, rootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("PS-01", rootElement.GetProperty("benchmarkId").GetString());
        Assert.True(rootElement.GetProperty("payloadSafe").GetBoolean());
        Assert.False(rootElement.GetProperty("rawSourcePayloadsIncluded").GetBoolean());
        Assert.False(rootElement.GetProperty("rawMemoryPayloadsIncluded").GetBoolean());

        var period = rootElement.GetProperty("period");
        Assert.Equal("2026-06-08", period.GetProperty("planningStart").GetString());
        Assert.Equal("2026-06-14", period.GetProperty("planningEnd").GetString());
        Assert.Equal("2026-06-15", period.GetProperty("pilotStart").GetString());
        Assert.Equal("2026-06-28", period.GetProperty("pilotEnd").GetString());
        Assert.Equal("2026-07-05", period.GetProperty("closeoutDue").GetString());

        var targets = rootElement.GetProperty("targets");
        Assert.Equal(1.0, targets.GetProperty("sourceLinkCoverageMinimum").GetDouble());
        Assert.Equal(0, targets.GetProperty("scopedSafetyLeaksMaximum").GetInt32());
        Assert.Equal(0, targets.GetProperty("staleMemoryBenchmarkUsageMaximum").GetInt32());
        Assert.Equal(0, targets.GetProperty("rawPayloadLeakageMaximum").GetInt32());
        Assert.Equal(2, targets.GetProperty("criticalReviewResolutionBusinessDaysMaximum").GetInt32());
        Assert.Equal(0.7, targets.GetProperty("feedbackCoverageMinimum").GetDouble());
        Assert.Equal(4.0, targets.GetProperty("pilotUserConfidenceMinimum").GetDouble());
        Assert.Equal(4.0, targets.GetProperty("operatorConfidenceMinimum").GetDouble());

        var metrics = rootElement.GetProperty("metrics");
        foreach (var section in new[]
        {
            "adoption",
            "memoryQuality",
            "trustAndSafety",
            "operatorBurden",
            "projectDeliveryImpact",
            "humanConfidence"
        })
        {
            Assert.True(metrics.TryGetProperty(section, out _), $"Missing metrics section {section}.");
        }

        var evidence = rootElement.GetProperty("evidence");
        Assert.True(evidence.TryGetProperty("operationsSummary", out _));
        Assert.True(evidence.TryGetProperty("weeklyAdminReview", out _));
        Assert.True(evidence.TryGetProperty("accessBoundaryReview", out _));
        Assert.True(evidence.TryGetProperty("benchmarkReleaseGate", out _));
    }

    private static readonly string[] RequiredSignals =
    [
        "adoption",
        "memory quality",
        "trust and safety",
        "operator burden",
        "project delivery impact",
        "human confidence"
    ];

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

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
