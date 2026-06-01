namespace MemorySystem.UnitTests;

public sealed partial class ProductionPlatformTerraformSkeletonTests
{
    [Fact]
    public void Terraform_runtime_contract_defines_pi04_backup_and_restore_jobs()
    {
        var root = FindRepositoryRoot();
        var runtimeMain = File.ReadAllText(Path.Combine(root, "infra", "terraform", "modules", "memorysystem-runtime", "main.tf"));
        var runtimeVariables = File.ReadAllText(Path.Combine(root, "infra", "terraform", "modules", "memorysystem-runtime", "variables.tf"));

        Assert.Contains("backup_export = {", runtimeMain, StringComparison.Ordinal);
        Assert.Contains("restore_validation = {", runtimeMain, StringComparison.Ordinal);
        Assert.Contains("target_slice         = \"PI-04\"", runtimeMain, StringComparison.Ordinal);
        Assert.Contains("[\"/app/scripts/platform-backup-export.sh\"]", runtimeMain, StringComparison.Ordinal);
        Assert.Contains("[\"/app/scripts/platform-restore-validation.sh\"]", runtimeMain, StringComparison.Ordinal);
        Assert.Contains("MEMORYSYSTEM_BACKUP_EVIDENCE_FILE", runtimeMain, StringComparison.Ordinal);
        Assert.Contains("MEMORYSYSTEM_RESTORE_VALIDATION_EVIDENCE_FILE", runtimeMain, StringComparison.Ordinal);
        Assert.Contains("memorysystem_backup_export_success", runtimeMain, StringComparison.Ordinal);
        Assert.Contains("memorysystem_backup_age_seconds", runtimeMain, StringComparison.Ordinal);
        Assert.Contains("memorysystem_restore_validation_success", runtimeMain, StringComparison.Ordinal);
        Assert.Contains("memorysystem_restore_validation_table_rows", runtimeMain, StringComparison.Ordinal);
        Assert.Contains("required_secret_refs = [\"postgres\", \"restore_validation\"]", runtimeMain, StringComparison.Ordinal);

        Assert.Contains("variable \"restore_validation_database\"", runtimeVariables, StringComparison.Ordinal);
        Assert.Contains("variable \"backup_export_schedule_expression\"", runtimeVariables, StringComparison.Ordinal);

        foreach (var environment in new[] { "pilot", "production" })
        {
            var main = File.ReadAllText(Path.Combine(root, "infra", "terraform", "environments", environment, "main.tf"));
            var variables = File.ReadAllText(Path.Combine(root, "infra", "terraform", "environments", environment, "variables.tf"));
            var example = File.ReadAllText(Path.Combine(root, "infra", "terraform", "environments", environment, "terraform.tfvars.example"));

            Assert.Contains("restore_validation_database       = var.restore_validation_database", main, StringComparison.Ordinal);
            Assert.Contains("backup_export_schedule_expression = var.backup_export_schedule_expression", main, StringComparison.Ordinal);
            Assert.Contains("variable \"backup_export_schedule_expression\"", variables, StringComparison.Ordinal);
            Assert.Contains("memorysystem_backup_export_success", variables, StringComparison.Ordinal);
            Assert.Contains("memorysystem_restore_validation_table_rows", variables, StringComparison.Ordinal);
            Assert.Contains("backup_export_schedule_expression", example, StringComparison.Ordinal);
            Assert.Contains("release_evidence_bucket", example, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Platform_backup_restore_scripts_emit_evidence_and_alert_metrics()
    {
        var root = FindRepositoryRoot();
        var backupScript = File.ReadAllText(Path.Combine(root, "scripts", "platform-backup-export.sh"));
        var restoreScript = File.ReadAllText(Path.Combine(root, "scripts", "platform-restore-validation.sh"));
        var backupRunbook = File.ReadAllText(Path.Combine(root, "docs", "backup-restore.md"));
        var externalMetrics = File.ReadAllText(Path.Combine(root, "observability", "alert-inputs", "external-pilot-metrics.txt"));

        Assert.Contains("pg_dump", backupScript, StringComparison.Ordinal);
        Assert.Contains("pg_restore --list", backupScript, StringComparison.Ordinal);
        Assert.Contains("\"kind\": \"memorysystem.backup_export\"", backupScript, StringComparison.Ordinal);
        Assert.Contains("memorysystem_backup_export_success", backupScript, StringComparison.Ordinal);
        Assert.Contains("memorysystem_backup_age_seconds", backupScript, StringComparison.Ordinal);

        Assert.Contains("createdb", restoreScript, StringComparison.Ordinal);
        Assert.Contains("pg_restore", restoreScript, StringComparison.Ordinal);
        Assert.Contains("MemorySystem.Migrator", restoreScript, StringComparison.Ordinal);
        Assert.Contains("load_restore_validation_tables", restoreScript, StringComparison.Ordinal);
        Assert.Contains("pg_extension WHERE extname = 'vector'", restoreScript, StringComparison.Ordinal);
        Assert.Contains("\"kind\": \"memorysystem.restore_validation\"", restoreScript, StringComparison.Ordinal);
        Assert.Contains("memorysystem_restore_validation_success", restoreScript, StringComparison.Ordinal);
        Assert.Contains("memorysystem_restore_validation_table_rows", restoreScript, StringComparison.Ordinal);

        Assert.Contains("/app/scripts/platform-backup-export.sh", backupRunbook, StringComparison.Ordinal);
        Assert.Contains("/app/scripts/platform-restore-validation.sh", backupRunbook, StringComparison.Ordinal);
        Assert.Contains("memorysystem_backup_export_success", externalMetrics, StringComparison.Ordinal);
        Assert.Contains("memorysystem_restore_validation_success", externalMetrics, StringComparison.Ordinal);
    }

    [Fact]
    public void Backlog_marks_platform_slices_done_and_names_ea05_next()
    {
        var root = FindRepositoryRoot();
        var backlog = File.ReadAllText(Path.Combine(root, "docs", "backlog.md"));
        var productPlan = File.ReadAllText(Path.Combine(root, "docs", "product-improvement-plan.md"));

        Assert.Contains("| PI-04 | P0 | Done | Add backup exporter and restore validation automation.", backlog, StringComparison.Ordinal);
        Assert.Contains("| PI-05 | P0 | Done | Wire runtime OpenTelemetry exporters.", backlog, StringComparison.Ordinal);
        Assert.Contains("| PI-06 | P1 | Done | Connect alert routing and runbook links.", backlog, StringComparison.Ordinal);
        Assert.Contains("| PI-07 | P1 | Done | Add environment-specific release checklists.", backlog, StringComparison.Ordinal);
        Assert.Contains("| PI-08 | P1 | Done | Run first platform rehearsal.", backlog, StringComparison.Ordinal);
        Assert.Contains("| EA-05 | P0 | Done | Add service-account lifecycle.", backlog, StringComparison.Ordinal);
        Assert.Contains("The next move should be `GC-03`", productPlan, StringComparison.Ordinal);
        Assert.Contains("| DM-06 | P2 | Done | Remove duplicate string normalization helpers.", backlog, StringComparison.Ordinal);
    }
}
