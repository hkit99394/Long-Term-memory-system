using System.Diagnostics;
using System.Text.Json;

namespace MemorySystem.UnitTests;

public sealed class RoleLensFirstContentPassIp13Tests
{
    [Fact]
    public void Ip13_contract_is_documented_and_wired()
    {
        var root = FindRepositoryRoot();
        var productPlan = File.ReadAllText(Path.Combine(root, "docs", "product-improvement-plan.md"));
        var contract = File.ReadAllText(Path.Combine(root, "docs", "role-lens-first-content-pass-ip13.md"));
        var docsIndex = File.ReadAllText(Path.Combine(root, "docs", "README.md"));
        var folderStructure = File.ReadAllText(Path.Combine(root, "docs", "folder-structure.md"));
        var runbook = File.ReadAllText(Path.Combine(root, "docs", "project-memory-runbook.md"));
        var boundary = File.ReadAllText(Path.Combine(root, "docs", "project-memory-boundary.md"));
        var testing = File.ReadAllText(Path.Combine(root, "docs", "testing.md"));
        var script = File.ReadAllText(Path.Combine(root, "scripts", "role-lens-first-pass.sh"));

        Assert.Contains("| IP-13 | P1 | Done | All Role Owners | Role Lens First Content Pass |", productPlan, StringComparison.Ordinal);
        Assert.Contains("# Role Lens First Content Pass IP-13", contract, StringComparison.Ordinal);
        Assert.Contains("[Role Lens First Content Pass IP-13](role-lens-first-content-pass-ip13.md)", docsIndex, StringComparison.Ordinal);
        Assert.Contains("docs/role-lens-first-content-pass-ip13.md", folderStructure, StringComparison.Ordinal);
        Assert.Contains("scripts/role-lens-first-pass.sh", runbook, StringComparison.Ordinal);
        Assert.Contains("scripts/role-lens-first-pass.sh", boundary, StringComparison.Ordinal);
        Assert.Contains("bash -n scripts/role-lens-first-pass.sh", testing, StringComparison.Ordinal);

        foreach (var required in new[]
        {
            "/api/admin/memory/facts",
            "/api/events",
            "/api/memory/proposals",
            "role_lens",
            "roleId",
            "baseMemoryFactId",
            "sourceContentSha256",
            "sourceEventId"
        })
        {
            Assert.Contains(required, script, StringComparison.Ordinal);
        }

        foreach (var roleId in ExpectedRoleIds)
        {
            Assert.Contains(roleId, contract, StringComparison.Ordinal);
            Assert.Contains(roleId, script, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Role_lens_first_pass_dry_run_is_payload_safe_and_complete()
    {
        var root = FindRepositoryRoot();

        var syntax = await RunScriptAsync(root, ["-n", "scripts/role-lens-first-pass.sh"]);
        Assert.Equal(0, syntax.ExitCode);

        var help = await RunScriptAsync(root, ["scripts/role-lens-first-pass.sh", "--help"]);
        Assert.Equal(0, help.ExitCode);
        Assert.Contains("Seed the IP-13 first content pass for project role lenses", help.StandardOutput, StringComparison.Ordinal);

        var dryRun = await RunScriptAsync(root, ["scripts/role-lens-first-pass.sh", "--dry-run"]);
        Assert.Equal(0, dryRun.ExitCode);

        using var document = JsonDocument.Parse(dryRun.StandardOutput);
        var rootElement = document.RootElement;

        Assert.Equal("dry_run", rootElement.GetProperty("status").GetString());
        Assert.True(rootElement.GetProperty("payloadSafe").GetBoolean());
        Assert.False(rootElement.GetProperty("rawSourcePayloadsIncluded").GetBoolean());
        Assert.Equal("project", rootElement.GetProperty("targetScopeType").GetString());
        Assert.Equal(
            "9f8e7d6c-5b4a-4321-9123-abcdef123002",
            rootElement.GetProperty("targetScopeId").GetString());
        Assert.Equal("role_lens", rootElement.GetProperty("proposalMemoryType").GetString());
        Assert.Equal(8, rootElement.GetProperty("lensCount").GetInt32());

        var endpoints = rootElement.GetProperty("endpoints").EnumerateArray()
            .Select(element => element.GetString())
            .ToArray();
        Assert.Contains("/api/admin/memory/facts", endpoints);
        Assert.Contains("/api/events", endpoints);
        Assert.Contains("/api/memory/proposals", endpoints);

        var sourceFiles = rootElement.GetProperty("sourceFiles").EnumerateArray().ToArray();
        Assert.Equal(3, sourceFiles.Length);
        Assert.All(sourceFiles, source =>
        {
            Assert.StartsWith("docs/", source.GetProperty("path").GetString(), StringComparison.Ordinal);
            Assert.Equal(64, source.GetProperty("sha256").GetString()!.Length);
            Assert.True(source.GetProperty("excerptCount").GetInt32() >= 2);
        });

        var planned = rootElement.GetProperty("plannedLenses").EnumerateArray().ToArray();
        Assert.Equal(ExpectedRoleIds.Length, planned.Length);

        foreach (var roleId in ExpectedRoleIds)
        {
            var lens = planned.Single(element => element.GetProperty("roleId").GetString() == roleId);
            Assert.Equal("role_lens", lens.GetProperty("memoryType").GetString());
            Assert.Equal(
                $"/project/9f8e7d6c-5b4a-4321-9123-abcdef123002/role/{roleId}/lens",
                lens.GetProperty("namespace").GetString());
            Assert.True(lens.GetProperty("requiresBaseMemoryFact").GetBoolean());
            Assert.Equal("owns", lens.GetProperty("basePredicate").GetString());
            Assert.Equal("prioritizes", lens.GetProperty("predicate").GetString());
            Assert.True(lens.GetProperty("confidence").GetDecimal() >= 0.90m);
        }

        var requiredBaseFacts = rootElement.GetProperty("requiredBaseFacts").EnumerateArray().ToArray();
        Assert.Equal(ExpectedRoleIds.Length, requiredBaseFacts.Length);
        Assert.All(requiredBaseFacts, fact =>
        {
            Assert.Equal("fact", fact.GetProperty("memoryType").GetString());
            Assert.Equal(
                "/project/9f8e7d6c-5b4a-4321-9123-abcdef123002/facts",
                fact.GetProperty("namespace").GetString());
            Assert.Equal("owns", fact.GetProperty("predicate").GetString());
        });

        Assert.Equal(
            "scripts/role-lens-first-pass.sh",
            rootElement.GetProperty("commands").GetProperty("live").GetString());
        Assert.Equal(
            "scripts/seed-production-knowledge-base.sh",
            rootElement.GetProperty("commands").GetProperty("sourceSeed").GetString());
    }

    private static readonly string[] ExpectedRoleIds =
    [
        "product_owner",
        "cto",
        "security_professional",
        "it_manager",
        "developer",
        "tester_qa",
        "release_manager",
        "knowledge_steward"
    ];

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
            ?? throw new InvalidOperationException("Could not start role lens first pass script.");

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
