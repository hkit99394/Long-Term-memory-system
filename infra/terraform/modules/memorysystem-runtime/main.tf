locals {
  task_environment = {
    ASPNETCORE_ENVIRONMENT = title(var.environment_name)
    DOTNET_ENVIRONMENT     = title(var.environment_name)
    ASPNETCORE_URLS        = "http://+:${var.api_container_port}"
  }

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
    backup_restore_metrics   = local.backup_restore_metrics
    roles                    = local.roles
    future_jobs              = local.future_jobs
  }
}
