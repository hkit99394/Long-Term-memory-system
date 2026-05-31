output "runtime_role_contract" {
  description = "Role commands and sizing expected by ECS task definitions."
  value       = module.runtime.role_contract
}

output "postgres_contract" {
  description = "Managed PostgreSQL and pgvector assumptions for PI-03."
  value       = module.postgres.platform_contract
}

output "observability_contract" {
  description = "External metric and alert-routing contract for PI-05 and PI-06."
  value       = module.observability.platform_contract
}
