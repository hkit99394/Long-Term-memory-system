locals {
  open_telemetry_environment = {
    OpenTelemetry__Enabled         = tostring(var.open_telemetry_enabled)
    OpenTelemetry__Exporter        = var.open_telemetry_exporter
    OpenTelemetry__ServiceName     = var.service_name
    OpenTelemetry__ServiceVersion  = var.image_digest
    OpenTelemetry__EnvironmentName = var.environment_name
    OTEL_SERVICE_NAME              = var.service_name
    OTEL_SERVICE_VERSION           = var.image_digest
    OTEL_DEPLOYMENT_ENVIRONMENT    = var.environment_name
    OTEL_EXPORTER_OTLP_ENDPOINT    = coalesce(var.open_telemetry_otlp_endpoint, "")
    OTEL_RESOURCE_ATTRIBUTES       = "service.namespace=memorysystem,deployment.environment=${var.environment_name}"
  }

  task_environment = merge({
    ASPNETCORE_ENVIRONMENT = title(var.environment_name)
    DOTNET_ENVIRONMENT     = title(var.environment_name)
    ASPNETCORE_URLS        = "http://+:${var.api_container_port}"
  }, local.open_telemetry_environment)

  secret_contract = {
    authentication     = var.application_secret_arns["authentication"]
    postgres           = var.postgres_secret_arn
    restore_validation = var.restore_secret_arn
    embeddings         = var.application_secret_arns["embeddings"]
  }

  backup_restore_metrics = [
    "memorysystem_backup_export_success",
    "memorysystem_backup_age_seconds",
    "memorysystem_backup_export_timestamp_seconds",
    "memorysystem_backup_export_bytes",
    "memorysystem_restore_validation_success",
    "memorysystem_restore_validation_age_seconds",
    "memorysystem_restore_validation_timestamp_seconds",
    "memorysystem_restore_validation_vector_extension_count",
    "memorysystem_restore_validation_table_rows"
  ]

  release_checklist_gates = [
    "migration",
    "health",
    "metrics",
    "benchmark_gate",
    "backup_restore",
    "rollback_owner",
    "alert_routing",
    "audit_evidence"
  ]

  release_checklist = {
    target_slice                  = "PI-07"
    document                      = "docs/production-release-checklists-pi07.md"
    environments                  = ["local", "ci", "pilot", "production"]
    required_gates                = local.release_checklist_gates
    release_evidence_bucket       = var.release_evidence_bucket
    rollback_owner_required       = true
    alert_route_test_metric       = "memorysystem_alert_route_test"
    observability_artifact_smoke  = "scripts/observability-artifacts-smoke.sh"
    operations_metrics_smoke      = "scripts/operations-metrics-smoke.sh"
    backup_restore_smoke          = "scripts/backup-restore-smoke.sh"
    benchmark_gate                = "scripts/benchmark-release-gate.sh"
    platform_deployment_smoke     = "scripts/production-pilot-deployment-smoke.sh"
    terraform_pilot_validation    = "terraform -chdir=infra/terraform/environments/pilot validate"
    terraform_production_validate = "terraform -chdir=infra/terraform/environments/production validate"
  }

  platform_rehearsal = {
    target_slice             = "PI-08"
    report                   = "docs/production-platform-rehearsal-pi08.md"
    command                  = "scripts/production-pilot-deployment-smoke.sh"
    scenario                 = "0001"
    required_roles           = ["migrator", "api", "worker"]
    required_checks          = ["migration", "health", "authenticated_smoke", "metrics", "backup_restore", "rollback_rehearsal"]
    benchmark_gate           = "scripts/benchmark-release-gate.sh"
    alert_routing_smoke      = "scripts/observability-artifacts-smoke.sh"
    release_evidence_bucket  = var.release_evidence_bucket
    external_rehearsal_owner = "platform-release-owner"
  }

  backup_export_environment = {
    MEMORYSYSTEM_ENVIRONMENT             = var.environment_name
    MEMORYSYSTEM_BACKUP_EVIDENCE_DIR     = "/tmp/memorysystem-backup-evidence"
    MEMORYSYSTEM_BACKUP_METRICS_FILE     = "/tmp/memorysystem-backup-evidence/backup-metrics.prom"
    MEMORYSYSTEM_BACKUP_EVIDENCE_FILE    = "/tmp/memorysystem-backup-evidence/backup-export-evidence.json"
    MEMORYSYSTEM_RELEASE_EVIDENCE_BUCKET = coalesce(var.release_evidence_bucket, "")
  }

  restore_validation_environment = {
    MEMORYSYSTEM_ENVIRONMENT                      = var.environment_name
    MEMORYSYSTEM_RESTORE_DATABASE                 = var.restore_validation_database
    MEMORYSYSTEM_RESTORE_VALIDATION_EVIDENCE_DIR  = "/tmp/memorysystem-backup-evidence"
    MEMORYSYSTEM_RESTORE_VALIDATION_METRICS_FILE  = "/tmp/memorysystem-backup-evidence/restore-validation-metrics.prom"
    MEMORYSYSTEM_RESTORE_VALIDATION_EVIDENCE_FILE = "/tmp/memorysystem-backup-evidence/restore-validation-evidence.json"
    MEMORYSYSTEM_RESTORE_VALIDATION_TABLES_FILE   = "/app/scripts/restore-validation-tables.txt"
    MEMORYSYSTEM_MIGRATOR_DLL                     = "/app/migrator/MemorySystem.Migrator.dll"
    MEMORYSYSTEM_MIGRATIONS_DIR                   = "/app/migrations"
  }

  roles = {
    api = {
      kind          = "service"
      desired_count = var.api_desired_count
      cpu           = var.api_task_cpu
      memory        = var.api_task_memory
      command       = ["dotnet", "/app/api/MemorySystem.Api.dll"]
      port          = var.api_container_port
      health_path   = "/health/ready"
    }

    worker = {
      kind          = "service"
      desired_count = var.worker_desired_count
      cpu           = var.worker_task_cpu
      memory        = var.worker_task_memory
      command       = ["dotnet", "/app/worker/MemorySystem.Worker.dll"]
      port          = null
      health_path   = null
    }

    migrator = {
      kind        = "run-task"
      cpu         = var.job_task_cpu
      memory      = var.job_task_memory
      command     = ["dotnet", "/app/migrator/MemorySystem.Migrator.dll", "--migrations-directory", "/app/migrations"]
      port        = null
      health_path = null
    }

    backup_export = {
      kind                 = "scheduled-task"
      target_slice         = "PI-04"
      schedule_expression  = var.backup_export_schedule_expression
      cpu                  = var.job_task_cpu
      memory               = var.job_task_memory
      command              = ["/app/scripts/platform-backup-export.sh"]
      port                 = null
      health_path          = null
      environment          = local.backup_export_environment
      required_secret_refs = ["postgres"]
      required_runtime_env = ["PGHOST", "PGPORT", "PGUSER", "PGPASSWORD", "PGDATABASE"]
      evidence_files       = [local.backup_export_environment["MEMORYSYSTEM_BACKUP_EVIDENCE_FILE"]]
      metrics_files        = [local.backup_export_environment["MEMORYSYSTEM_BACKUP_METRICS_FILE"]]
      emitted_metrics      = local.backup_restore_metrics
    }

    restore_validation = {
      kind                 = "run-task"
      target_slice         = "PI-04"
      cpu                  = var.job_task_cpu
      memory               = var.job_task_memory
      command              = ["/app/scripts/platform-restore-validation.sh"]
      port                 = null
      health_path          = null
      environment          = local.restore_validation_environment
      required_secret_refs = ["postgres", "restore_validation"]
      required_runtime_env = ["PGHOST", "PGPORT", "PGUSER", "PGPASSWORD", "PGDATABASE", "MEMORYSYSTEM_BACKUP_FILE", "MEMORYSYSTEM_RESTORE_CONNECTION_STRING"]
      evidence_files       = [local.restore_validation_environment["MEMORYSYSTEM_RESTORE_VALIDATION_EVIDENCE_FILE"]]
      metrics_files        = [local.restore_validation_environment["MEMORYSYSTEM_RESTORE_VALIDATION_METRICS_FILE"]]
      emitted_metrics      = local.backup_restore_metrics
    }

    demo_seeder = {
      kind        = "run-task"
      cpu         = var.job_task_cpu
      memory      = var.job_task_memory
      command     = ["dotnet", "/app/seeder/MemorySystem.DemoSeeder.dll", "--migrations-directory", "/app/migrations", "--skip-migrations"]
      port        = null
      health_path = null
    }
  }

  future_jobs = {
    benchmark_gate = {
      kind           = "run-task"
      target_slice   = "PI-08"
      expected_input = "image digest, API base URL, benchmark fixture set, and release evidence bucket"
    }
  }

  runtime_contract = {
    service_name             = var.service_name
    environment_name         = var.environment_name
    aws_region               = var.aws_region
    image_digest             = var.image_digest
    vpc_id                   = var.vpc_id
    private_subnet_ids       = var.private_subnet_ids
    public_subnet_ids        = var.public_subnet_ids
    certificate_arn          = var.certificate_arn
    api_ingress_cidr_blocks  = var.api_ingress_cidr_blocks
    postgres_endpoint        = var.postgres_endpoint
    secret_contract          = local.secret_contract
    task_environment         = local.task_environment
    log_retention_days       = var.log_retention_days
    release_evidence_bucket  = var.release_evidence_bucket
    alert_route_destinations = var.alert_route_destinations
    open_telemetry = {
      enabled                  = var.open_telemetry_enabled
      exporter                 = var.open_telemetry_exporter
      otlp_endpoint_configured = var.open_telemetry_otlp_endpoint != null
      service_name             = var.service_name
      service_version          = var.image_digest
      environment_name         = var.environment_name
      resource_attributes      = local.open_telemetry_environment["OTEL_RESOURCE_ATTRIBUTES"]
      trace_coverage_manifest  = "observability/tracing/memorysystem-pilot-trace-coverage.json"
    }
    backup_restore_metrics = local.backup_restore_metrics
    release_checklist      = local.release_checklist
    platform_rehearsal     = local.platform_rehearsal
    roles                  = local.roles
    future_jobs            = local.future_jobs
  }
}
