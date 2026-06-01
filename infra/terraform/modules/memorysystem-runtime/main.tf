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
    "memorysystem_erasure_replay_ledger_export_success",
    "memorysystem_erasure_replay_ledger_records",
    "memorysystem_erasure_replay_ledger_bytes",
    "memorysystem_erasure_replay_ledger_timestamp_seconds",
    "memorysystem_restore_validation_success",
    "memorysystem_restore_validation_age_seconds",
    "memorysystem_restore_validation_timestamp_seconds",
    "memorysystem_restore_validation_vector_extension_count",
    "memorysystem_restore_validation_table_rows",
    "memorysystem_restore_erasure_replay_validation_success",
    "memorysystem_restore_erasure_replay_configured",
    "memorysystem_restore_erasure_replay_required",
    "memorysystem_restore_erasure_replay_actions",
    "memorysystem_restore_erasure_replay_failures"
  ]

  governance_retention_metrics = [
    "memorysystem_retention_minimization_success",
    "memorysystem_retention_minimization_candidates",
    "memorysystem_retention_minimization_minimized_events",
    "memorysystem_retention_minimization_review_notes_cleared",
    "memorysystem_retention_minimization_legal_hold_skipped",
    "memorysystem_retention_minimization_external_payload_skipped",
    "memorysystem_retention_minimization_failures",
    "memorysystem_retention_minimization_timestamp_seconds",
    "memorysystem_external_payload_retention_check_success",
    "memorysystem_external_payload_retention_check_targets",
    "memorysystem_external_payload_retention_expected_present",
    "memorysystem_external_payload_retention_expected_absent",
    "memorysystem_external_payload_retention_verified_present",
    "memorysystem_external_payload_retention_verified_absent",
    "memorysystem_external_payload_retention_unverified_targets",
    "memorysystem_external_payload_retention_policy_violations",
    "memorysystem_external_payload_retention_unsupported_scheme",
    "memorysystem_external_payload_retention_mismatches",
    "memorysystem_external_payload_retention_probe_failures",
    "memorysystem_external_payload_retention_failures",
    "memorysystem_external_payload_retention_timestamp_seconds"
  ]

  governance_compliance_metrics = [
    "memorysystem_compliance_evidence_package_success",
    "memorysystem_compliance_evidence_package_artifacts",
    "memorysystem_compliance_evidence_package_present_artifacts",
    "memorysystem_compliance_evidence_package_missing_artifacts",
    "memorysystem_compliance_evidence_package_missing_required_artifacts",
    "memorysystem_compliance_evidence_package_manifest_bytes",
    "memorysystem_compliance_evidence_package_timestamp_seconds"
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

  erasure_replay_ledger_environment = {
    MEMORYSYSTEM_ENVIRONMENT                  = var.environment_name
    MEMORYSYSTEM_ERASURE_REPLAY_EVIDENCE_DIR  = "/tmp/memorysystem-backup-evidence"
    MEMORYSYSTEM_ERASURE_REPLAY_LEDGER_FILE   = "/tmp/memorysystem-backup-evidence/erasure-replay-ledger.csv"
    MEMORYSYSTEM_ERASURE_REPLAY_METRICS_FILE  = "/tmp/memorysystem-backup-evidence/erasure-replay-ledger-metrics.prom"
    MEMORYSYSTEM_ERASURE_REPLAY_EVIDENCE_FILE = "/tmp/memorysystem-backup-evidence/erasure-replay-ledger-evidence.json"
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
    MEMORYSYSTEM_ERASURE_REPLAY_LEDGER_FILE       = "/tmp/memorysystem-backup-evidence/erasure-replay-ledger.csv"
  }

  retention_minimization_environment = {
    MEMORYSYSTEM_ENVIRONMENT                          = var.environment_name
    MEMORYSYSTEM_RETENTION_MINIMIZATION_MODE          = "dry-run"
    MEMORYSYSTEM_RETENTION_STANDARD_MAX_AGE_DAYS      = "90"
    MEMORYSYSTEM_RETENTION_AUDIT_MAX_AGE_DAYS         = "365"
    MEMORYSYSTEM_RETENTION_MINIMIZATION_BATCH_SIZE    = "500"
    MEMORYSYSTEM_RETENTION_MINIMIZATION_EVIDENCE_DIR  = "/tmp/memorysystem-governance-evidence"
    MEMORYSYSTEM_RETENTION_MINIMIZATION_METRICS_FILE  = "/tmp/memorysystem-governance-evidence/retention-minimization-metrics.prom"
    MEMORYSYSTEM_RETENTION_MINIMIZATION_EVIDENCE_FILE = "/tmp/memorysystem-governance-evidence/retention-minimization-evidence.json"
  }

  external_payload_retention_environment = {
    MEMORYSYSTEM_ENVIRONMENT                             = var.environment_name
    MEMORYSYSTEM_EXTERNAL_PAYLOAD_CHECK_MODE             = "audit-only"
    MEMORYSYSTEM_EXTERNAL_PAYLOAD_POLICY_MODE            = "disabled"
    MEMORYSYSTEM_EXTERNAL_PAYLOAD_ALLOWED_SCHEMES        = ""
    MEMORYSYSTEM_EXTERNAL_PAYLOAD_EPHEMERAL_MAX_AGE_DAYS = "7"
    MEMORYSYSTEM_EXTERNAL_PAYLOAD_STANDARD_MAX_AGE_DAYS  = "90"
    MEMORYSYSTEM_EXTERNAL_PAYLOAD_AUDIT_MAX_AGE_DAYS     = "365"
    MEMORYSYSTEM_EXTERNAL_PAYLOAD_CHECK_BATCH_SIZE       = "500"
    MEMORYSYSTEM_EXTERNAL_PAYLOAD_EVIDENCE_DIR           = "/tmp/memorysystem-governance-evidence"
    MEMORYSYSTEM_EXTERNAL_PAYLOAD_METRICS_FILE           = "/tmp/memorysystem-governance-evidence/external-payload-retention-metrics.prom"
    MEMORYSYSTEM_EXTERNAL_PAYLOAD_EVIDENCE_FILE          = "/tmp/memorysystem-governance-evidence/external-payload-retention-evidence.json"
  }

  compliance_evidence_package_environment = {
    MEMORYSYSTEM_ENVIRONMENT                                = var.environment_name
    MEMORYSYSTEM_COMPLIANCE_EVIDENCE_PACKAGE_MODE           = "draft"
    MEMORYSYSTEM_COMPLIANCE_OPERATOR_ID                     = "platform-release-owner"
    MEMORYSYSTEM_COMPLIANCE_GOVERNANCE_EVIDENCE_DIR         = "/tmp/memorysystem-governance-evidence"
    MEMORYSYSTEM_COMPLIANCE_BACKUP_EVIDENCE_DIR             = "/tmp/memorysystem-backup-evidence"
    MEMORYSYSTEM_COMPLIANCE_RELEASE_EVIDENCE_DIR            = "/tmp/memorysystem-release-evidence"
    MEMORYSYSTEM_COMPLIANCE_EVIDENCE_DIR                    = "/tmp/memorysystem-compliance-evidence"
    MEMORYSYSTEM_COMPLIANCE_PACKAGE_FILE                    = "/tmp/memorysystem-compliance-evidence/compliance-evidence-package.json"
    MEMORYSYSTEM_COMPLIANCE_ARTIFACT_INDEX_FILE             = "/tmp/memorysystem-compliance-evidence/compliance-evidence-package-artifacts.ndjson"
    MEMORYSYSTEM_COMPLIANCE_PACKAGE_HASH_FILE               = "/tmp/memorysystem-compliance-evidence/compliance-evidence-package.json.sha256"
    MEMORYSYSTEM_COMPLIANCE_METRICS_FILE                    = "/tmp/memorysystem-compliance-evidence/compliance-evidence-package-metrics.prom"
    MEMORYSYSTEM_COMPLIANCE_AUDIT_EXPORT_FILE               = "/tmp/memorysystem-governance-evidence/audit-export.ndjson"
    MEMORYSYSTEM_COMPLIANCE_RETENTION_REPORT_FILE           = "/tmp/memorysystem-governance-evidence/retention-report.json"
    MEMORYSYSTEM_COMPLIANCE_LEGAL_HOLD_SUMMARY_FILE         = "/tmp/memorysystem-governance-evidence/legal-hold-summary.json"
    MEMORYSYSTEM_COMPLIANCE_PERMISSION_DRIFT_FILE           = "/tmp/memorysystem-governance-evidence/permission-drift-report.json"
    MEMORYSYSTEM_COMPLIANCE_RETENTION_MINIMIZATION_FILE     = "/tmp/memorysystem-governance-evidence/retention-minimization-evidence.json"
    MEMORYSYSTEM_COMPLIANCE_EXTERNAL_PAYLOAD_RETENTION_FILE = "/tmp/memorysystem-governance-evidence/external-payload-retention-evidence.json"
    MEMORYSYSTEM_COMPLIANCE_ERASURE_REPLAY_FILE             = "/tmp/memorysystem-backup-evidence/erasure-replay-ledger-evidence.json"
    MEMORYSYSTEM_COMPLIANCE_BACKUP_EXPORT_FILE              = "/tmp/memorysystem-backup-evidence/backup-export-evidence.json"
    MEMORYSYSTEM_COMPLIANCE_RESTORE_VALIDATION_FILE         = "/tmp/memorysystem-backup-evidence/restore-validation-evidence.json"
    MEMORYSYSTEM_COMPLIANCE_RELEASE_CHECKLIST_FILE          = "/tmp/memorysystem-release-evidence/release-checklist-evidence.json"
    MEMORYSYSTEM_COMPLIANCE_BENCHMARK_GATE_FILE             = "/tmp/memorysystem-release-evidence/benchmark-release-gate.json"
    MEMORYSYSTEM_COMPLIANCE_ALERT_ROUTE_FILE                = "/tmp/memorysystem-release-evidence/alert-route-smoke.json"
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

    erasure_replay_ledger_export = {
      kind                 = "run-task"
      target_slice         = "GC-03"
      cpu                  = var.job_task_cpu
      memory               = var.job_task_memory
      command              = ["/app/scripts/platform-erasure-replay-ledger-export.sh"]
      port                 = null
      health_path          = null
      environment          = local.erasure_replay_ledger_environment
      required_secret_refs = ["postgres"]
      required_runtime_env = ["PGHOST", "PGPORT", "PGUSER", "PGPASSWORD", "PGDATABASE"]
      evidence_files       = [local.erasure_replay_ledger_environment["MEMORYSYSTEM_ERASURE_REPLAY_EVIDENCE_FILE"]]
      metrics_files        = [local.erasure_replay_ledger_environment["MEMORYSYSTEM_ERASURE_REPLAY_METRICS_FILE"]]
      emitted_metrics      = local.backup_restore_metrics
    }

    restore_validation = {
      kind                 = "run-task"
      target_slice         = "GC-03"
      cpu                  = var.job_task_cpu
      memory               = var.job_task_memory
      command              = ["/app/scripts/platform-restore-validation.sh"]
      port                 = null
      health_path          = null
      environment          = local.restore_validation_environment
      required_secret_refs = ["postgres", "restore_validation"]
      required_runtime_env = ["PGHOST", "PGPORT", "PGUSER", "PGPASSWORD", "PGDATABASE", "MEMORYSYSTEM_BACKUP_FILE", "MEMORYSYSTEM_RESTORE_CONNECTION_STRING", "MEMORYSYSTEM_BACKUP_CREATED_AT_UTC", "MEMORYSYSTEM_ERASURE_REPLAY_LEDGER_FILE"]
      evidence_files       = [local.restore_validation_environment["MEMORYSYSTEM_RESTORE_VALIDATION_EVIDENCE_FILE"]]
      metrics_files        = [local.restore_validation_environment["MEMORYSYSTEM_RESTORE_VALIDATION_METRICS_FILE"]]
      emitted_metrics      = local.backup_restore_metrics
    }

    retention_minimization = {
      kind                 = "run-task"
      target_slice         = "GC-04"
      cpu                  = var.job_task_cpu
      memory               = var.job_task_memory
      command              = ["/app/scripts/platform-retention-minimization.sh"]
      port                 = null
      health_path          = null
      environment          = local.retention_minimization_environment
      required_secret_refs = ["postgres"]
      required_runtime_env = ["PGHOST", "PGPORT", "PGUSER", "PGPASSWORD", "PGDATABASE"]
      evidence_files       = [local.retention_minimization_environment["MEMORYSYSTEM_RETENTION_MINIMIZATION_EVIDENCE_FILE"]]
      metrics_files        = [local.retention_minimization_environment["MEMORYSYSTEM_RETENTION_MINIMIZATION_METRICS_FILE"]]
      emitted_metrics      = local.governance_retention_metrics
    }

    external_payload_retention_check = {
      kind                 = "run-task"
      target_slice         = "GC-05"
      cpu                  = var.job_task_cpu
      memory               = var.job_task_memory
      command              = ["/app/scripts/platform-external-payload-retention-check.sh"]
      port                 = null
      health_path          = null
      environment          = local.external_payload_retention_environment
      required_secret_refs = ["postgres"]
      required_runtime_env = ["PGHOST", "PGPORT", "PGUSER", "PGPASSWORD", "PGDATABASE"]
      evidence_files       = [local.external_payload_retention_environment["MEMORYSYSTEM_EXTERNAL_PAYLOAD_EVIDENCE_FILE"]]
      metrics_files        = [local.external_payload_retention_environment["MEMORYSYSTEM_EXTERNAL_PAYLOAD_METRICS_FILE"]]
      emitted_metrics      = local.governance_retention_metrics
    }

    compliance_evidence_package = {
      kind                 = "run-task"
      target_slice         = "GC-06"
      cpu                  = var.job_task_cpu
      memory               = var.job_task_memory
      command              = ["/app/scripts/platform-compliance-evidence-package.sh"]
      port                 = null
      health_path          = null
      environment          = local.compliance_evidence_package_environment
      required_secret_refs = []
      required_runtime_env = ["MEMORYSYSTEM_COMPLIANCE_EVIDENCE_PACKAGE_MODE", "MEMORYSYSTEM_COMPLIANCE_PACKAGE_FILE", "MEMORYSYSTEM_COMPLIANCE_ARTIFACT_INDEX_FILE", "MEMORYSYSTEM_COMPLIANCE_PACKAGE_HASH_FILE"]
      evidence_files       = [local.compliance_evidence_package_environment["MEMORYSYSTEM_COMPLIANCE_PACKAGE_FILE"], local.compliance_evidence_package_environment["MEMORYSYSTEM_COMPLIANCE_ARTIFACT_INDEX_FILE"], local.compliance_evidence_package_environment["MEMORYSYSTEM_COMPLIANCE_PACKAGE_HASH_FILE"]]
      metrics_files        = [local.compliance_evidence_package_environment["MEMORYSYSTEM_COMPLIANCE_METRICS_FILE"]]
      emitted_metrics      = local.governance_compliance_metrics
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
    backup_restore_metrics        = local.backup_restore_metrics
    governance_retention_metrics  = local.governance_retention_metrics
    governance_compliance_metrics = local.governance_compliance_metrics
    release_checklist             = local.release_checklist
    platform_rehearsal            = local.platform_rehearsal
    roles                         = local.roles
    future_jobs                   = local.future_jobs
  }
}
