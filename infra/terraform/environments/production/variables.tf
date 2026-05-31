variable "aws_region" {
  type        = string
  description = "AWS region for the production environment."
  default     = "eu-west-2"
}

variable "image_digest" {
  type        = string
  description = "Immutable ECR image reference, including repository URL and sha256 digest."

  validation {
    condition     = can(regex("@sha256:[a-f0-9]{64}$", var.image_digest))
    error_message = "image_digest must be an immutable OCI image reference ending in @sha256:<64 hex chars>."
  }
}

variable "vpc_id" {
  type        = string
  description = "VPC id for ECS, load balancer, and RDS resources."
}

variable "private_subnet_ids" {
  type        = list(string)
  description = "Private subnet ids for ECS tasks and RDS."
}

variable "public_subnet_ids" {
  type        = list(string)
  description = "Public subnet ids for the API ingress layer."
  default     = []
}

variable "certificate_arn" {
  type        = string
  description = "TLS certificate ARN for API ingress."
  default     = null
}

variable "api_ingress_cidr_blocks" {
  type        = list(string)
  description = "CIDR blocks allowed to reach the API ingress."
  default     = []
}

variable "secret_arns" {
  type        = map(string)
  description = "Secret references by purpose. Values must be ARNs or names, never raw secret values."

  validation {
    condition = alltrue([
      for key in ["authentication", "postgres", "restore_validation", "embeddings"] :
      contains(keys(var.secret_arns), key)
    ])
    error_message = "secret_arns must include authentication, postgres, restore_validation, and embeddings references."
  }
}

variable "database_name" {
  type        = string
  description = "Initial PostgreSQL database name."
  default     = "memory_system"
}

variable "database_port" {
  type        = number
  description = "PostgreSQL port."
  default     = 5432
}

variable "postgres_engine_version" {
  type        = string
  description = "RDS PostgreSQL engine version that supports pgvector."
  default     = "17"
}

variable "postgres_instance_class" {
  type        = string
  description = "Production RDS instance class placeholder."
  default     = "db.t4g.small"
}

variable "postgres_allocated_storage_gib" {
  type        = number
  description = "Initial production RDS storage in GiB."
  default     = 50
}

variable "postgres_max_allocated_storage_gib" {
  type        = number
  description = "Maximum autoscaled production RDS storage in GiB."
  default     = 500
}

variable "postgres_storage_kms_key_id" {
  type        = string
  description = "Optional KMS key id or ARN for production RDS storage encryption."
  default     = null
}

variable "postgres_master_user_secret_kms_key_id" {
  type        = string
  description = "Optional KMS key id or ARN for the production RDS-managed master user secret."
  default     = null
}

variable "backup_retention_days" {
  type        = number
  description = "RDS backup retention window in days."
  default     = 14
}

variable "postgres_backup_window" {
  type        = string
  description = "Preferred production RDS backup window in UTC."
  default     = "02:00-03:00"
}

variable "postgres_maintenance_window" {
  type        = string
  description = "Preferred production RDS maintenance window in UTC."
  default     = "sun:03:00-sun:04:00"
}

variable "deletion_protection" {
  type        = bool
  description = "Whether the production database should use deletion protection."
  default     = true
}

variable "postgres_skip_final_snapshot" {
  type        = bool
  description = "Whether production RDS deletion can skip the final snapshot."
  default     = false
}

variable "postgres_auto_minor_version_upgrade" {
  type        = bool
  description = "Whether production RDS minor PostgreSQL updates can apply automatically."
  default     = true
}

variable "postgres_allow_major_version_upgrade" {
  type        = bool
  description = "Whether production RDS major PostgreSQL updates are allowed."
  default     = false
}

variable "postgres_multi_az" {
  type        = bool
  description = "Whether production RDS Multi-AZ is enabled."
  default     = true
}

variable "postgres_publicly_accessible" {
  type        = bool
  description = "Whether production RDS receives a public endpoint."
  default     = false
}

variable "postgres_apply_immediately" {
  type        = bool
  description = "Whether production RDS changes can apply outside the maintenance window."
  default     = false
}

variable "postgres_performance_insights_enabled" {
  type        = bool
  description = "Whether production RDS Performance Insights is enabled."
  default     = true
}

variable "postgres_enabled_cloudwatch_logs_exports" {
  type        = list(string)
  description = "Production RDS PostgreSQL log exports."
  default     = ["postgresql", "upgrade"]
}

variable "postgres_iam_database_authentication_enabled" {
  type        = bool
  description = "Whether production RDS IAM database authentication is enabled."
  default     = false
}

variable "postgres_ca_cert_identifier" {
  type        = string
  description = "Optional production RDS CA certificate identifier."
  default     = null
}

variable "database_client_security_group_ids" {
  type        = list(string)
  description = "Security group ids allowed to connect to production PostgreSQL."
  default     = []
}

variable "database_client_cidr_blocks" {
  type        = list(string)
  description = "IPv4 CIDR blocks allowed to connect to production PostgreSQL."
  default     = []
}

variable "restore_validation_database" {
  type        = string
  description = "Database name pattern or target used by restore-validation jobs."
  default     = "memory_system_restore_validation"
}

variable "api_container_port" {
  type        = number
  description = "Container port exposed by MemorySystem.Api."
  default     = 8080
}

variable "api_desired_count" {
  type        = number
  description = "Desired API task count."
  default     = 2
}

variable "worker_desired_count" {
  type        = number
  description = "Desired worker task count."
  default     = 1
}

variable "api_task_cpu" {
  type        = number
  description = "API ECS task CPU units."
  default     = 1024
}

variable "api_task_memory" {
  type        = number
  description = "API ECS task memory in MiB."
  default     = 2048
}

variable "worker_task_cpu" {
  type        = number
  description = "Worker ECS task CPU units."
  default     = 1024
}

variable "worker_task_memory" {
  type        = number
  description = "Worker ECS task memory in MiB."
  default     = 2048
}

variable "job_task_cpu" {
  type        = number
  description = "One-shot job ECS task CPU units."
  default     = 1024
}

variable "job_task_memory" {
  type        = number
  description = "One-shot job ECS task memory in MiB."
  default     = 2048
}

variable "log_retention_days" {
  type        = number
  description = "Cloud log retention in days."
  default     = 90
}

variable "release_evidence_bucket" {
  type        = string
  description = "Bucket name or reference for release evidence artifacts."
  default     = null
}

variable "backup_export_schedule_expression" {
  type        = string
  description = "Production backup export schedule expression."
  default     = "rate(6 hours)"
}

variable "alert_route_destinations" {
  type        = map(string)
  description = "Alert route destinations by severity or area."
  default     = {}
}

variable "dashboard_enabled" {
  type        = bool
  description = "Whether dashboard provisioning is enabled for this environment."
  default     = true
}

variable "trace_export_enabled" {
  type        = bool
  description = "Whether runtime trace exporter wiring is enabled."
  default     = false
}

variable "external_metric_names" {
  type        = list(string)
  description = "External platform metric names expected by the observability contract."
  default = [
    "memorysystem_postgres_up",
    "memorysystem_postgres_storage_used_ratio",
    "memorysystem_postgres_connections_used_ratio",
    "memorysystem_postgres_long_running_queries",
    "memorysystem_backup_export_success",
    "memorysystem_backup_age_seconds",
    "memorysystem_backup_export_timestamp_seconds",
    "memorysystem_backup_export_bytes",
    "memorysystem_restore_validation_success",
    "memorysystem_restore_validation_age_seconds",
    "memorysystem_restore_validation_timestamp_seconds",
    "memorysystem_restore_validation_vector_extension_count",
    "memorysystem_restore_validation_table_rows",
    "memorysystem_benchmark_agent_contract_success"
  ]
}
