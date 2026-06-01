output "role_contract" {
  description = "ECS role contract for API, worker, migrator, backup/restore, benchmark tasks, and release checklist gates."
  value       = local.runtime_contract
}
