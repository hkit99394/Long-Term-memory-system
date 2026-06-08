using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace MemorySystem.UnitTests;

public sealed class TargetEnvironmentEvidenceHardeningTests
{
    [Fact]
    public void Ip04_target_environment_evidence_hardening_is_documented_scripted_and_marked_done_for_uat()
    {
        var root = FindRepositoryRoot();
        var productPlan = File.ReadAllText(Path.Combine(root, "docs", "product-improvement-plan.md"));
        var hardening = File.ReadAllText(Path.Combine(root, "docs", "target-environment-evidence-hardening-ip04.md"));
        var runbook = File.ReadAllText(Path.Combine(root, "docs", "target-environment-pilot-rehearsal-p0.md"));
        var releaseChecklist = File.ReadAllText(Path.Combine(root, "docs", "production-release-checklists-pi07.md"));
        var testing = File.ReadAllText(Path.Combine(root, "docs", "testing.md"));
        var docsIndex = File.ReadAllText(Path.Combine(root, "docs", "README.md"));
        var folderStructure = File.ReadAllText(Path.Combine(root, "docs", "folder-structure.md"));
        var script = File.ReadAllText(Path.Combine(root, "scripts", "target-environment-evidence-verify.sh"));
        var schema = File.ReadAllText(Path.Combine(root, "docs", "target-environment-evidence-manifest.schema.json"));
        var example = File.ReadAllText(Path.Combine(root, "docs", "target-environment-evidence-manifest.example.json"));
        var uatEvidence = File.ReadAllText(Path.Combine(root, "docs", "release-evidence", "uat-layout-fix-2026-06-08", "ip04", "README.md"));
        var uatManifestPath = Path.Combine(root, "docs", "release-evidence", "uat-layout-fix-2026-06-08", "ip04", "target-environment-evidence-manifest.json");
        var uatManifest = File.ReadAllText(uatManifestPath);
        var verifierOutput = File.ReadAllText(Path.Combine(root, "docs", "release-evidence", "uat-layout-fix-2026-06-08", "ip04", "verifier-output.txt"));

        Assert.Contains("| IP-04 | P0 | Done | Release Manager + Tester/QA + Ops | Target-Environment Evidence Hardening |", productPlan, StringComparison.Ordinal);
        Assert.Contains("Status: done for the accepted UAT target environment.", hardening, StringComparison.Ordinal);
        Assert.Contains("memorysystem.target_environment_evidence", hardening, StringComparison.Ordinal);
        Assert.Contains("scripts/target-environment-evidence-verify.sh", hardening, StringComparison.Ordinal);
        Assert.Contains("UAT Target Evidence IP-04", hardening, StringComparison.Ordinal);
        Assert.Contains("releaseId=uat-layout-fix-2026-06-08 environment=uat artifacts=11", verifierOutput, StringComparison.Ordinal);
        Assert.Contains("\"environment\": \"uat\"", uatManifest, StringComparison.Ordinal);
        Assert.Contains("\"payloadSafe\": true", uatManifest, StringComparison.Ordinal);

        foreach (var gate in RequiredGateIds())
        {
            Assert.Contains(gate, hardening, StringComparison.Ordinal);
            Assert.Contains(gate, script, StringComparison.Ordinal);
            Assert.Contains(gate, schema, StringComparison.Ordinal);
            Assert.Contains(gate, example, StringComparison.Ordinal);
            Assert.Contains(gate, uatManifest, StringComparison.Ordinal);
        }

        Assert.Contains("Target-Environment Evidence Hardening IP-04", docsIndex, StringComparison.Ordinal);
        Assert.Contains("UAT Target Evidence IP-04", docsIndex, StringComparison.Ordinal);
        Assert.Contains("Status: verified GO for the accepted UAT target environment.", uatEvidence, StringComparison.Ordinal);
        Assert.Contains("target-environment-evidence-hardening-ip04.md", folderStructure, StringComparison.Ordinal);
        Assert.Contains("target-environment-evidence-manifest.schema.json", folderStructure, StringComparison.Ordinal);
        Assert.Contains("target-environment-evidence-manifest.example.json", folderStructure, StringComparison.Ordinal);
        Assert.Contains("docs/release-evidence/uat-layout-fix-2026-06-08/ip04/", folderStructure, StringComparison.Ordinal);

        Assert.Contains("target-environment-evidence-verify.sh", runbook, StringComparison.Ordinal);
        Assert.Contains("target-environment-evidence-verify.sh", releaseChecklist, StringComparison.Ordinal);
        Assert.Contains("bash -n scripts/target-environment-evidence-verify.sh", testing, StringComparison.Ordinal);

        Assert.Contains("memorysystem.target_environment_evidence", schema, StringComparison.Ordinal);
        Assert.Contains("\"payloadSafe\":", schema, StringComparison.Ordinal);
        Assert.Contains("sha256:<64 hex chars>", hardening, StringComparison.Ordinal);
        Assert.Contains("sha256_file \"$artifact_file\"", script, StringComparison.Ordinal);
        Assert.Contains("payloadSafe true", script, StringComparison.Ordinal);
        Assert.Contains("must be a local file path for hash verification", script, StringComparison.Ordinal);
        Assert.DoesNotContain("cat \"$artifact_file\"", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SELECT event.content", script, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(uatManifestPath));
    }

    [Fact]
    public async Task Target_environment_verifier_rejects_unverified_pgvector()
    {
        var root = FindRepositoryRoot();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"memorysystem-ip04-{Guid.NewGuid():N}");

        try
        {
            var manifestPath = CreateTargetEvidenceManifest(tempRoot, pgvectorVerified: false);

            var result = await RunScriptAsync(
                root,
                ["scripts/target-environment-evidence-verify.sh", manifestPath]);

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("databaseTarget.pgvectorVerified must be true", result.StandardError, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static string[] RequiredGateIds()
    {
        return new[]
        {
            "environment_preflight",
            "terraform_validation",
            "deployment_smoke",
            "metrics_and_tracing",
            "benchmark_scorecards",
            "governance_smoke",
            "backup_restore",
            "alert_receiver_acknowledgement",
            "evidence_upload",
            "rollback_notes",
            "go_no_go"
        };
    }

    private static string CreateTargetEvidenceManifest(string tempRoot, bool pgvectorVerified)
    {
        var evidenceDir = Path.Combine(tempRoot, "evidence");
        Directory.CreateDirectory(evidenceDir);

        var gates = new List<Dictionary<string, object?>>();
        foreach (var gateId in RequiredGateIds())
        {
            var artifactRelativePath = $"evidence/{gateId}.txt";
            var artifactPath = Path.Combine(tempRoot, artifactRelativePath);
            File.WriteAllText(artifactPath, $"{gateId}\n");

            var gate = new Dictionary<string, object?>
            {
                ["id"] = gateId,
                ["status"] = "passed",
                ["completedAtUtc"] = "2026-06-07T00:00:00Z",
                ["summary"] = gateId,
                ["artifacts"] = new[]
                {
                    new Dictionary<string, object?>
                    {
                        ["path"] = artifactRelativePath,
                        ["sha256"] = $"sha256:{Sha256File(artifactPath)}",
                        ["description"] = gateId,
                        ["payloadSafe"] = true
                    }
                }
            };

            if (gateId == "alert_receiver_acknowledgement")
            {
                gate["acknowledgement"] = new Dictionary<string, object?>
                {
                    ["receiver"] = "pilot-page-route",
                    ["acknowledgedBy"] = "on-call-owner",
                    ["acknowledgedAtUtc"] = "2026-06-07T00:00:00Z",
                    ["runbook"] = "docs/production-observability.md"
                };
            }

            if (gateId == "rollback_notes")
            {
                gate["rollback"] = new Dictionary<string, object?>
                {
                    ["previousImageDigest"] = $"sha256:{new string('1', 64)}",
                    ["rollbackBoundary"] = "before first target write",
                    ["communicationRoute"] = "pilot-release-channel"
                };
                gate["signatures"] = new Dictionary<string, object?>
                {
                    ["rollbackOwner"] = "ops-rollback-owner"
                };
            }

            if (gateId == "go_no_go")
            {
                gate["decision"] = new Dictionary<string, object?>
                {
                    ["decision"] = "GO",
                    ["releaseOwnerSignature"] = "release-manager",
                    ["rollbackOwnerSignature"] = "ops-rollback-owner"
                };
            }

            gates.Add(gate);
        }

        var manifest = new Dictionary<string, object?>
        {
            ["schemaVersion"] = 1,
            ["kind"] = "memorysystem.target_environment_evidence",
            ["releaseId"] = "pilot-2026-06-07",
            ["environment"] = "pilot",
            ["targetEnvironment"] = new Dictionary<string, object?>
            {
                ["name"] = "pilot",
                ["account"] = "platform-account",
                ["region"] = "platform-region",
                ["networkBoundary"] = "vpc"
            },
            ["imageDigest"] = $"sha256:{new string('0', 64)}",
            ["databaseTarget"] = new Dictionary<string, object?>
            {
                ["provider"] = "managed-postgresql",
                ["endpointRef"] = "secret-ref",
                ["databaseName"] = "memory_system",
                ["pgvectorVerified"] = pgvectorVerified
            },
            ["evidencePrefix"] = "s3://release-evidence-bucket/memorysystem/pilot-2026-06-07/",
            ["generatedAtUtc"] = "2026-06-07T00:00:00Z",
            ["payloadSafe"] = true,
            ["owners"] = new Dictionary<string, object?>
            {
                ["releaseOwner"] = "release-manager",
                ["rollbackOwner"] = "ops-rollback-owner",
                ["alertRouteOwner"] = "on-call-owner",
                ["evidenceOwner"] = "evidence-custodian",
                ["benchmarkScorer"] = "qa-benchmark-owner",
                ["governanceReviewer"] = "governance-reviewer"
            },
            ["gates"] = gates
        };

        var manifestPath = Path.Combine(tempRoot, "manifest.json");
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
        return manifestPath;
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

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start target evidence verifier.");

        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return new ScriptResult(process.ExitCode, await standardOutput, await standardError);
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

    private sealed record ScriptResult(int ExitCode, string StandardOutput, string StandardError);
}
