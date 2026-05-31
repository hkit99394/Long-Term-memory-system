variable "service_name" {
  type        = string
  description = "Service name used for resource naming."
}

variable "environment_name" {
  type        = string
  description = "Environment name such as pilot or production."
}

variable "vpc_id" {
  type        = string
  description = "VPC id for PostgreSQL networking."
}

variable "private_subnet_ids" {
  type        = list(string)
  description = "Private subnet ids for PostgreSQL."
}

variable "database_name" {
  type        = string
  description = "Initial database name."

  validation {
    condition     = can(regex("^[a-z][a-z0-9_]{0,62}$", var.database_name))
    error_message = "database_name must be a PostgreSQL-compatible identifier up to 63 characters."
  }
}

variable "database_port" {
  type        = number
  description = "PostgreSQL port."
}

variable "postgres_engine_version" {
  type        = string
  description = "PostgreSQL engine version."
}

variable "master_username" {
  type        = string
  description = "Dedicated RDS master username for the memory system. RDS manages the credential outside Terraform state."
  default     = "memorysystem_owner"

  validation {
    condition     = can(regex("^[a-z][a-z0-9_]{0,62}$", var.master_username))
    error_message = "master_username must be a PostgreSQL-compatible identifier up to 63 characters."
  }
}

variable "instance_class" {
  type        = string
  description = "RDS instance class."
}

variable "allocated_storage_gib" {
  type        = number
  description = "Initial allocated storage in GiB."
  default     = 20
}

variable "max_allocated_storage_gib" {
  type        = number
  description = "Maximum autoscaled storage in GiB."
  default     = 100
}

variable "storage_type" {
  type        = string
  description = "RDS storage type."
  default     = "gp3"
}

variable "storage_encrypted" {
  type        = bool
  description = "Whether RDS storage encryption is enabled."
  default     = true
}

variable "storage_kms_key_id" {
  type        = string
  description = "Optional KMS key id or ARN for RDS storage encryption."
  default     = null
}

variable "master_user_secret_kms_key_id" {
  type        = string
  description = "Optional KMS key id or ARN for the RDS-managed master user secret."
  default     = null
}

variable "backup_retention_days" {
  type        = number
  description = "Backup retention window in days."
}

variable "backup_window" {
  type        = string
  description = "Preferred backup window in UTC."
  default     = "02:00-03:00"
}

variable "maintenance_window" {
  type        = string
  description = "Preferred maintenance window in UTC."
  default     = "sun:03:00-sun:04:00"
}

variable "deletion_protection" {
  type        = bool
  description = "Whether deletion protection should be enabled."
}

variable "skip_final_snapshot" {
  type        = bool
  description = "Whether to skip final snapshot on database deletion."
  default     = false
}

variable "final_snapshot_identifier" {
  type        = string
  description = "Optional final snapshot identifier used when final snapshots are enabled."
  default     = null
}

variable "auto_minor_version_upgrade" {
  type        = bool
  description = "Whether minor PostgreSQL version upgrades can be applied automatically."
  default     = true
}

variable "allow_major_version_upgrade" {
  type        = bool
  description = "Whether major PostgreSQL version upgrades are allowed."
  default     = false
}

variable "multi_az" {
  type        = bool
  description = "Whether RDS Multi-AZ is enabled."
  default     = false
}

variable "publicly_accessible" {
  type        = bool
  description = "Whether the RDS instance receives a public endpoint. Keep false for pilot and production."
  default     = false
}

variable "apply_immediately" {
  type        = bool
  description = "Whether pending RDS changes can apply outside the maintenance window."
  default     = false
}

variable "performance_insights_enabled" {
  type        = bool
  description = "Whether RDS Performance Insights is enabled."
  default     = false
}

variable "enabled_cloudwatch_logs_exports" {
  type        = list(string)
  description = "RDS PostgreSQL log exports."
  default     = ["postgresql", "upgrade"]
}

variable "iam_database_authentication_enabled" {
  type        = bool
  description = "Whether IAM database authentication is enabled."
  default     = false
}

variable "ca_cert_identifier" {
  type        = string
  description = "Optional RDS CA certificate identifier."
  default     = null
}

variable "database_client_security_group_ids" {
  type        = list(string)
  description = "Security group ids allowed to connect to PostgreSQL."
  default     = []
}

variable "database_client_cidr_blocks" {
  type        = list(string)
  description = "CIDR blocks allowed to connect to PostgreSQL. Prefer security group ids for ECS tasks."
  default     = []
}

variable "connection_secret_arn" {
  type        = string
  description = "Optional application runtime secret reference for PostgreSQL connection material."
  default     = null

  validation {
    condition     = var.connection_secret_arn == null || length(trimspace(var.connection_secret_arn)) > 0
    error_message = "connection_secret_arn must be null or a non-empty secret reference."
  }
}

variable "restore_validation_database" {
  type        = string
  description = "Restore-validation database target or naming pattern."
}

variable "restore_validation_secret_arn" {
  type        = string
  description = "Secret reference for restore-validation database access."
}
