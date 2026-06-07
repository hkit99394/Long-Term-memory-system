using System.Diagnostics;
using System.Text.Json;

namespace MemorySystem.UnitTests;

public sealed class BacklogRoadmapMemorySyncIp12Tests
{
    [Fact]
    public void Ip12_contract_is_documented_seeded_and_wired()
    {
        var root = FindRepositoryRoot();
        var productPlan = File.ReadAllText(Path.Combine(root, "docs", "product-improvement-plan.md"));
        var contract = File.ReadAllText(Path.Combine(root, "docs", "backlog-roadmap-memory-sync-ip12.md"));
        var docsIndex = File.ReadAllText(Path.Combine(root, "docs", "README.md"));
        var folderStructure = File.ReadAllText(Path.Combine(root, "docs", "folder-structure.md"));
        var runbook = File.ReadAllText(Path.Combine(root, "docs", "project-memory-runbook.md"));
        var boundary = File.ReadAllText(Path.Combine(root, "docs", "project-memory-boundary.md"));
        var testing = File.ReadAllText(Path.Combine(root, "docs", "testing.md"));
        var policy = File.ReadAllText(Path.Combine(root, "docs", "memory-vs-markdown-policy.md"));
        var script = File.ReadAllText(Path.Combine(root, "scripts", "backlog-roadmap-memory-sync.sh"));
        var seed = File.ReadAllText(Path.Combine(root, "scripts", "seed-production-knowledge-base.sh"));

        Assert.Contains("| IP-12 | P1 | Done | Product Owner | Backlog And Roadmap Memory Sync |", productPlan, StringComparison.Ordinal);
        Assert.Contains("# Backlog And Roadmap Memory Sync IP-12", contract, StringComparison.Ordinal);
        Assert.Contains("[Backlog And Roadmap Memory Sync IP-12](backlog-roadmap-memory-sync-ip12.md)", docsIndex, StringComparison.Ordinal);
        Assert.Contains("docs/backlog-roadmap-memory-sync-ip12.md", folderStructure, StringComparison.Ordinal);
        Assert.Contains("scripts/backlog-roadmap-memory-sync.sh", runbook, StringComparison.Ordinal);
        Assert.Contains("scripts/backlog-roadmap-memory-sync.sh", boundary, StringComparison.Ordinal);
        Assert.Contains("bash -n scripts/backlog-roadmap-memory-sync.sh", testing, StringComparison.Ordinal);
        Assert.Contains("scripts/backlog-roadmap-memory-sync.sh --dry-run", policy, StringComparison.Ordinal);

        foreach (var required in new[]
        {
            "docs/roadmap.md",
            "docs/backlog.md",
            "sourceSha256",
            "curated excerpts"
        })
        {
            Assert.Contains(required, contract, StringComparison.Ordinal);
            Assert.Contains(required, script, StringComparison.Ordinal);
        }

        foreach (var required in new[]
        {
            "roadmap current state",
            "external pilot readiness gates",
            "context productization backlog",
            "next backlog focus"
        })
        {
            Assert.Contains(required, contract, StringComparison.Ordinal);
            Assert.Contains(required, seed, StringComparison.Ordinal);
        }

        Assert.Contains("\"roadmap\": {", seed, StringComparison.Ordinal);
        Assert.Contains("\"backlog\": {", seed, StringComparison.Ordinal);
        Assert.Contains("\"subject\": \"roadmap current state\"", seed, StringComparison.Ordinal);
        Assert.Contains("\"subject\": \"external pilot readiness gates\"", seed, StringComparison.Ordinal);
        Assert.Contains("\"subject\": \"context productization backlog\"", seed, StringComparison.Ordinal);
        Assert.Contains("\"subject\": \"next backlog focus\"", seed, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Backlog_roadmap_memory_sync_dry_run_is_payload_safe_and_clear()
    {
        var root = FindRepositoryRoot();

        var syntax = await RunScriptAsync(root, ["-n", "scripts/backlog-roadmap-memory-sync.sh"]);
        Assert.Equal(0, syntax.ExitCode);

        var help = await RunScriptAsync(root, ["scripts/backlog-roadmap-memory-sync.sh", "--help"]);
        Assert.Equal(0, help.ExitCode);
        Assert.Contains("payload-safe roadmap/backlog memory sync", help.StandardOutput, StringComparison.Ordinal);

        var dryRun = await RunScriptAsync(root, ["scripts/backlog-roadmap-memory-sync.sh", "--dry-run"]);
        Assert.Equal(0, dryRun.ExitCode);

        using var dryRunDocument = JsonDocument.Parse(dryRun.StandardOutput);
        var dryRunRoot = dryRunDocument.RootElement;

        Assert.Equal("dry_run", dryRunRoot.GetProperty("status").GetString());
        Assert.Equal("clear", dryRunRoot.GetProperty("syncStatus").GetString());
        Assert.True(dryRunRoot.GetProperty("payloadSafe").GetBoolean());
        Assert.False(dryRunRoot.GetProperty("rawSourcePayloadsIncluded").GetBoolean());
        Assert.Equal("scripts/seed-production-knowledge-base.sh", dryRunRoot.GetProperty("seedScript").GetString());
        Assert.Equal("scripts/source-backed-memory-hygiene.sh", dryRunRoot.GetProperty("commands").GetProperty("hygiene").GetString());
        Assert.Equal(
            "MEMORYSYSTEM_KNOWLEDGE_SEED_DRY_RUN=true scripts/seed-production-knowledge-base.sh",
            dryRunRoot.GetProperty("commands").GetProperty("seedDryRun").GetString());

        var canonicalSources = dryRunRoot.GetProperty("canonicalSources").EnumerateArray()
            .Select(element => element.GetString())
            .ToArray();
        Assert.Contains("docs/roadmap.md", canonicalSources);
        Assert.Contains("docs/backlog.md", canonicalSources);

        var requiredDocuments = dryRunRoot.GetProperty("requiredSeedDocuments").EnumerateArray().ToArray();
        Assert.Equal(2, requiredDocuments.Length);

        var roadmap = requiredDocuments.Single(document => document.GetProperty("key").GetString() == "roadmap");
        Assert.Equal("docs/roadmap.md", roadmap.GetProperty("path").GetString());
        Assert.True(roadmap.GetProperty("hashMatches").GetBoolean());
        Assert.Equal(0, roadmap.GetProperty("staleExcerptCount").GetInt32());
        Assert.True(roadmap.GetProperty("memoryItemCount").GetInt32() >= 2);
        Assert.Contains(
            "roadmap current state",
            roadmap.GetProperty("subjects").EnumerateArray().Select(element => element.GetString()).ToArray());

        var backlog = requiredDocuments.Single(document => document.GetProperty("key").GetString() == "backlog");
        Assert.Equal("docs/backlog.md", backlog.GetProperty("path").GetString());
        Assert.True(backlog.GetProperty("hashMatches").GetBoolean());
        Assert.Equal(0, backlog.GetProperty("staleExcerptCount").GetInt32());
        Assert.True(backlog.GetProperty("memoryItemCount").GetInt32() >= 3);
        Assert.Contains(
            "next backlog focus",
            backlog.GetProperty("subjects").EnumerateArray().Select(element => element.GetString()).ToArray());

        Assert.Empty(dryRunRoot.GetProperty("errors").EnumerateArray());

        var check = await RunScriptAsync(root, ["scripts/backlog-roadmap-memory-sync.sh"]);
        Assert.Equal(0, check.ExitCode);
        using var checkDocument = JsonDocument.Parse(check.StandardOutput);
        Assert.Equal("clear", checkDocument.RootElement.GetProperty("status").GetString());
        Assert.Equal("clear", checkDocument.RootElement.GetProperty("syncStatus").GetString());
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
            ?? throw new InvalidOperationException("Could not start backlog roadmap memory sync.");

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
