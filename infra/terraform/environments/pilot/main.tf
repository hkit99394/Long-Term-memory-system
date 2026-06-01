terraform {
  required_version = ">= 1.6.0"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 6.0"
    }
  }
}

provider "aws" {
  region = var.aws_region

  default_tags {
    tags = local.common_tags
  }
}

locals {
  service_name     = "memorysystem"
  environment_name = "pilot"
  common_tags = {
    Application = local.service_name
    Environment = local.environment_name
    ManagedBy   = "terraform"
    Milestone   = "PI-03"
  }
}

module "postgres" {
  source = "../../modules/memorysystem-postgres"

  service_name                        = local.service_name
  environment_name                    = local.environment_name
  vpc_id                              = var.vpc_id
  private_subnet_ids                  = var.private_subnet_ids
  database_name                       = var.database_name
  database_port                       = var.database_port
  postgres_engine_version             = var.postgres_engine_version
  instance_class                      = var.postgres_instance_class
  allocated_storage_gib               = var.postgres_allocated_storage_gib
  max_allocated_storage_gib           = var.postgres_max_allocated_storage_gib
  storage_kms_key_id                  = var.postgres_storage_kms_key_id
  master_user_secret_kms_key_id       = var.postgres_master_user_secret_kms_key_id
  backup_retention_days               = var.backup_retention_days
  backup_window                       = var.postgres_backup_window
  maintenance_window                  = var.postgres_maintenance_window
  deletion_protection                 = var.deletion_protection
  skip_final_snapshot                 = var.postgres_skip_final_snapshot
  auto_minor_version_upgrade          = var.postgres_auto_minor_version_upgrade
  allow_major_version_upgrade         = var.postgres_allow_major_version_upgrade
  multi_az                            = var.postgres_multi_az
  publicly_accessible                 = var.postgres_publicly_accessible
  apply_immediately                   = var.postgres_apply_immediately
  performance_insights_enabled        = var.postgres_performance_insights_enabled
  enabled_cloudwatch_logs_exports     = var.postgres_enabled_cloudwatch_logs_exports
  iam_database_authentication_enabled = var.postgres_iam_database_authentication_enabled
  ca_cert_identifier                  = var.postgres_ca_cert_identifier
  database_client_security_group_ids  = var.database_client_security_group_ids
  database_client_cidr_blocks         = var.database_client_cidr_blocks
  connection_secret_arn               = var.secret_arns["postgres"]
  restore_validation_database         = var.restore_validation_database
  restore_validation_secret_arn       = var.secret_arns["restore_validation"]
}

module "runtime" {
  source = "../../modules/memorysystem-runtime"

  service_name                      = local.service_name
  environment_name                  = local.environment_name
  aws_region                        = var.aws_region
  image_digest                      = var.image_digest
  vpc_id                            = var.vpc_id
  private_subnet_ids                = var.private_subnet_ids
  public_subnet_ids                 = var.public_subnet_ids
  api_container_port                = var.api_container_port
  api_desired_count                 = var.api_desired_count
  worker_desired_count              = var.worker_desired_count
  api_task_cpu                      = var.api_task_cpu
  api_task_memory                   = var.api_task_memory
  worker_task_cpu                   = var.worker_task_cpu
  worker_task_memory                = var.worker_task_memory
  job_task_cpu                      = var.job_task_cpu
  job_task_memory                   = var.job_task_memory
  certificate_arn                   = var.certificate_arn
  api_ingress_cidr_blocks           = var.api_ingress_cidr_blocks
  postgres_endpoint                 = module.postgres.endpoint_contract
  postgres_secret_arn               = module.postgres.connection_secret_reference
  restore_secret_arn                = module.postgres.restore_validation_secret_reference
  restore_validation_database       = var.restore_validation_database
  application_secret_arns           = var.secret_arns
  log_retention_days                = var.log_retention_days
  release_evidence_bucket           = var.release_evidence_bucket
  backup_export_schedule_expression = var.backup_export_schedule_expression
  alert_route_destinations          = var.alert_route_destinations
  open_telemetry_enabled            = true
  open_telemetry_exporter           = var.trace_export_enabled ? "otlp" : "none"
  open_telemetry_otlp_endpoint      = var.open_telemetry_otlp_endpoint
}

module "observability" {
  source = "../../modules/memorysystem-observability"

  service_name             = local.service_name
  environment_name         = local.environment_name
  aws_region               = var.aws_region
  alert_route_destinations = var.alert_route_destinations
  alert_route_owners       = var.alert_route_owners
  alert_silence_policy     = var.alert_silence_policy
  alert_route_test_enabled = var.alert_route_test_enabled
  dashboard_enabled        = var.dashboard_enabled
  trace_export_enabled     = var.trace_export_enabled
  external_metric_names    = var.external_metric_names
  release_evidence_bucket  = var.release_evidence_bucket
}
