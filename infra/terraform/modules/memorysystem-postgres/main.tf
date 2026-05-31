locals {
  name_prefix = lower(replace("${var.service_name}-${var.environment_name}", "_", "-"))

  required_extensions = [
    "vector"
  ]

  endpoint_contract = {
    host       = aws_db_instance.postgres.address
    port       = aws_db_instance.postgres.port
    database   = aws_db_instance.postgres.db_name
    identifier = aws_db_instance.postgres.identifier
  }

  managed_master_user_secret_arn = try(aws_db_instance.postgres.master_user_secret[0].secret_arn, null)

  connection_secret_reference = coalesce(
    var.connection_secret_arn,
    local.managed_master_user_secret_arn
  )

  pgvector_validation = {
    extension_name = "vector"
    migration_file = "migrations/001_initial_memory_schema.sql"
    migration_sql  = "CREATE EXTENSION IF NOT EXISTS vector;"
    check_sql      = "SELECT extname, extversion FROM pg_extension WHERE extname = 'vector';"
  }

  platform_contract = {
    service_name                   = var.service_name
    environment_name               = var.environment_name
    vpc_id                         = var.vpc_id
    private_subnet_ids             = var.private_subnet_ids
    engine                         = "postgres"
    postgres_engine_version        = var.postgres_engine_version
    instance_class                 = var.instance_class
    database_name                  = var.database_name
    database_port                  = var.database_port
    required_extensions            = local.required_extensions
    pgvector_validation            = local.pgvector_validation
    db_instance_identifier         = aws_db_instance.postgres.identifier
    db_subnet_group_name           = aws_db_subnet_group.postgres.name
    security_group_id              = aws_security_group.postgres.id
    backup_retention_days          = var.backup_retention_days
    point_in_time_recovery_enabled = var.backup_retention_days > 0
    backup_window                  = var.backup_window
    maintenance_window             = var.maintenance_window
    allocated_storage_gib          = var.allocated_storage_gib
    max_allocated_storage_gib      = var.max_allocated_storage_gib
    storage_type                   = var.storage_type
    storage_encrypted              = var.storage_encrypted
    multi_az                       = var.multi_az
    publicly_accessible            = var.publicly_accessible
    deletion_protection            = var.deletion_protection
    skip_final_snapshot            = var.skip_final_snapshot
    final_snapshot_identifier      = var.skip_final_snapshot ? null : coalesce(var.final_snapshot_identifier, "${local.name_prefix}-postgres-final")
    iam_database_authentication    = var.iam_database_authentication_enabled
    connection_secret_arn          = local.connection_secret_reference
    rds_managed_master_secret_arn  = local.managed_master_user_secret_arn
    restore_validation_database    = var.restore_validation_database
    restore_validation_secret_arn  = var.restore_validation_secret_arn
  }
}

resource "aws_db_subnet_group" "postgres" {
  name        = "${local.name_prefix}-postgres"
  description = "Private subnet group for ${local.name_prefix} PostgreSQL."
  subnet_ids  = var.private_subnet_ids

  tags = {
    Name = "${local.name_prefix}-postgres"
  }
}

resource "aws_security_group" "postgres" {
  name_prefix = "${local.name_prefix}-postgres-"
  description = "PostgreSQL access for ${local.name_prefix}."
  vpc_id      = var.vpc_id

  tags = {
    Name = "${local.name_prefix}-postgres"
  }
}

resource "aws_vpc_security_group_ingress_rule" "client_security_groups" {
  for_each = toset(var.database_client_security_group_ids)

  security_group_id            = aws_security_group.postgres.id
  referenced_security_group_id = each.value
  ip_protocol                  = "tcp"
  from_port                    = var.database_port
  to_port                      = var.database_port
  description                  = "PostgreSQL from MemorySystem runtime security group."
}

resource "aws_vpc_security_group_ingress_rule" "client_cidr_blocks" {
  for_each = toset(var.database_client_cidr_blocks)

  security_group_id = aws_security_group.postgres.id
  cidr_ipv4         = each.value
  ip_protocol       = "tcp"
  from_port         = var.database_port
  to_port           = var.database_port
  description       = "PostgreSQL from approved client CIDR."
}

resource "aws_vpc_security_group_egress_rule" "all_egress" {
  security_group_id = aws_security_group.postgres.id
  cidr_ipv4         = "0.0.0.0/0"
  ip_protocol       = "-1"
  description       = "Allow managed RDS control-plane egress."
}

resource "aws_db_instance" "postgres" {
  identifier = "${local.name_prefix}-postgres"

  engine         = "postgres"
  engine_version = var.postgres_engine_version
  instance_class = var.instance_class

  db_name  = var.database_name
  username = var.master_username
  port     = var.database_port

  manage_master_user_password   = true
  master_user_secret_kms_key_id = var.master_user_secret_kms_key_id

  allocated_storage     = var.allocated_storage_gib
  max_allocated_storage = var.max_allocated_storage_gib
  storage_type          = var.storage_type
  storage_encrypted     = var.storage_encrypted
  kms_key_id            = var.storage_kms_key_id

  db_subnet_group_name   = aws_db_subnet_group.postgres.name
  vpc_security_group_ids = [aws_security_group.postgres.id]
  publicly_accessible    = var.publicly_accessible

  backup_retention_period = var.backup_retention_days
  backup_window           = var.backup_window
  maintenance_window      = var.maintenance_window
  copy_tags_to_snapshot   = true

  deletion_protection       = var.deletion_protection
  skip_final_snapshot       = var.skip_final_snapshot
  final_snapshot_identifier = var.skip_final_snapshot ? null : coalesce(var.final_snapshot_identifier, "${local.name_prefix}-postgres-final")

  auto_minor_version_upgrade  = var.auto_minor_version_upgrade
  allow_major_version_upgrade = var.allow_major_version_upgrade
  apply_immediately           = var.apply_immediately
  multi_az                    = var.multi_az

  performance_insights_enabled        = var.performance_insights_enabled
  enabled_cloudwatch_logs_exports     = var.enabled_cloudwatch_logs_exports
  iam_database_authentication_enabled = var.iam_database_authentication_enabled
  ca_cert_identifier                  = var.ca_cert_identifier

  tags = {
    Name = "${local.name_prefix}-postgres"
  }
}
