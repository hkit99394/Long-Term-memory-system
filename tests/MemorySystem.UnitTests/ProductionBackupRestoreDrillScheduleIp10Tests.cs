using System.Diagnostics;
using System.Text.Json;

namespace MemorySystem.UnitTests;

public sealed class ProductionBackupRestoreDrillScheduleIp10Tests
{
    [Fact]
    public void Ip10_contract_is_documented_and_wired()
    {
        var root = FindRepositoryRoot();
        var productPlan = File.ReadAllText(Path.Combine(root, "docs", "product-improvement-plan.md"));
        var contract = File.ReadAllText(Path.Combine(root, "docs", "production-backup-restore-drill-schedule-ip10.md"));
        var docsIndex = File.ReadAllText(Path.Combine(root, "docs", "README.md"));
        var folderStructure = File.ReadAllText(Path.Combine(root, "docs", "folder-structure.md"));
        var backupRunbook = File.ReadAllText(Path.Combine(root, "docs", "backup-restore.md"));
        var testing = File.ReadAllText(Path.Combine(root, "docs", "testing.md"));
        var script = File.ReadAllText(Path.Combine(root, "scripts", "backup-restore-drill-schedule.sh"));

        Assert.Contains("| IP-10 | P1 | Done | IT/Ops | Production Backup And Restore Drill Schedule |", productPlan, StringComparison.Ordinal);
        Assert.Contains("# Production Backup And Restore Drill Schedule IP-10", contract, StringComparison.Ordinal);
        Assert.Contains("[Production Backup And Restore Drill Schedule IP-10](production-backup-restore-drill-schedule-ip10.md)", docsIndex, StringComparison.Ordinal);
        Assert.Contains("docs/production-backup-restore-drill-schedule-ip10.md", folderStructure, StringComparison.Ordinal);
        Assert.Contains("scripts/backup-restore-drill-schedule.sh", backupRunbook, StringComparison.Ordinal);
        Assert.Contains("bash -n scripts/backup-restore-drill-schedule.sh", testing, StringComparison.Ordinal);

        foreach (var required in new[]
        {
            "weekly",
            "monthly",
            "quarterly",
            "post-erasure",
            "release-gate",
            "memorysystem_backup_age_seconds",
            "memorysystem_restore_validation_table_rows",
            "memorysystem_restore_erasure_replay_validation_success",
            "MEMORYSYSTEM_RESTORE_VALIDATION_REQUIRE_ERASURE_REPLAY=true"
        })
        {
            Assert.Contains(required, script, StringComparison.Ordinal);
            Assert.Contains(required, contract, StringComparison.Ordinal);
        }

        Assert.Contains("protectedVolumeChecks", script, StringComparison.Ordinal);
        Assert.Contains("exportChecks", script, StringComparison.Ordinal);
        Assert.Contains("Protected Volume And Export Checks", contract, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Backup_restore_drill_schedule_dry_run_is_payload_safe_and_complete()
    {
        var root = FindRepositoryRoot();

        var syntax = await RunScriptAsync(root, ["-n", "scripts/backup-restore-drill-schedule.sh"]);
        Assert.Equal(0, syntax.ExitCode);

        var help = await RunScriptAsync(root, ["scripts/backup-restore-drill-schedule.sh", "--help"]);
        Assert.Equal(0, help.ExitCode);
        Assert.Contains("payload-safe backup and restore drill schedule", help.StandardOutput, StringComparison.Ordinal);

        var dryRun = await RunScriptAsync(
            root,
            [
                "scripts/backup-restore-drill-schedule.sh",
                "--environment",
                "pilot",
                "--drill-type",
                "monthly",
                "--dry-run"
            ]);
        Assert.Equal(0, dryRun.ExitCode);

        using var document = JsonDocument.Parse(dryRun.StandardOutput);
        var rootElement = document.RootElement;

        Assert.Equal("dry_run", rootElement.GetProperty("status").GetString());
        Assert.True(rootElement.GetProperty("payloadSafe").GetBoolean());
        Assert.False(rootElement.GetProperty("rawSourcePayloadsIncluded").GetBoolean());
        Assert.Equal("pilot", rootElement.GetProperty("environment").GetString());
        Assert.Equal("monthly", rootElement.GetProperty("drillType").GetString());
        Assert.Equal("IT/Ops", rootElement.GetProperty("owner").GetString());
        Assert.Contains("24 hours", rootElement.GetProperty("rpo").GetProperty("expectation").GetString(), StringComparison.Ordinal);
        Assert.Contains("4 hours", rootElement.GetProperty("rto").GetProperty("expectation").GetString(), StringComparison.Ordinal);

        var commands = rootElement.GetProperty("commands").EnumerateArray()
            .Select(element => element.GetString())
            .ToArray();
        Assert.Contains("/app/scripts/platform-backup-export.sh", commands);
        Assert.Contains("/app/scripts/platform-erasure-replay-ledger-export.sh", commands);
        Assert.Contains("MEMORYSYSTEM_RESTORE_VALIDATION_REQUIRE_ERASURE_REPLAY=true /app/scripts/platform-restore-validation.sh", commands);

        var requiredEvidence = rootElement.GetProperty("requiredEvidence").EnumerateArray()
            .Select(element => element.GetString())
            .ToArray();
        Assert.Contains("backup export evidence JSON", requiredEvidence);
        Assert.Contains("erasure replay ledger evidence JSON and CSV hash", requiredEvidence);
        Assert.Contains("restore validation evidence JSON", requiredEvidence);

        var metrics = rootElement.GetProperty("metrics").EnumerateArray()
            .Select(element => element.GetString())
            .ToArray();
        Assert.Contains("memorysystem_backup_age_seconds", metrics);
        Assert.Contains("memorysystem_restore_validation_table_rows", metrics);
        Assert.Contains("memorysystem_restore_erasure_replay_validation_success", metrics);

        var protectedVolumeChecks = rootElement.GetProperty("protectedVolumeChecks").EnumerateArray()
            .Select(element => element.GetString())
            .ToArray();
        Assert.Contains(protectedVolumeChecks, check => check is not null && check.Contains("managed PostgreSQL profile", StringComparison.Ordinal));
        Assert.Contains(protectedVolumeChecks, check => check is not null && check.Contains("fresh validation database", StringComparison.Ordinal));

        var exportChecks = rootElement.GetProperty("exportChecks").EnumerateArray()
            .Select(element => element.GetString())
            .ToArray();
        Assert.Contains(exportChecks, check => check is not null && check.Contains("backup export evidence JSON", StringComparison.Ordinal));
        Assert.Contains(exportChecks, check => check is not null && check.Contains("MEMORYSYSTEM_RESTORE_VALIDATION_REQUIRE_ERASURE_REPLAY=true", StringComparison.Ordinal));

        var passCriteria = rootElement.GetProperty("passCriteria").EnumerateArray()
            .Select(element => element.GetString())
            .ToArray();
        Assert.Contains("Restore validation succeeds against a fresh database.", passCriteria);
        Assert.Contains("Observed RPO and RTO are inside the environment expectations.", passCriteria);
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
        startInfo.Environment.Remove("MEMORYSYSTEM_POSTGRES_CONNECTION_STRING");
        startInfo.Environment.Remove("MEMORYSYSTEM_POSTGRES_URL");

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start backup restore drill schedule.");

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
