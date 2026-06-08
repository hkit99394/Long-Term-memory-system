using System.Text.Json;

namespace MemorySystem.UnitTests;

public sealed class ProjectSuccessBenchmarkPs03Tests
{
    [Fact]
    public void Ps03_observation_runbook_and_template_are_documented()
    {
        var root = FindRepositoryRoot();
        var productPlan = File.ReadAllText(Path.Combine(root, "docs", "product-improvement-plan.md"));
        var backlog = File.ReadAllText(Path.Combine(root, "docs", "backlog.md"));
        var ps01 = File.ReadAllText(Path.Combine(root, "docs", "project-success-benchmark-ps01.md"));
        var ps02 = File.ReadAllText(Path.Combine(root, "docs", "project-success-pilot-baseline-ps02.md"));
        var ps03 = File.ReadAllText(Path.Combine(root, "docs", "project-success-observation-ps03.md"));
        var docsIndex = File.ReadAllText(Path.Combine(root, "docs", "README.md"));
        var benchmarking = File.ReadAllText(Path.Combine(root, "docs", "benchmarking.md"));
        var folderStructure = File.ReadAllText(Path.Combine(root, "docs", "folder-structure.md"));
        var testing = File.ReadAllText(Path.Combine(root, "docs", "testing.md"));
        var benchmarksIndex = File.ReadAllText(Path.Combine(root, "benchmarks", "README.md"));
        var benchmarkReadme = File.ReadAllText(Path.Combine(root, "benchmarks", "project-success", "README.md"));

        Assert.Contains("| PS-03 | P0 | In Progress | Product Owner + Tester/QA + Knowledge Steward + Security Professional + Pilot Operator | Two-Week Pilot Observation |", productPlan, StringComparison.Ordinal);
        Assert.Contains("| PS-03 | P0 | In Progress | Run two-week pilot observation. |", backlog, StringComparison.Ordinal);
        Assert.Contains("| PS-03 | P0 | In Progress | Run two-week pilot observation. |", ps01, StringComparison.Ordinal);
        Assert.Contains("[Project Success Observation PS-03](project-success-observation-ps03.md)", ps02, StringComparison.Ordinal);
        Assert.Contains("# Project Success Observation PS-03", ps03, StringComparison.Ordinal);
        Assert.Contains("[Project Success Observation PS-03](project-success-observation-ps03.md)", docsIndex, StringComparison.Ordinal);
        Assert.Contains("scorecard and weekly-cycle templates", benchmarking, StringComparison.Ordinal);
        Assert.Contains("docs/project-success-observation-ps03.md", folderStructure, StringComparison.Ordinal);
        Assert.Contains("benchmarks/project-success/weekly-cycle-template.json", folderStructure, StringComparison.Ordinal);
        Assert.Contains("weekly-cycle-template.json", testing, StringComparison.Ordinal);
        Assert.Contains("PS-01/PS-03 pilot scorecards and observation cycles", benchmarksIndex, StringComparison.Ordinal);
        Assert.Contains("memorysystem.project_success_observation_cycle", benchmarkReadme, StringComparison.Ordinal);

        foreach (var required in new[]
        {
            "2026-06-15 to 2026-06-21",
            "2026-06-22 to 2026-06-28",
            "2026-07-05",
            "memory.getContext",
            "memory.queryFacts",
            "source-backed memory hygiene",
            "backlog/roadmap memory sync",
            "scoped safety leaks above zero",
            "raw payload leakage in committed scorecard or cycle evidence"
        })
        {
            Assert.Contains(required, ps03, StringComparison.Ordinal);
        }

        foreach (var payloadSafetyRule in new[]
        {
            "raw source event payloads",
            "memory bodies",
            "review notes",
            "raw queries",
            "provider payloads",
            "API keys",
            "database connection strings",
            "secret values"
        })
        {
            Assert.Contains(payloadSafetyRule, ps03, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Ps03_weekly_cycle_template_is_payload_safe()
    {
        var root = FindRepositoryRoot();
        var templatePath = Path.Combine(root, "benchmarks", "project-success", "weekly-cycle-template.json");

        using var document = JsonDocument.Parse(File.ReadAllText(templatePath));
        var rootElement = document.RootElement;

        Assert.Equal("memorysystem.project_success_observation_cycle", rootElement.GetProperty("kind").GetString());
        Assert.Equal(1, rootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("PS-03", rootElement.GetProperty("benchmarkId").GetString());
        Assert.True(rootElement.GetProperty("payloadSafe").GetBoolean());
        Assert.False(rootElement.GetProperty("rawSourcePayloadsIncluded").GetBoolean());
        Assert.False(rootElement.GetProperty("rawMemoryPayloadsIncluded").GetBoolean());
        Assert.False(rootElement.GetProperty("rawQueriesIncluded").GetBoolean());

        var pilotProject = rootElement.GetProperty("pilotProject");
        Assert.Equal("9f8e7d6c-5b4a-4321-9123-abcdef123001", pilotProject.GetProperty("organizationId").GetString());
        Assert.Equal("9f8e7d6c-5b4a-4321-9123-abcdef123002", pilotProject.GetProperty("projectId").GetString());
        Assert.Equal("Long-Term Memory System", pilotProject.GetProperty("projectName").GetString());

        var measurements = rootElement.GetProperty("measurements");
        foreach (var section in ExpectedMetricSections)
        {
            Assert.True(measurements.TryGetProperty(section, out _), $"Missing measurements section {section}.");
        }

        var evidence = rootElement.GetProperty("evidence");
        foreach (var field in ExpectedEvidenceFields)
        {
            Assert.True(evidence.TryGetProperty(field, out _), $"Missing evidence field {field}.");
        }

        var hardGateChecks = rootElement.GetProperty("hardGateChecks");
        foreach (var field in ExpectedHardGateFields)
        {
            Assert.True(hardGateChecks.TryGetProperty(field, out _), $"Missing hard-gate field {field}.");
        }
    }

    private static readonly string[] ExpectedMetricSections =
    [
        "adoption",
        "memoryQuality",
        "trustAndSafety",
        "operatorBurden",
        "projectDeliveryImpact",
        "humanConfidence"
    ];

    private static readonly string[] ExpectedEvidenceFields =
    [
        "memoryPreworkRecords",
        "contextFeedbackRecords",
        "operationsSummary",
        "weeklyAdminReview",
        "accessBoundaryReview",
        "benchmarkReleaseGate",
        "sourceBackedMemoryHygiene",
        "backlogRoadmapSync",
        "confidenceSurvey"
    ];

    private static readonly string[] ExpectedHardGateFields =
    [
        "scopedSafetyLeaksPass",
        "staleMemoryBenchmarkUsagePass",
        "sourceLinkCoveragePass",
        "rawPayloadLeakagePass",
        "criticalReviewSlaPass",
        "weeklyAccessBoundaryReviewPass"
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
