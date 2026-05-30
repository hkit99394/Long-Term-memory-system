using System.Text.Json;

namespace MemorySystem.UnitTests;

public sealed class ObservabilityArtifactTests
{
    private static readonly string[] RequiredAreas =
    [
        "api",
        "worker",
        "postgresql",
        "retrieval",
        "review",
        "vault_export",
        "backup",
        "governance"
    ];

    [Fact]
    public void Observability_artifacts_cover_required_pilot_areas_and_metric_inputs()
    {
        var root = FindRepositoryRoot();
        var apiMetrics = ReadMetricList(root, "observability/alert-inputs/api-metrics.txt");
        var externalMetrics = ReadMetricList(root, "observability/alert-inputs/external-pilot-metrics.txt");
        var alertRules = File.ReadAllText(Path.Combine(root, "observability/prometheus/memorysystem-pilot-alerts.yml"));
        var dashboardJson = File.ReadAllText(Path.Combine(root, "observability/grafana/memorysystem-pilot-dashboard.json"));
        var tracingJson = File.ReadAllText(Path.Combine(root, "observability/tracing/memorysystem-pilot-trace-coverage.json"));

        using var dashboard = JsonDocument.Parse(dashboardJson);
        using var tracing = JsonDocument.Parse(tracingJson);

        Assert.True(apiMetrics.Count >= 20);
        Assert.True(externalMetrics.Count >= 5);
        Assert.Equal("memorysystem-pilot", dashboard.RootElement.GetProperty("uid").GetString());
        Assert.True(dashboard.RootElement.GetProperty("panels").GetArrayLength() >= RequiredAreas.Length);

        foreach (var metric in apiMetrics.Concat(externalMetrics))
        {
            Assert.Contains(metric, dashboardJson, StringComparison.Ordinal);
        }

        foreach (var area in RequiredAreas)
        {
            Assert.Contains($"area: {area}", alertRules, StringComparison.Ordinal);
            Assert.Contains($"area:{area}", dashboardJson, StringComparison.Ordinal);
        }

        var traceRequiredAreas = tracing.RootElement
            .GetProperty("requiredAreas")
            .EnumerateArray()
            .Select(area => area.GetString())
            .ToHashSet(StringComparer.Ordinal);
        var traceSpanAreas = tracing.RootElement
            .GetProperty("spans")
            .EnumerateArray()
            .Select(span => span.GetProperty("area").GetString())
            .ToHashSet(StringComparer.Ordinal);

        foreach (var area in RequiredAreas)
        {
            Assert.Contains(area, traceRequiredAreas);
            Assert.Contains(area, traceSpanAreas);
        }

        var forbiddenAttributes = tracing.RootElement
            .GetProperty("forbiddenAttributes")
            .EnumerateArray()
            .Select(attribute => attribute.GetString())
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("memory.raw_query", forbiddenAttributes);
        Assert.Contains("event.raw_payload", forbiddenAttributes);
        Assert.Contains("embedding.input", forbiddenAttributes);
    }

    private static IReadOnlyList<string> ReadMetricList(string root, string relativePath)
    {
        return File.ReadAllLines(Path.Combine(root, relativePath))
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToArray();
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
