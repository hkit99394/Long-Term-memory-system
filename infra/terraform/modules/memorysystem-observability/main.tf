locals {
  platform_contract = {
    service_name             = var.service_name
    environment_name         = var.environment_name
    aws_region               = var.aws_region
    dashboard_enabled        = var.dashboard_enabled
    trace_export_enabled     = var.trace_export_enabled
    external_metric_names    = var.external_metric_names
    alert_route_destinations = var.alert_route_destinations
    release_evidence_bucket  = var.release_evidence_bucket
    checked_in_artifacts = {
      alert_rules      = "observability/prometheus/memorysystem-pilot-alerts.yml"
      dashboard        = "observability/grafana/memorysystem-pilot-dashboard.json"
      trace_coverage   = "observability/tracing/memorysystem-pilot-trace-coverage.json"
      external_metrics = "observability/alert-inputs/external-pilot-metrics.txt"
    }
  }
}
