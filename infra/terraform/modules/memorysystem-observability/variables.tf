variable "service_name" {
  type        = string
  description = "Service name used for observability labels."
}

variable "environment_name" {
  type        = string
  description = "Environment name such as pilot or production."
}

variable "aws_region" {
  type        = string
  description = "AWS region for platform observability resources."
}

variable "alert_route_destinations" {
  type        = map(string)
  description = "Alert route destinations by severity or area."
  default = {
    page   = "unconfigured-page-route"
    ticket = "unconfigured-ticket-route"
    info   = "unconfigured-info-route"
  }
}

variable "alert_route_owners" {
  type        = map(string)
  description = "Named alert owners by route. Values are owner identifiers, not secret destinations."
  default = {
    page   = "memorysystem-oncall"
    ticket = "memorysystem-platform-maintainers"
    info   = "memorysystem-release-owner"
  }
}

variable "alert_silence_policy" {
  type = object({
    default_max_duration     = string
    page_max_duration        = string
    requires_owner_approval  = bool
    audit_destination        = string
    migration_silence_policy = string
  })
  description = "Environment silencing policy for routed alerts."
  default = {
    default_max_duration     = "4h"
    page_max_duration        = "1h"
    requires_owner_approval  = true
    audit_destination        = "release evidence record"
    migration_silence_policy = "Do not silence backup or restore-validation alerts during migrations."
  }
}

variable "alert_route_test_enabled" {
  type        = bool
  description = "Whether this environment must run the synthetic alert route test before release."
  default     = true
}

variable "dashboard_enabled" {
  type        = bool
  description = "Whether dashboard provisioning should be enabled."
}

variable "trace_export_enabled" {
  type        = bool
  description = "Whether trace exporter wiring should be enabled."
}

variable "external_metric_names" {
  type        = list(string)
  description = "External metric names expected from platform exporters and release jobs."
}

variable "release_evidence_bucket" {
  type        = string
  description = "Bucket name or reference for release evidence artifacts."
  default     = null
}
