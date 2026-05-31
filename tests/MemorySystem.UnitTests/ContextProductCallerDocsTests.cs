namespace MemorySystem.UnitTests;

public sealed class ContextProductCallerDocsTests
{
    [Fact]
    public void Context_product_caller_docs_cover_exclusions_feedback_and_raw_query_hygiene()
    {
        var root = FindRepositoryRoot();
        var guidePath = Path.Combine(root, "docs", "api", "context-product-v1-caller-guide.md");
        var examplesPath = Path.Combine(root, "docs", "api", "agent-memory-v1-examples.md");
        var scriptPath = Path.Combine(root, "docs", "api", "examples", "agent-memory-v1-curl.sh");

        Assert.True(File.Exists(guidePath));
        Assert.True(File.Exists(examplesPath));
        Assert.True(File.Exists(scriptPath));

        var guide = File.ReadAllText(guidePath);
        Assert.Contains("explanation.primaryReason", guide, StringComparison.Ordinal);
        Assert.Contains("countDisclosure: \"withheld\"", guide, StringComparison.Ordinal);
        Assert.Contains("POST /api/memory/context/feedback", guide, StringComparison.Ordinal);
        Assert.Contains("/api/reviews/context-observations/$FEEDBACK_ID/review", guide, StringComparison.Ordinal);
        Assert.Contains("Prefer `packetId` over `query`", guide, StringComparison.Ordinal);

        var examples = File.ReadAllText(examplesPath);
        Assert.Contains("Context Product v1 Caller Guide", examples, StringComparison.Ordinal);

        var script = File.ReadAllText(scriptPath);
        Assert.Contains("context_product_summary", script, StringComparison.Ordinal);
        Assert.Contains("\"sourceType\": \"${feedback_source_type}\"", script, StringComparison.Ordinal);
        Assert.Contains("\"sourceId\": \"${feedback_source_id}\"", script, StringComparison.Ordinal);
        Assert.Contains("\"feedbackType\": \"missing\"", script, StringComparison.Ordinal);
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
