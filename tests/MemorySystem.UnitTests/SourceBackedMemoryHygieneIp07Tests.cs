using System.Diagnostics;

namespace MemorySystem.UnitTests;

public sealed class SourceBackedMemoryHygieneIp07Tests
{
    [Fact]
    public void Ip07_contract_is_documented_and_wired()
    {
        var root = FindRepositoryRoot();
        var productPlan = File.ReadAllText(Path.Combine(root, "docs", "product-improvement-plan.md"));
        var contract = File.ReadAllText(Path.Combine(root, "docs", "source-backed-memory-hygiene-ip07.md"));
        var docsIndex = File.ReadAllText(Path.Combine(root, "docs", "README.md"));
        var folderStructure = File.ReadAllText(Path.Combine(root, "docs", "folder-structure.md"));
        var testing = File.ReadAllText(Path.Combine(root, "docs", "testing.md"));
        var seed = File.ReadAllText(Path.Combine(root, "scripts", "seed-production-knowledge-base.sh"));
        var hygieneScript = File.ReadAllText(Path.Combine(root, "scripts", "source-backed-memory-hygiene.sh"));

        Assert.Contains("| IP-07 | P1 | Done | Knowledge Steward + Tester/QA | Source-Backed Memory Hygiene Automation |", productPlan, StringComparison.Ordinal);
        Assert.Contains("# Source-Backed Memory Hygiene Automation IP-07", contract, StringComparison.Ordinal);
        Assert.Contains("[Source-Backed Memory Hygiene Automation IP-07](source-backed-memory-hygiene-ip07.md)", docsIndex, StringComparison.Ordinal);
        Assert.Contains("docs/source-backed-memory-hygiene-ip07.md", folderStructure, StringComparison.Ordinal);
        Assert.Contains("./scripts/source-backed-memory-hygiene.sh", testing, StringComparison.Ordinal);
        Assert.Contains("bash -n scripts/source-backed-memory-hygiene.sh", testing, StringComparison.Ordinal);

        Assert.Contains("\"sourceSha256\":", seed, StringComparison.Ordinal);
        Assert.Contains("expected_sha256 = document.get(\"sourceSha256\")", seed, StringComparison.Ordinal);
        Assert.Contains("Update curated excerpts and sourceSha256 before writing memory", seed, StringComparison.Ordinal);
        Assert.Contains("\"roleId\"", seed, StringComparison.Ordinal);
        Assert.Contains("\"baseMemoryFactId\"", seed, StringComparison.Ordinal);

        foreach (var check in new[]
        {
            "source_hash_drift",
            "stale_source_links",
            "duplicate_memory_identities",
            "missing_evidence",
            "curated_excerpt_drift",
            "role_lens_source_contract"
        })
        {
            Assert.Contains(check, hygieneScript, StringComparison.Ordinal);
            Assert.Contains(check.Replace('_', ' '), contract, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Source_backed_memory_hygiene_gate_passes_current_seed()
    {
        var root = FindRepositoryRoot();
        var startInfo = new ProcessStartInfo
        {
            FileName = "bash",
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("scripts/source-backed-memory-hygiene.sh");

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start source-backed memory hygiene script.");

        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        var output = await standardOutput;
        var error = await standardError;

        Assert.True(
            process.ExitCode == 0,
            $"Hygiene script failed with exit code {process.ExitCode}.\nSTDOUT:\n{output}\nSTDERR:\n{error}");
        Assert.Contains("Source-backed memory hygiene check passed.", output, StringComparison.Ordinal);
        Assert.Contains("\"errorCount\": 0", output, StringComparison.Ordinal);
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
