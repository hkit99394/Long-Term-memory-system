output "role_contract" {
  description = "ECS role contract for API, worker, migrator, backup/restore, and benchmark tasks."
  value       = local.runtime_contract
}
