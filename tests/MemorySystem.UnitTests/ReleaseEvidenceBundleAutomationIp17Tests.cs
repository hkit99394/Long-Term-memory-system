using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace MemorySystem.UnitTests;

public sealed class ReleaseEvidenceBundleAutomationIp17Tests
{
    [Fact]
    public void Ip17_contract_is_documented_and_wired()
    {
        var root = FindRepositoryRoot();
        var productPlan = File.ReadAllText(Path.Combine(root, "docs", "product-improvement-plan.md"));
        var contract = File.ReadAllText(Path.Combine(root, "docs", "release-evidence-bundle-automation-ip17.md"));
        var docsIndex = File.ReadAllText(Path.Combine(root, "docs", "README.md"));
        var folderStructure = File.ReadAllText(Path.Combine(root, "docs", "folder-structure.md"));
        var releaseChecklist = File.ReadAllText(Path.Combine(root, "docs", "production-release-checklists-pi07.md"));
        var testing = File.ReadAllText(Path.Combine(root, "docs", "testing.md"));
        var script = File.ReadAllText(Path.Combine(root, "scripts", "release-evidence-bundle.sh"));

        Assert.Contains("| IP-17 | P2 | Done | Release Manager + Ops | Release Evidence Bundle Automation |", productPlan, StringComparison.Ordinal);
        Assert.Contains("# Release Evidence Bundle Automation IP-17", contract, StringComparison.Ordinal);
        Assert.Contains("[Release Evidence Bundle Automation IP-17](release-evidence-bundle-automation-ip17.md)", docsIndex, StringComparison.Ordinal);
        Assert.Contains("docs/release-evidence-bundle-automation-ip17.md", folderStructure, StringComparison.Ordinal);
        Assert.Contains("scripts/release-evidence-bundle.sh", releaseChecklist, StringComparison.Ordinal);
        Assert.Contains("bash -n scripts/release-evidence-bundle.sh", testing, StringComparison.Ordinal);

        foreach (var required in RequiredGates)
        {
            Assert.Contains(required, contract, StringComparison.Ordinal);
            Assert.Contains(required, script, StringComparison.Ordinal);
        }

        foreach (var required in new[]
        {
            "memorysystem.release_evidence_bundle",
            "payloadSafe",
            "rawArtifactPayloadsIncluded",
            "artifactIndexPath",
            "sha256SidecarPath",
            "strict",
            "rollbackOwner"
        })
        {
            Assert.Contains(required, contract, StringComparison.Ordinal);
            Assert.Contains(required, script, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Release_evidence_bundle_strict_mode_generates_payload_safe_outputs()
    {
        var root = FindRepositoryRoot();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"memorysystem-ip17-{Guid.NewGuid():N}");
        var inputDir = Path.Combine(tempRoot, "inputs");
        var outputDir = Path.Combine(tempRoot, "bundle");
        Directory.CreateDirectory(inputDir);

        try
        {
            var artifactPaths = CreateFixtureArtifacts(inputDir);

            var syntax = await RunScriptAsync(root, ["-n", "scripts/release-evidence-bundle.sh"]);
            Assert.Equal(0, syntax.ExitCode);

            var help = await RunScriptAsync(root, ["scripts/release-evidence-bundle.sh", "--help"]);
            Assert.Equal(0, help.ExitCode);
            Assert.Contains("Generate the IP-17 payload-safe release evidence bundle", help.StandardOutput, StringComparison.Ordinal);

            var result = await RunScriptAsync(
                root,
                [
                    "scripts/release-evidence-bundle.sh",
                    "--mode", "strict",
                    "--release-id", "ip17-test",
                    "--environment", "local",
                    "--operator-id", "release-manager",
                    "--rollback-owner", "ops-owner",
                    "--bundle-id", "ip17-test",
                    "--output-dir", outputDir,
                    "--generated-at-utc", "2026-06-07T00:00:00Z",
                    "--tests-report", artifactPaths["tests"],
                    "--migration-status", artifactPaths["migration_status"],
                    "--health-report", artifactPaths["health"],
                    "--operations-summary", artifactPaths["operations_summary"],
                    "--benchmark-report", artifactPaths["benchmark"],
                    "--backup-restore-report", artifactPaths["backup_restore"],
                    "--rollback-report", artifactPaths["rollback"]
                ]);

            Assert.Equal(0, result.ExitCode);

            using var stdoutDocument = JsonDocument.Parse(result.StandardOutput);
            var rootElement = stdoutDocument.RootElement;
            Assert.Equal("memorysystem.release_evidence_bundle", rootElement.GetProperty("kind").GetString());
            Assert.Equal("complete", rootElement.GetProperty("status").GetString());
            Assert.Equal("complete", rootElement.GetProperty("bundleStatus").GetString());
            Assert.Equal("strict", rootElement.GetProperty("mode").GetString());
            Assert.True(rootElement.GetProperty("payloadSafe").GetBoolean());
            Assert.False(rootElement.GetProperty("rawArtifactPayloadsIncluded").GetBoolean());
            Assert.False(rootElement.GetProperty("rawSourcePayloadsIncluded").GetBoolean());
            Assert.Equal("ip17-test", rootElement.GetProperty("release").GetProperty("releaseId").GetString());
            Assert.Equal("ops-owner", rootElement.GetProperty("release").GetProperty("rollbackOwner").GetString());

            var counts = rootElement.GetProperty("counts");
            Assert.Equal(RequiredGates.Length, counts.GetProperty("artifactCount").GetInt32());
            Assert.Equal(RequiredGates.Length, counts.GetProperty("presentArtifacts").GetInt32());
            Assert.Equal(0, counts.GetProperty("missingRequiredArtifacts").GetInt32());
            Assert.Empty(rootElement.GetProperty("errors").EnumerateArray());

            var artifacts = rootElement.GetProperty("artifacts").EnumerateArray().ToArray();
            Assert.Equal(RequiredGates.Length, artifacts.Length);
            foreach (var gate in RequiredGates)
            {
                var artifact = artifacts.Single(element => element.GetProperty("gate").GetString() == gate);
                Assert.Equal("present", artifact.GetProperty("status").GetString());
                Assert.True(artifact.GetProperty("bytes").GetInt32() > 0);
                Assert.Equal(64, artifact.GetProperty("sha256").GetString()!.Length);
            }

            var bundle = rootElement.GetProperty("bundle");
            var manifestPath = bundle.GetProperty("manifestPath").GetString()!;
            var artifactIndexPath = bundle.GetProperty("artifactIndexPath").GetString()!;
            var summaryPath = bundle.GetProperty("summaryPath").GetString()!;
            var sidecarPath = bundle.GetProperty("sha256SidecarPath").GetString()!;

            Assert.True(File.Exists(manifestPath));
            Assert.True(File.Exists(artifactIndexPath));
            Assert.True(File.Exists(summaryPath));
            Assert.True(File.Exists(sidecarPath));

            var manifest = File.ReadAllText(manifestPath);
            Assert.DoesNotContain(SecretFixtureMarker, manifest, StringComparison.Ordinal);
            Assert.DoesNotContain(SecretFixtureMarker, File.ReadAllText(summaryPath), StringComparison.Ordinal);
            Assert.All(File.ReadAllLines(artifactIndexPath), line =>
            {
                using var artifactDocument = JsonDocument.Parse(line);
                Assert.Equal("present", artifactDocument.RootElement.GetProperty("status").GetString());
                Assert.False(line.Contains(SecretFixtureMarker, StringComparison.Ordinal));
            });

            var sidecarParts = File.ReadAllText(sidecarPath)
                .Trim()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(2, sidecarParts.Length);
            Assert.Equal("ip17-test.json", sidecarParts[1]);
            Assert.Equal(Sha256File(manifestPath), sidecarParts[0]);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private const string SecretFixtureMarker = "SECRET_FIXTURE_PAYLOAD_SHOULD_NOT_APPEAR";

    private static readonly string[] RequiredGates =
    [
        "tests",
        "migration_status",
        "health",
        "operations_summary",
        "benchmark",
        "backup_restore",
        "rollback"
    ];

    private static Dictionary<string, string> CreateFixtureArtifacts(string inputDir)
    {
        var artifacts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["tests"] = Path.Combine(inputDir, "tests.json"),
            ["migration_status"] = Path.Combine(inputDir, "migration-status.json"),
            ["health"] = Path.Combine(inputDir, "health.json"),
            ["operations_summary"] = Path.Combine(inputDir, "operations-summary.json"),
            ["benchmark"] = Path.Combine(inputDir, "benchmark.json"),
            ["backup_restore"] = Path.Combine(inputDir, "backup-restore.json"),
            ["rollback"] = Path.Combine(inputDir, "rollback.json")
        };

        foreach (var (gate, path) in artifacts)
        {
            File.WriteAllText(
                path,
                $$"""
                {
                  "gate": "{{gate}}",
                  "status": "passed",
                  "payload": "{{SecretFixtureMarker}}"
                }
                """);
        }

        return artifacts;
    }

    private static string Sha256File(string path)
    {
        return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
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
            ?? throw new InvalidOperationException("Could not start release evidence bundle script.");

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
