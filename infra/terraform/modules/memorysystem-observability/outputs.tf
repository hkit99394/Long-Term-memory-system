output "platform_contract" {
  description = "Observability exporter, dashboard, alert routing, and release evidence contract."
  value       = local.platform_contract
}

output "deployment_boundary" {
  description = "Machine-readable boundary clarifying that this module emits observability contracts but does not yet provision managed collectors, exporters, or alert receivers."
  value = {
    contract_only                            = true
    observability_resources_provisioned      = false
    apply_creates_observability_integrations = false
    provisioned_resource_types               = []
    future_resource_types = [
      "aws_cloudwatch_metric_alarm",
      "aws_cloudwatch_dashboard",
      "aws_prometheus_workspace",
      "aws_grafana_workspace",
      "aws_sns_topic"
    ]
  }
}
