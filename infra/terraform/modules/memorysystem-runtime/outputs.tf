output "role_contract" {
  description = "ECS role contract for API, worker, migrator, backup/restore, benchmark tasks, and release checklist gates."
  value       = local.runtime_contract
}

output "deployment_boundary" {
  description = "Machine-readable boundary clarifying that this module emits runtime contracts but does not yet provision ECS or scheduler resources."
  value = {
    contract_only                 = true
    runtime_resources_provisioned = false
    apply_creates_runtime         = false
    provisioned_resource_types    = []
    future_resource_types = [
      "aws_ecs_cluster",
      "aws_ecs_service",
      "aws_ecs_task_definition",
      "aws_lb",
      "aws_cloudwatch_log_group",
      "aws_scheduler_schedule",
      "aws_iam_role"
    ]
  }
}
