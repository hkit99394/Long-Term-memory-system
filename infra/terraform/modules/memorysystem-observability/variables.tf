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
  default     = {}
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
