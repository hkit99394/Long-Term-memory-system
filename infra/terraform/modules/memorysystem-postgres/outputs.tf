output "endpoint_contract" {
  description = "Database endpoint contract for runtime roles."
  value       = local.endpoint_contract
}

output "connection_secret_reference" {
  description = "Secret reference used by runtime roles to connect to PostgreSQL."
  value       = local.connection_secret_reference
}

output "managed_master_user_secret_reference" {
  description = "RDS-managed master user secret reference for administrative database access."
  value       = local.managed_master_user_secret_arn
}

output "restore_validation_secret_reference" {
  description = "Secret reference used by restore-validation jobs."
  value       = var.restore_validation_secret_arn
}

output "platform_contract" {
  description = "Managed PostgreSQL and pgvector platform contract."
  value       = local.platform_contract
}

output "pgvector_validation" {
  description = "SQL contract that proves pgvector is enabled by migrations."
  value       = local.pgvector_validation
}
