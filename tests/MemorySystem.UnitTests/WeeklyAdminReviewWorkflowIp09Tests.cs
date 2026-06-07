using System.Diagnostics;
using System.Text.Json;

namespace MemorySystem.UnitTests;

public sealed class WeeklyAdminReviewWorkflowIp09Tests
{
    [Fact]
    public void Ip09_contract_is_documented_and_wired()
    {
        var root = FindRepositoryRoot();
        var productPlan = File.ReadAllText(Path.Combine(root, "docs", "product-improvement-plan.md"));
        var contract = File.ReadAllText(Path.Combine(root, "docs", "weekly-admin-review-workflow-ip09.md"));
        var docsIndex = File.ReadAllText(Path.Combine(root, "docs", "README.md"));
        var folderStructure = File.ReadAllText(Path.Combine(root, "docs", "folder-structure.md"));
        var runbook = File.ReadAllText(Path.Combine(root, "docs", "project-memory-runbook.md"));
        var testing = File.ReadAllText(Path.Combine(root, "docs", "testing.md"));
        var script = File.ReadAllText(Path.Combine(root, "scripts", "weekly-admin-review-workflow.sh"));

        Assert.Contains("| IP-09 | P1 | Done | Knowledge Steward + Role Owners | Weekly Admin Review Workflow |", productPlan, StringComparison.Ordinal);
        Assert.Contains("# Weekly Admin Review Workflow IP-09", contract, StringComparison.Ordinal);
        Assert.Contains("[Weekly Admin Review Workflow IP-09](weekly-admin-review-workflow-ip09.md)", docsIndex, StringComparison.Ordinal);
        Assert.Contains("docs/weekly-admin-review-workflow-ip09.md", folderStructure, StringComparison.Ordinal);
        Assert.Contains("scripts/weekly-admin-review-workflow.sh", runbook, StringComparison.Ordinal);
        Assert.Contains("bash -n scripts/weekly-admin-review-workflow.sh", testing, StringComparison.Ordinal);

        foreach (var required in new[]
        {
            "/api/reviews/pending",
            "/api/reviews/context-observations",
            "/api/admin/memory/facts",
            "/api/operations/summary",
            "source-backed-memory-hygiene.sh",
            "duplicate_memory_candidates",
            "missing_feedback_metrics",
            "reviewable_feedback_stale_wrong_sensitive"
        })
        {
            Assert.Contains(required, script, StringComparison.Ordinal);
        }

        foreach (var feedbackType in new[] { "stale", "wrong", "missing", "sensitive", "over_broad" })
        {
            Assert.Contains(feedbackType, contract, StringComparison.Ordinal);
            Assert.Contains(feedbackType, script, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Weekly_admin_review_workflow_dry_run_is_payload_safe_and_complete()
    {
        var root = FindRepositoryRoot();

        var syntax = await RunScriptAsync(root, ["-n", "scripts/weekly-admin-review-workflow.sh"]);
        Assert.Equal(0, syntax.ExitCode);

        var help = await RunScriptAsync(root, ["scripts/weekly-admin-review-workflow.sh", "--help"]);
        Assert.Equal(0, help.ExitCode);
        Assert.Contains("payload-safe weekly admin memory review queue", help.StandardOutput, StringComparison.Ordinal);

        var dryRun = await RunScriptAsync(root, ["scripts/weekly-admin-review-workflow.sh", "--dry-run"]);
        Assert.Equal(0, dryRun.ExitCode);

        using var document = JsonDocument.Parse(dryRun.StandardOutput);
        var rootElement = document.RootElement;

        Assert.Equal("dry_run", rootElement.GetProperty("status").GetString());
        Assert.True(rootElement.GetProperty("payloadSafe").GetBoolean());
        Assert.False(rootElement.GetProperty("rawSourcePayloadsIncluded").GetBoolean());
        Assert.Equal("scripts/source-backed-memory-hygiene.sh", rootElement.GetProperty("sourceHygieneCommand").GetString());

        var checks = rootElement.GetProperty("checks").EnumerateArray()
            .Select(element => element.GetString())
            .ToArray();
        Assert.Contains("pending_reviews", checks);
        Assert.Contains("reviewable_feedback_stale_wrong_sensitive", checks);
        Assert.Contains("over_broad_feedback_triage", checks);
        Assert.Contains("missing_feedback_metrics", checks);
        Assert.Contains("duplicate_memory_candidates", checks);
        Assert.Contains("source_drift_hygiene", checks);

        var endpoints = rootElement.GetProperty("endpoints").EnumerateArray()
            .Select(element => element.GetString())
            .ToArray();
        Assert.Contains("/api/reviews/pending", endpoints);
        Assert.Contains("/api/reviews/context-observations?feedbackType=stale", endpoints);
        Assert.Contains("/api/reviews/context-observations?feedbackType=wrong", endpoints);
        Assert.Contains("/api/reviews/context-observations?feedbackType=sensitive", endpoints);
        Assert.Contains("/api/reviews/context-observations?feedbackType=over_broad", endpoints);
        Assert.Contains("/api/reviews/context-observations?feedbackType=missing", endpoints);
        Assert.Contains("/api/admin/memory/facts", endpoints);
        Assert.Contains("/api/operations/summary", endpoints);
    }

    private static async Task<ScriptResult> RunScriptAsync(string root, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "bash",
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment.Remove("MEMORYSYSTEM_API_KEY");
        startInfo.Environment.Remove("MEMORYSYSTEM_OPERATOR_API_KEY");

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start weekly admin review workflow.");

        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return new ScriptResult(
            process.ExitCode,
            await standardOutput,
            await standardError);
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

    private sealed record ScriptResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);
}
