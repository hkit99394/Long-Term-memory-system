using System.Text.Json;

namespace MemorySystem.UnitTests;

public sealed class ContextProductBenchmarkArtifactTests
{
    [Fact]
    public void Context_product_benchmark_artifacts_define_cp08_smoke_tasks()
    {
        var root = FindRepositoryRoot();
        var benchmarkDirectory = Path.Combine(root, "benchmarks", "context-product-v1");
        var tasksPath = Path.Combine(benchmarkDirectory, "tasks.json");
        var runnerPath = Path.Combine(benchmarkDirectory, "run_smoke.py");
        var wrapperPath = Path.Combine(root, "scripts", "context-product-benchmark-smoke.sh");

        Assert.True(File.Exists(tasksPath));
        Assert.True(File.Exists(runnerPath));
        Assert.True(File.Exists(wrapperPath));

        using var document = JsonDocument.Parse(File.ReadAllText(tasksPath));
        var rootElement = document.RootElement;

        Assert.Equal("context-product-v1", rootElement.GetProperty("suiteId").GetString());
        Assert.Equal("context-product-v1", rootElement.GetProperty("contractVersion").GetString());

        var taskIds = rootElement
            .GetProperty("tasks")
            .EnumerateArray()
            .Select(task => task.GetProperty("id").GetString())
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("context-product-cp08-001", taskIds);
        Assert.Contains("context-product-cp08-002", taskIds);
        Assert.Contains("context-product-cp08-003", taskIds);
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
