using System.Text.Json;
using MemorySystem.Application.Operations;

namespace MemorySystem.Api.Operations;

public sealed class ContextProductBenchmarkMetricReader(
    IConfiguration configuration,
    IHostEnvironment environment) : IContextProductBenchmarkMetricReader
{
    private const string DefaultRelativePath = "benchmarks/outputs/context-product-v1/latest.json";
    private static readonly IReadOnlyList<OperationalContextProductBenchmarkDeltaSummary> EmptyDeltas = [];

    public OperationalContextProductBenchmarkSummary Read()
    {
        var source = ResolvePath();
        if (!File.Exists(source))
        {
            return new OperationalContextProductBenchmarkSummary(
                Observed: false,
                source,
                GeneratedAt: null,
                ReadError: null,
                EmptyDeltas);
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(source));
            var root = document.RootElement;
            var generatedAt = TryReadGeneratedAt(root);
            var metrics = root.TryGetProperty("metrics", out var metricElement)
                ? metricElement
                : default;

            var deltas = new List<OperationalContextProductBenchmarkDeltaSummary>();
            AddDelta(metrics, deltas, "feedbackAdjustmentDelta");
            AddDelta(metrics, deltas, "rankDelta");

            return new OperationalContextProductBenchmarkSummary(
                Observed: deltas.Count > 0,
                source,
                generatedAt,
                ReadError: deltas.Count > 0 ? null : "missing_metrics",
                deltas);
        }
        catch (JsonException)
        {
            return new OperationalContextProductBenchmarkSummary(
                Observed: false,
                source,
                GeneratedAt: null,
                ReadError: "invalid_json",
                EmptyDeltas);
        }
        catch (IOException)
        {
            return new OperationalContextProductBenchmarkSummary(
                Observed: false,
                source,
                GeneratedAt: null,
                ReadError: "io_error",
                EmptyDeltas);
        }
        catch (UnauthorizedAccessException)
        {
            return new OperationalContextProductBenchmarkSummary(
                Observed: false,
                source,
                GeneratedAt: null,
                ReadError: "io_error",
                EmptyDeltas);
        }
    }

    private string ResolvePath()
    {
        var configuredPath = configuration["MemorySystem:ContextProductBenchmark:LatestResultPath"];
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        var directory = new DirectoryInfo(environment.ContentRootPath);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MemorySystem.sln")))
            {
                return Path.Combine(directory.FullName, DefaultRelativePath);
            }

            directory = directory.Parent;
        }

        return Path.Combine(environment.ContentRootPath, DefaultRelativePath);
    }

    private static DateTimeOffset? TryReadGeneratedAt(JsonElement root)
    {
        if (!root.TryGetProperty("generatedAt", out var generatedAtElement)
            || generatedAtElement.ValueKind != JsonValueKind.String
            || !DateTimeOffset.TryParse(generatedAtElement.GetString(), out var generatedAt))
        {
            return null;
        }

        return generatedAt;
    }

    private static void AddDelta(
        JsonElement metrics,
        List<OperationalContextProductBenchmarkDeltaSummary> deltas,
        string metricName)
    {
        if (metrics.ValueKind != JsonValueKind.Object
            || !metrics.TryGetProperty(metricName, out var metric)
            || metric.ValueKind is not JsonValueKind.Number
            || !metric.TryGetDouble(out var value))
        {
            return;
        }

        deltas.Add(new OperationalContextProductBenchmarkDeltaSummary(metricName, value));
    }
}
