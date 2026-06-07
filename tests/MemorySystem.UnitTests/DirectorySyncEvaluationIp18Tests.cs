using System.Diagnostics;
using System.Text.Json;

namespace MemorySystem.UnitTests;

public sealed class DirectorySyncEvaluationIp18Tests
{
    [Fact]
    public void Ip18_contract_is_documented_and_wired()
    {
        var root = FindRepositoryRoot();
        var productPlan = File.ReadAllText(Path.Combine(root, "docs", "product-improvement-plan.md"));
        var contract = File.ReadAllText(Path.Combine(root, "docs", "directory-sync-evaluation-ip18.md"));
        var docsIndex = File.ReadAllText(Path.Combine(root, "docs", "README.md"));
        var folderStructure = File.ReadAllText(Path.Combine(root, "docs", "folder-structure.md"));
        var enterpriseGate = File.ReadAllText(Path.Combine(root, "docs", "enterprise-access-gate.md"));
        var testing = File.ReadAllText(Path.Combine(root, "docs", "testing.md"));
        var script = File.ReadAllText(Path.Combine(root, "scripts", "directory-sync-evaluation.sh"));

        Assert.Contains("| IP-18 | P3 | Done | Security Professional + IT/Ops | Directory Sync Evaluation |", productPlan, StringComparison.Ordinal);
        Assert.Contains("# Directory Sync Evaluation IP-18", contract, StringComparison.Ordinal);
        Assert.Contains("[Directory Sync Evaluation IP-18](directory-sync-evaluation-ip18.md)", docsIndex, StringComparison.Ordinal);
        Assert.Contains("docs/directory-sync-evaluation-ip18.md", folderStructure, StringComparison.Ordinal);
        Assert.Contains("scripts/directory-sync-evaluation.sh --dry-run", enterpriseGate, StringComparison.Ordinal);
        Assert.Contains("bash -n scripts/directory-sync-evaluation.sh", testing, StringComparison.Ordinal);

        foreach (var required in new[]
        {
            "provisioning-only",
            "runtime authorization stays local",
            "IMemoryAccessAuthorizer",
            "Access Boundary Review IP-11",
            "Decision 0047",
            "rawDirectoryPayloadsIncluded",
            "defer_directory_sync",
            "reconsider_provisioning_only_directory_sync"
        })
        {
            Assert.Contains(required, contract, StringComparison.Ordinal);
        }

        foreach (var required in new[]
        {
            "provisioning_only",
            "defer_directory_sync",
            "reconsider_provisioning_only_directory_sync",
            "authorize memory reads or writes from token group claims at request time",
            "grant read, write, review, or admin access directly from provider groups"
        })
        {
            Assert.Contains(required, script, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Directory_sync_evaluation_dry_run_preserves_local_authorization_boundary()
    {
        var root = FindRepositoryRoot();

        var syntax = await RunScriptAsync(root, ["-n", "scripts/directory-sync-evaluation.sh"]);
        Assert.Equal(0, syntax.ExitCode);

        var help = await RunScriptAsync(root, ["scripts/directory-sync-evaluation.sh", "--help"]);
        Assert.Equal(0, help.ExitCode);
        Assert.Contains("Evaluate the IP-18 provisioning-only directory sync posture", help.StandardOutput, StringComparison.Ordinal);

        var dryRun = await RunScriptAsync(root, ["scripts/directory-sync-evaluation.sh", "--dry-run"]);
        Assert.Equal(0, dryRun.ExitCode);

        using var document = JsonDocument.Parse(dryRun.StandardOutput);
        var rootElement = document.RootElement;
        Assert.Equal("dry_run", rootElement.GetProperty("status").GetString());
        Assert.True(rootElement.GetProperty("payloadSafe").GetBoolean());
        Assert.False(rootElement.GetProperty("rawDirectoryPayloadsIncluded").GetBoolean());
        Assert.False(rootElement.GetProperty("rawSourcePayloadsIncluded").GetBoolean());
        Assert.Equal("defer_directory_sync", rootElement.GetProperty("recommendation").GetString());
        Assert.Equal("provisioning_only", rootElement.GetProperty("directorySyncBoundary").GetString());
        Assert.Contains(
            "local memberships",
            rootElement.GetProperty("authorizedSourceOfAccess").GetString(),
            StringComparison.Ordinal);

        var policyChecks = rootElement.GetProperty("policyChecks").EnumerateArray().ToArray();
        Assert.All(policyChecks, check => Assert.True(check.GetProperty("present").GetBoolean()));
        Assert.Contains(policyChecks, check => check.GetProperty("id").GetString() == "human_login_stable");
        Assert.Contains(policyChecks, check => check.GetProperty("id").GetString() == "access_boundary_review_stable");
        Assert.Contains(policyChecks, check => check.GetProperty("id").GetString() == "no_runtime_grants");

        var disallowed = rootElement.GetProperty("disallowedFutureSyncBehavior").EnumerateArray()
            .Select(element => element.GetString())
            .ToArray();
        Assert.Contains("authorize memory reads or writes from token group claims at request time", disallowed);
        Assert.Contains("grant read, write, review, or admin access directly from provider groups", disallowed);

        var signalRun = await RunScriptAsync(
            root,
            [
                "scripts/directory-sync-evaluation.sh",
                "--dry-run",
                "--active-human-users", "30",
                "--group-churn", "weekly",
                "--operator-minutes-per-week", "45",
                "--manual-provisioning-errors", "2",
                "--service-accounts", "3"
            ]);
        Assert.Equal(0, signalRun.ExitCode);

        using var signalDocument = JsonDocument.Parse(signalRun.StandardOutput);
        var signalRoot = signalDocument.RootElement;
        Assert.Equal("reconsider_provisioning_only_directory_sync", signalRoot.GetProperty("recommendation").GetString());
        Assert.Contains(
            "user_volume",
            signalRoot.GetProperty("triggeredSignalIds").EnumerateArray().Select(element => element.GetString()).ToArray());
        Assert.Contains(
            "group_churn",
            signalRoot.GetProperty("triggeredSignalIds").EnumerateArray().Select(element => element.GetString()).ToArray());
        Assert.Empty(signalRoot.GetProperty("errors").EnumerateArray());
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
            ?? throw new InvalidOperationException("Could not start directory sync evaluation script.");

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

        throw new InvalidOperationException("Could not locate repository root.");
    }

    private sealed record ScriptResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);
}
