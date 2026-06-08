using System.Text.Json;

namespace MemorySystem.UnitTests;

public sealed class ProjectSuccessBenchmarkPs04Tests
{
    [Fact]
    public void Ps04_closeout_runbook_and_template_are_documented()
    {
        var root = FindRepositoryRoot();
        var productPlan = File.ReadAllText(Path.Combine(root, "docs", "product-improvement-plan.md"));
        var backlog = File.ReadAllText(Path.Combine(root, "docs", "backlog.md"));
        var ps01 = File.ReadAllText(Path.Combine(root, "docs", "project-success-benchmark-ps01.md"));
        var ps03 = File.ReadAllText(Path.Combine(root, "docs", "project-success-observation-ps03.md"));
        var ps04 = File.ReadAllText(Path.Combine(root, "docs", "project-success-closeout-ps04.md"));
        var docsIndex = File.ReadAllText(Path.Combine(root, "docs", "README.md"));
        var benchmarking = File.ReadAllText(Path.Combine(root, "docs", "benchmarking.md"));
        var folderStructure = File.ReadAllText(Path.Combine(root, "docs", "folder-structure.md"));
        var testing = File.ReadAllText(Path.Combine(root, "docs", "testing.md"));
        var benchmarksIndex = File.ReadAllText(Path.Combine(root, "benchmarks", "README.md"));
        var benchmarkReadme = File.ReadAllText(Path.Combine(root, "benchmarks", "project-success", "README.md"));

        Assert.Contains("| PS-04 | P0 | In Progress | Product Owner + Tester/QA + Knowledge Steward + Security Professional + Release Manager + Pilot Operator | Project Success Closeout |", productPlan, StringComparison.Ordinal);
        Assert.Contains("| PS-04 | P0 | In Progress | Produce project-success closeout. |", backlog, StringComparison.Ordinal);
        Assert.Contains("| PS-04 | P0 | In Progress | Produce closeout and promotion recommendation. |", ps01, StringComparison.Ordinal);
        Assert.Contains("closeout-template.json", ps03, StringComparison.Ordinal);
        Assert.Contains("# Project Success Closeout PS-04", ps04, StringComparison.Ordinal);
        Assert.Contains("[Project Success Closeout PS-04](project-success-closeout-ps04.md)", docsIndex, StringComparison.Ordinal);
        Assert.Contains("scorecard, weekly-cycle, and closeout templates", benchmarking, StringComparison.Ordinal);
        Assert.Contains("docs/project-success-closeout-ps04.md", folderStructure, StringComparison.Ordinal);
        Assert.Contains("benchmarks/project-success/closeout-template.json", folderStructure, StringComparison.Ordinal);
        Assert.Contains("closeout-template.json", testing, StringComparison.Ordinal);
        Assert.Contains("PS-01 through PS-04 pilot scorecards", benchmarksIndex, StringComparison.Ordinal);
        Assert.Contains("memorysystem.project_success_closeout", benchmarkReadme, StringComparison.Ordinal);

        foreach (var required in new[]
        {
            "2026-06-29 to 2026-07-05",
            "2026-07-05",
            "Cycle 1 weekly observation record",
            "Cycle 2 weekly observation record",
            "GO",
            "WATCH",
            "NO-GO",
            "source-backed memory hygiene",
            "backlog/roadmap memory sync",
            "missing-cycle reason",
            "must not be recorded before 2026-06-29"
        })
        {
            Assert.Contains(required, ps04, StringComparison.Ordinal);
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
            Assert.Contains(payloadSafetyRule, ps04, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Ps04_closeout_template_is_payload_safe()
    {
        var root = FindRepositoryRoot();
        var templatePath = Path.Combine(root, "benchmarks", "project-success", "closeout-template.json");

        using var document = JsonDocument.Parse(File.ReadAllText(templatePath));
        var rootElement = document.RootElement;

        Assert.Equal("memorysystem.project_success_closeout", rootElement.GetProperty("kind").GetString());
        Assert.Equal(1, rootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("PS-04", rootElement.GetProperty("benchmarkId").GetString());
        Assert.True(rootElement.GetProperty("payloadSafe").GetBoolean());
        Assert.False(rootElement.GetProperty("rawSourcePayloadsIncluded").GetBoolean());
        Assert.False(rootElement.GetProperty("rawMemoryPayloadsIncluded").GetBoolean());
        Assert.False(rootElement.GetProperty("rawQueriesIncluded").GetBoolean());

        var pilotProject = rootElement.GetProperty("pilotProject");
        Assert.Equal("9f8e7d6c-5b4a-4321-9123-abcdef123001", pilotProject.GetProperty("organizationId").GetString());
        Assert.Equal("9f8e7d6c-5b4a-4321-9123-abcdef123002", pilotProject.GetProperty("projectId").GetString());
        Assert.Equal("Long-Term Memory System", pilotProject.GetProperty("projectName").GetString());

        var period = rootElement.GetProperty("period");
        Assert.Equal("2026-06-29", period.GetProperty("closeoutStart").GetString());
        Assert.Equal("2026-07-05", period.GetProperty("closeoutDue").GetString());

        var requiredEvidence = rootElement.GetProperty("requiredEvidence");
        foreach (var field in ExpectedEvidenceFields)
        {
            Assert.True(requiredEvidence.TryGetProperty(field, out _), $"Missing required evidence field {field}.");
        }

        var finalMetrics = rootElement.GetProperty("finalMetrics");
        foreach (var section in ExpectedMetricSections)
        {
            Assert.True(finalMetrics.TryGetProperty(section, out _), $"Missing final metrics section {section}.");
        }

        var hardGateChecks = rootElement.GetProperty("hardGateChecks");
        foreach (var field in ExpectedHardGateFields)
        {
            Assert.True(hardGateChecks.TryGetProperty(field, out _), $"Missing hard-gate field {field}.");
        }

        var recommendation = rootElement.GetProperty("recommendation");
        Assert.Equal("pending", recommendation.GetProperty("decision").GetString());
        Assert.True(recommendation.TryGetProperty("residualRisks", out _));
        Assert.True(recommendation.TryGetProperty("nextAction", out _));
    }

    private static readonly string[] ExpectedEvidenceFields =
    [
        "cycle1Record",
        "cycle2Record",
        "finalScorecard",
        "operationsSummary",
        "weeklyAdminReviewCycle1",
        "weeklyAdminReviewCycle2",
        "accessBoundaryReviewCycle1",
        "accessBoundaryReviewCycle2",
        "sourceBackedMemoryHygiene",
        "backlogRoadmapSync",
        "benchmarkReleaseGate",
        "confidenceRatings"
    ];

    private static readonly string[] ExpectedMetricSections =
    [
        "adoption",
        "memoryQuality",
        "trustAndSafety",
        "operatorBurden",
        "projectDeliveryImpact",
        "humanConfidence"
    ];

    private static readonly string[] ExpectedHardGateFields =
    [
        "scopedSafetyLeaksPass",
        "staleMemoryBenchmarkUsagePass",
        "sourceLinkCoveragePass",
        "rawPayloadLeakagePass",
        "criticalReviewSlaPass",
        "weeklyAccessBoundaryReviewPass",
        "missingCycleReasonAccepted"
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
