locals {
  alert_route_destinations = merge({
    page   = "unconfigured-page-route"
    ticket = "unconfigured-ticket-route"
    info   = "unconfigured-info-route"
  }, var.alert_route_destinations)

  alert_route_owners = merge({
    page   = "memorysystem-oncall"
    ticket = "memorysystem-platform-maintainers"
    info   = "memorysystem-release-owner"
  }, var.alert_route_owners)

  alert_routes = {
    page = {
      severity              = "page"
      route                 = "page"
      owner                 = local.alert_route_owners["page"]
      destination_ref       = "page"
      destination           = local.alert_route_destinations["page"]
      receiver_name         = "${var.service_name}-${var.environment_name}-page"
      escalation_fallback   = local.alert_route_owners["info"]
      max_silence_duration  = var.alert_silence_policy.page_max_duration
      requires_runbook_link = true
    }

    ticket = {
      severity              = "ticket"
      route                 = "ticket"
      owner                 = local.alert_route_owners["ticket"]
      destination_ref       = "ticket"
      destination           = local.alert_route_destinations["ticket"]
      receiver_name         = "${var.service_name}-${var.environment_name}-ticket"
      escalation_fallback   = local.alert_route_owners["info"]
      max_silence_duration  = var.alert_silence_policy.default_max_duration
      requires_runbook_link = true
    }

    info = {
      severity              = "info"
      route                 = "info"
      owner                 = local.alert_route_owners["info"]
      destination_ref       = "info"
      destination           = local.alert_route_destinations["info"]
      receiver_name         = "${var.service_name}-${var.environment_name}-info"
      escalation_fallback   = local.alert_route_owners["page"]
      max_silence_duration  = var.alert_silence_policy.default_max_duration
      requires_runbook_link = true
    }
  }

  environment_test_route = {
    environment      = var.environment_name
    enabled          = var.alert_route_test_enabled
    alert_name       = title(var.environment_name) == "Pilot" ? "MemorySystemPilotAlertRouteTest" : "MemorySystemProductionAlertRouteTest"
    trigger_metric   = "memorysystem_alert_route_test"
    severity         = "info"
    route            = "info"
    owner            = local.alert_route_owners["info"]
    destination_ref  = "info"
    destination      = local.alert_route_destinations["info"]
    runbook_url      = "docs/production-observability.md#alert-routing-test"
    evidence_bucket  = var.release_evidence_bucket
    success_criteria = "Synthetic info alert reaches the configured environment destination and the result is attached to release evidence."
  }

  platform_contract = {
    service_name             = var.service_name
    environment_name         = var.environment_name
    aws_region               = var.aws_region
    dashboard_enabled        = var.dashboard_enabled
    trace_export_enabled     = var.trace_export_enabled
    external_metric_names    = var.external_metric_names
    alert_route_destinations = local.alert_route_destinations
    alert_route_owners       = local.alert_route_owners
    alert_routes             = local.alert_routes
    alert_silence_policy     = var.alert_silence_policy
    alert_route_test         = local.environment_test_route
    release_evidence_bucket  = var.release_evidence_bucket
    checked_in_artifacts = {
      alert_rules      = "observability/prometheus/memorysystem-pilot-alerts.yml"
      alert_routing    = "observability/alert-routing/memorysystem-alert-routing.json"
      dashboard        = "observability/grafana/memorysystem-pilot-dashboard.json"
      trace_coverage   = "observability/tracing/memorysystem-pilot-trace-coverage.json"
      external_metrics = "observability/alert-inputs/external-pilot-metrics.txt"
    }
  }
}
