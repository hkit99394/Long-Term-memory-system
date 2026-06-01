variable "service_name" {
  type        = string
  description = "Service name used for resource naming."
}

variable "environment_name" {
  type        = string
  description = "Environment name such as pilot or production."
}

variable "aws_region" {
  type        = string
  description = "AWS region for the runtime roles."
}

variable "image_digest" {
  type        = string
  description = "Immutable ECR image reference with sha256 digest."

  validation {
    condition     = can(regex("@sha256:[a-f0-9]{64}$", var.image_digest))
    error_message = "image_digest must end in @sha256:<64 hex chars>."
  }
}

variable "vpc_id" {
  type        = string
  description = "VPC id for runtime networking."
}

variable "private_subnet_ids" {
  type        = list(string)
  description = "Private subnet ids for ECS tasks."
}

variable "public_subnet_ids" {
  type        = list(string)
  description = "Public subnet ids for API ingress."
  default     = []
}

variable "api_container_port" {
  type        = number
  description = "API container port."
}

variable "api_desired_count" {
  type        = number
  description = "Desired API task count."
}

variable "worker_desired_count" {
  type        = number
  description = "Desired worker task count."
}

variable "api_task_cpu" {
  type        = number
  description = "API task CPU units."
}

variable "api_task_memory" {
  type        = number
  description = "API task memory in MiB."
}

variable "worker_task_cpu" {
  type        = number
  description = "Worker task CPU units."
}

variable "worker_task_memory" {
  type        = number
  description = "Worker task memory in MiB."
}

variable "job_task_cpu" {
  type        = number
  description = "One-shot job task CPU units."
}

variable "job_task_memory" {
  type        = number
  description = "One-shot job task memory in MiB."
}

variable "certificate_arn" {
  type        = string
  description = "TLS certificate ARN for API ingress."
  default     = null
}

variable "api_ingress_cidr_blocks" {
  type        = list(string)
  description = "CIDR blocks allowed to reach API ingress."
  default     = []
}

variable "postgres_endpoint" {
  type        = object({ host = string, port = number, database = string })
  description = "Database endpoint contract from the PostgreSQL module."
}

variable "postgres_secret_arn" {
  type        = string
  description = "Secret reference for PostgreSQL connection material."
}

variable "restore_secret_arn" {
  type        = string
  description = "Secret reference for restore-validation database access."
}

variable "restore_validation_database" {
  type        = string
  description = "Restore-validation database target or naming pattern."
}

variable "application_secret_arns" {
  type        = map(string)
  description = "Application secret references by purpose. Values are names or ARNs, not raw secrets."
}

variable "log_retention_days" {
  type        = number
  description = "Cloud log retention in days."
}

variable "release_evidence_bucket" {
  type        = string
  description = "Bucket name or reference for release evidence artifacts."
  default     = null
}

variable "backup_export_schedule_expression" {
  type        = string
  description = "Human-readable or EventBridge-compatible schedule expression for backup export jobs."
  default     = "rate(6 hours)"
}

variable "alert_route_destinations" {
  type        = map(string)
  description = "Alert route destinations by severity or area."
  default     = {}
}

variable "open_telemetry_enabled" {
  type        = bool
  description = "Whether runtime OpenTelemetry providers are enabled."
  default     = true
}

variable "open_telemetry_exporter" {
  type        = string
  description = "Runtime OpenTelemetry exporter selector. Use none for local/no collector or otlp for platform export."
  default     = "none"

  validation {
    condition     = contains(["none", "otlp"], var.open_telemetry_exporter)
    error_message = "open_telemetry_exporter must be either none or otlp."
  }
}

variable "open_telemetry_otlp_endpoint" {
  type        = string
  description = "Optional OTLP collector endpoint for runtime traces, metrics, and logs."
  default     = null
}
