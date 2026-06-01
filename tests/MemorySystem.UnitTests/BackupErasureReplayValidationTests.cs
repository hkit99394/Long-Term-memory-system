namespace MemorySystem.UnitTests;

public sealed class BackupErasureReplayValidationTests
{
    [Fact]
    public void Gc03_backup_erasure_replay_validation_is_documented_scripted_and_next_slice_is_gc04()
    {
        var root = FindRepositoryRoot();
        var contract = File.ReadAllText(Path.Combine(root, "docs", "backup-erasure-replay-validation-gc03.md"));
        var backupRunbook = File.ReadAllText(Path.Combine(root, "docs", "backup-restore.md"));
        var backlog = File.ReadAllText(Path.Combine(root, "docs", "backlog.md"));
        var index = File.ReadAllText(Path.Combine(root, "docs", "README.md"));
        var folderStructure = File.ReadAllText(Path.Combine(root, "docs", "folder-structure.md"));
        var productPlan = File.ReadAllText(Path.Combine(root, "docs", "product-improvement-plan.md"));
        var governancePlan = File.ReadAllText(Path.Combine(root, "docs", "governance-compliance-gate-lr06.md"));
        var ledgerScript = File.ReadAllText(Path.Combine(root, "scripts", "platform-erasure-replay-ledger-export.sh"));
        var restoreScript = File.ReadAllText(Path.Combine(root, "scripts", "platform-restore-validation.sh"));
        var externalMetrics = File.ReadAllText(Path.Combine(root, "observability", "alert-inputs", "external-pilot-metrics.txt"));

        Assert.Contains("# GC-03 Backup Erasure Replay Validation", contract, StringComparison.Ordinal);
        Assert.Contains("platform-erasure-replay-ledger-export.sh", contract, StringComparison.Ordinal);
        Assert.Contains("MEMORYSYSTEM_RESTORE_VALIDATION_REQUIRE_ERASURE_REPLAY=true", contract, StringComparison.Ordinal);
        Assert.Contains("erasureReplay", contract, StringComparison.Ordinal);
        Assert.Contains("payloadSafe", contract, StringComparison.Ordinal);
        Assert.Contains("memorysystem_restore_erasure_replay_validation_success", contract, StringComparison.Ordinal);

        Assert.Contains("platform-erasure-replay-ledger-export.sh", backupRunbook, StringComparison.Ordinal);
        Assert.Contains("MEMORYSYSTEM_BACKUP_CREATED_AT_UTC", backupRunbook, StringComparison.Ordinal);
        Assert.Contains("MEMORYSYSTEM_ERASURE_REPLAY_LEDGER_FILE", backupRunbook, StringComparison.Ordinal);

        Assert.Contains("| GC-03 | P0 | Done | Add backup erasure replay validation.", backlog, StringComparison.Ordinal);
        Assert.Contains("| GC-04 | P0 | Done | Implement standard and audit retention minimization.", backlog, StringComparison.Ordinal);
        Assert.Contains("[Backup Erasure Replay Validation GC-03](backup-erasure-replay-validation-gc03.md)", index, StringComparison.Ordinal);
        Assert.Contains("backup-erasure-replay-validation-gc03.md", folderStructure, StringComparison.Ordinal);
        Assert.Contains("The next move should be `GC-06`", productPlan, StringComparison.Ordinal);
        Assert.Contains("product planning points to `GC-06`", governancePlan, StringComparison.Ordinal);

        Assert.Contains("\"kind\": \"memorysystem.erasure_replay_ledger_export\"", ledgerScript, StringComparison.Ordinal);
        Assert.Contains("COPY (", ledgerScript, StringComparison.Ordinal);
        Assert.Contains("FROM memory_redactions AS redaction", ledgerScript, StringComparison.Ordinal);
        Assert.Contains("WHERE redaction.target_type IN ('event', 'memory_fact')", ledgerScript, StringComparison.Ordinal);
        Assert.DoesNotContain("redaction.reason", ledgerScript, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("MEMORYSYSTEM_BACKUP_CREATED_AT_UTC", restoreScript, StringComparison.Ordinal);
        Assert.Contains("MEMORYSYSTEM_ERASURE_REPLAY_LEDGER_FILE", restoreScript, StringComparison.Ordinal);
        Assert.Contains("MEMORYSYSTEM_RESTORE_VALIDATION_REQUIRE_ERASURE_REPLAY", restoreScript, StringComparison.Ordinal);
        Assert.Contains("\\copy erasure_replay_ledger_raw", restoreScript, StringComparison.Ordinal);
        Assert.Contains("UPDATE events", restoreScript, StringComparison.Ordinal);
        Assert.Contains("UPDATE memory_facts", restoreScript, StringComparison.Ordinal);
        Assert.Contains("UPDATE memory_chunks", restoreScript, StringComparison.Ordinal);
        Assert.Contains("DELETE FROM memory_embeddings", restoreScript, StringComparison.Ordinal);
        Assert.Contains("UPDATE memory_reviews", restoreScript, StringComparison.Ordinal);
        Assert.Contains("UPDATE vault_exports", restoreScript, StringComparison.Ordinal);
        Assert.Contains("\"erasureReplay\"", restoreScript, StringComparison.Ordinal);
        Assert.Contains("memorysystem_restore_erasure_replay_validation_success", restoreScript, StringComparison.Ordinal);

        Assert.Contains("memorysystem_erasure_replay_ledger_export_success", externalMetrics, StringComparison.Ordinal);
        Assert.Contains("memorysystem_restore_erasure_replay_validation_success", externalMetrics, StringComparison.Ordinal);
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
