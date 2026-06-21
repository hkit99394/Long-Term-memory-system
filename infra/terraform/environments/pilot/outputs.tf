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

output "platform_deployment_boundary" {
  description = "Explicit pilot deployment boundary for current Terraform coverage."
  value = {
    postgres_resources_provisioned      = true
    runtime_resources_provisioned       = module.runtime.deployment_boundary.runtime_resources_provisioned
    observability_resources_provisioned = module.observability.deployment_boundary.observability_resources_provisioned
    runtime_contract_only               = module.runtime.deployment_boundary.contract_only
    observability_contract_only         = module.observability.deployment_boundary.contract_only
  }
}
