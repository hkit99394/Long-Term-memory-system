using System.Diagnostics;

namespace MemorySystem.UnitTests;

public sealed class AgentMemoryClientWrapperIp08Tests
{
    [Fact]
    public void Ip08_contract_is_documented_and_wired()
    {
        var root = FindRepositoryRoot();
        var productPlan = File.ReadAllText(Path.Combine(root, "docs", "product-improvement-plan.md"));
        var contract = File.ReadAllText(Path.Combine(root, "docs", "agent-memory-client-wrapper-ip08.md"));
        var docsIndex = File.ReadAllText(Path.Combine(root, "docs", "README.md"));
        var apiIndex = File.ReadAllText(Path.Combine(root, "docs", "api", "README.md"));
        var folderStructure = File.ReadAllText(Path.Combine(root, "docs", "folder-structure.md"));
        var testing = File.ReadAllText(Path.Combine(root, "docs", "testing.md"));
        var script = File.ReadAllText(Path.Combine(root, "scripts", "agent-memory-client.sh"));

        Assert.Contains("| IP-08 | P1 | Done | Developer + CTO | Agent Memory Client / Wrapper |", productPlan, StringComparison.Ordinal);
        Assert.Contains("# Agent Memory Client Wrapper IP-08", contract, StringComparison.Ordinal);
        Assert.Contains("[Agent Memory Client Wrapper IP-08](agent-memory-client-wrapper-ip08.md)", docsIndex, StringComparison.Ordinal);
        Assert.Contains("../agent-memory-client-wrapper-ip08.md", apiIndex, StringComparison.Ordinal);
        Assert.Contains("docs/agent-memory-client-wrapper-ip08.md", folderStructure, StringComparison.Ordinal);
        Assert.Contains("bash -n scripts/agent-memory-client.sh", testing, StringComparison.Ordinal);

        Assert.Contains("prework", script, StringComparison.Ordinal);
        Assert.Contains("feedback", script, StringComparison.Ordinal);
        Assert.Contains("status", script, StringComparison.Ordinal);
        Assert.Contains("/api/memory/context?", script, StringComparison.Ordinal);
        Assert.Contains("/api/memory/query-facts", script, StringComparison.Ordinal);
        Assert.Contains("/api/memory/context/feedback", script, StringComparison.Ordinal);
        Assert.Contains("feedbackRequired", script, StringComparison.Ordinal);
        Assert.Contains("queryFactsCount", script, StringComparison.Ordinal);
        Assert.Contains("contextItemRefs", script, StringComparison.Ordinal);
        Assert.Contains("MEMORYSYSTEM_AGENT_MEMORY_STATE_FILE", script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Agent_memory_client_exposes_status_and_blocks_new_prework_until_feedback()
    {
        var root = FindRepositoryRoot();
        var stateFile = Path.Combine(Path.GetTempPath(), $"memorysystem-agent-client-ip08-{Guid.NewGuid():N}.json");

        try
        {
            var syntax = await RunScriptAsync(root, ["-n", "scripts/agent-memory-client.sh"], stateFile);
            Assert.Equal(0, syntax.ExitCode);

            var help = await RunScriptAsync(root, ["scripts/agent-memory-client.sh", "--help"], stateFile);
            Assert.Equal(0, help.ExitCode);
            Assert.Contains("prework", help.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("feedback", help.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("status", help.StandardOutput, StringComparison.Ordinal);

            var status = await RunScriptAsync(root, ["scripts/agent-memory-client.sh", "status"], stateFile);
            Assert.Equal(0, status.ExitCode);
            Assert.Contains("no_pending_feedback", status.StandardOutput, StringComparison.Ordinal);

            await File.WriteAllTextAsync(
                stateFile,
                """
                {
                  "contextItemCount": 0,
                  "contextItemRefs": [],
                  "feedbackRequired": true,
                  "packetId": "11111111-1111-4111-8111-111111111111",
                  "queryFactsCount": 0,
                  "roleId": "developer",
                  "targetScopeId": "9f8e7d6c-5b4a-4321-9123-abcdef123002",
                  "targetScopeType": "project",
                  "version": 1
                }
                """);

            var blocked = await RunScriptAsync(
                root,
                [
                    "scripts/agent-memory-client.sh",
                    "prework",
                    "--query",
                    "IP-08 should block while feedback is pending"
                ],
                stateFile);

            Assert.Equal(2, blocked.ExitCode);
            Assert.Contains("Pending context feedback exists", blocked.StandardError, StringComparison.Ordinal);
            Assert.DoesNotContain("MEMORYSYSTEM_API_KEY", blocked.StandardError, StringComparison.Ordinal);

            File.Delete(stateFile);
            var feedbackWithoutState = await RunScriptAsync(
                root,
                [
                    "scripts/agent-memory-client.sh",
                    "feedback",
                    "--feedback-type",
                    "missing"
                ],
                stateFile);

            Assert.Equal(2, feedbackWithoutState.ExitCode);
            Assert.Contains("No pending prework packet found", feedbackWithoutState.StandardError, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(stateFile);
        }
    }

    private static async Task<ScriptResult> RunScriptAsync(string root, IReadOnlyList<string> arguments, string stateFile)
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

        startInfo.Environment["MEMORYSYSTEM_AGENT_MEMORY_STATE_FILE"] = stateFile;
        startInfo.Environment.Remove("MEMORYSYSTEM_API_KEY");
        startInfo.Environment.Remove("MEMORYSYSTEM_OPERATOR_API_KEY");

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start agent memory client wrapper.");

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
