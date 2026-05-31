namespace MemorySystem.UnitTests;

public sealed partial class ProductionPlatformTerraformSkeletonTests
{
    [Fact]
    public void Terraform_postgres_module_provisions_managed_rds_with_pgvector_contract()
    {
        var root = FindRepositoryRoot();
        var moduleRoot = Path.Combine(root, "infra", "terraform", "modules", "memorysystem-postgres");

        var main = File.ReadAllText(Path.Combine(moduleRoot, "main.tf"));
        var variables = File.ReadAllText(Path.Combine(moduleRoot, "variables.tf"));
        var outputs = File.ReadAllText(Path.Combine(moduleRoot, "outputs.tf"));
        var firstMigration = File.ReadAllText(Path.Combine(root, "migrations", "001_initial_memory_schema.sql"));

        Assert.Contains("resource \"aws_db_instance\" \"postgres\"", main, StringComparison.Ordinal);
        Assert.Contains("resource \"aws_db_subnet_group\" \"postgres\"", main, StringComparison.Ordinal);
        Assert.Contains("resource \"aws_security_group\" \"postgres\"", main, StringComparison.Ordinal);
        Assert.Contains("resource \"aws_vpc_security_group_ingress_rule\" \"client_security_groups\"", main, StringComparison.Ordinal);
        Assert.Contains("resource \"aws_vpc_security_group_ingress_rule\" \"client_cidr_blocks\"", main, StringComparison.Ordinal);
        Assert.Contains("manage_master_user_password   = true", main, StringComparison.Ordinal);
        Assert.Contains("storage_encrypted     = var.storage_encrypted", main, StringComparison.Ordinal);
        Assert.Contains("backup_retention_period = var.backup_retention_days", main, StringComparison.Ordinal);
        Assert.Contains("point_in_time_recovery_enabled = var.backup_retention_days > 0", main, StringComparison.Ordinal);
        Assert.Contains("deletion_protection       = var.deletion_protection", main, StringComparison.Ordinal);
        Assert.Contains("final_snapshot_identifier = var.skip_final_snapshot ? null", main, StringComparison.Ordinal);
        Assert.Contains("publicly_accessible    = var.publicly_accessible", main, StringComparison.Ordinal);
        Assert.Contains("enabled_cloudwatch_logs_exports", main, StringComparison.Ordinal);
        Assert.Contains("iam_database_authentication_enabled", main, StringComparison.Ordinal);

        Assert.Contains("CREATE EXTENSION IF NOT EXISTS vector;", main, StringComparison.Ordinal);
        Assert.Contains("SELECT extname, extversion FROM pg_extension WHERE extname = 'vector';", main, StringComparison.Ordinal);
        Assert.Contains("CREATE EXTENSION IF NOT EXISTS vector;", firstMigration, StringComparison.Ordinal);

        Assert.Contains("variable \"connection_secret_arn\"", variables, StringComparison.Ordinal);
        Assert.Contains("default     = null", variables, StringComparison.Ordinal);
        Assert.Contains("variable \"database_client_security_group_ids\"", variables, StringComparison.Ordinal);
        Assert.Contains("variable \"database_client_cidr_blocks\"", variables, StringComparison.Ordinal);
        Assert.Contains("output \"managed_master_user_secret_reference\"", outputs, StringComparison.Ordinal);
    }

    [Fact]
    public void Terraform_environment_overlays_wire_postgres_rds_controls()
    {
        var root = FindRepositoryRoot();
        var infraRoot = Path.Combine(root, "infra", "terraform");

        foreach (var environment in new[] { "pilot", "production" })
        {
            var environmentRoot = Path.Combine(infraRoot, "environments", environment);
            var main = File.ReadAllText(Path.Combine(environmentRoot, "main.tf"));
            var variables = File.ReadAllText(Path.Combine(environmentRoot, "variables.tf"));
            var example = File.ReadAllText(Path.Combine(environmentRoot, "terraform.tfvars.example"));

            Assert.Contains("Milestone   = \"PI-03\"", main, StringComparison.Ordinal);
            Assert.Contains("allocated_storage_gib", main, StringComparison.Ordinal);
            Assert.Contains("max_allocated_storage_gib", main, StringComparison.Ordinal);
            Assert.Contains("storage_kms_key_id", main, StringComparison.Ordinal);
            Assert.Contains("master_user_secret_kms_key_id", main, StringComparison.Ordinal);
            Assert.Contains("backup_window", main, StringComparison.Ordinal);
            Assert.Contains("maintenance_window", main, StringComparison.Ordinal);
            Assert.Contains("skip_final_snapshot", main, StringComparison.Ordinal);
            Assert.Contains("multi_az", main, StringComparison.Ordinal);
            Assert.Contains("publicly_accessible", main, StringComparison.Ordinal);
            Assert.Contains("database_client_security_group_ids", main, StringComparison.Ordinal);
            Assert.Contains("database_client_cidr_blocks", main, StringComparison.Ordinal);

            Assert.Contains("variable \"postgres_allocated_storage_gib\"", variables, StringComparison.Ordinal);
            Assert.Contains("variable \"postgres_max_allocated_storage_gib\"", variables, StringComparison.Ordinal);
            Assert.Contains("variable \"postgres_publicly_accessible\"", variables, StringComparison.Ordinal);
            Assert.Contains("variable \"database_client_security_group_ids\"", variables, StringComparison.Ordinal);
            Assert.Contains("variable \"database_client_cidr_blocks\"", variables, StringComparison.Ordinal);

            Assert.Contains("database_client_security_group_ids", example, StringComparison.Ordinal);
            Assert.DoesNotContain("password", example, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Backlog_keeps_pi03_done_after_later_platform_slices()
    {
        var root = FindRepositoryRoot();
        var backlog = File.ReadAllText(Path.Combine(root, "docs", "backlog.md"));

        Assert.Contains("| PI-03 | P0 | Done | Provision Amazon RDS PostgreSQL with pgvector.", backlog, StringComparison.Ordinal);
    }
}
