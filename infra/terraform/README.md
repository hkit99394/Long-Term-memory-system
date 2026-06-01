# MemorySystem Terraform Platform

PI-02 introduced the first Terraform layout for the AWS production-pilot
baseline selected by PI-01. PI-03 adds the first managed PostgreSQL resources:
an RDS PostgreSQL instance, private subnet group, database security group,
controlled ingress, backup/PITR settings, RDS-managed master credential, and a
pgvector validation contract tied to the checked-in SQL migrations.
PI-04 adds executable backup/export and restore-validation job contracts that
produce evidence JSON and Prometheus-compatible metrics for the existing
observability alerts. PI-07 adds the release checklist contract that ties
runtime evidence to the local, CI, pilot, and production release gates. PI-08
records the first isolated platform rehearsal and keeps the same command/report
contract visible in runtime outputs.

This is still a staged platform baseline. Runtime ECS resources and managed
collector/receiver resources remain future platform work, but the first local
platform rehearsal path is now documented and smoke-tested.

## Layout

```text
infra/terraform/
  environments/
    pilot/
    production/
  modules/
    memorysystem-runtime/
    memorysystem-postgres/
    memorysystem-observability/
```

## Boundary

Terraform owns:

- ECS Fargate runtime role shape for API, worker, migrator, and one-shot jobs
- ECR image digest inputs
- RDS PostgreSQL resources and pgvector validation expectations
- backup/export and restore-validation job commands, evidence files, and metric
  files
- networking, ingress, TLS, and task resource assumptions
- secret references by name or ARN
- observability exporter, alert-routing, owner, silence-policy, and route-test
  contracts
- release checklist metadata for migration, health, metrics, benchmark,
  backup/restore, rollback owner, alert routing, and audit evidence
- platform rehearsal report and smoke command metadata for the PI-08
  migrator/API/worker rehearsal

Terraform does not own:

- SQL migrations under `migrations/`
- API, worker, migrator, or broker behavior
- raw API keys, OpenAI keys, PostgreSQL passwords, or connection strings
- benchmark outputs or release reports
- production data movement

## Artifact Contract

The root `Dockerfile` builds a single multi-role OCI image containing published
outputs for API, worker, migrator, demo seeder, the PI-04 backup/restore
scripts, the GC-03 erasure replay ledger exporter, and the GC-04 retention
minimization job. Runtime roles choose the entrypoint command in ECS task
definitions:

| Role | Command |
| --- | --- |
| API | `dotnet /app/api/MemorySystem.Api.dll` |
| Worker | `dotnet /app/worker/MemorySystem.Worker.dll` |
| Migrator | `dotnet /app/migrator/MemorySystem.Migrator.dll --migrations-directory /app/migrations` |
| Backup export | `/app/scripts/platform-backup-export.sh` |
| Erasure replay ledger export | `/app/scripts/platform-erasure-replay-ledger-export.sh` |
| Restore validation | `/app/scripts/platform-restore-validation.sh` |
| Retention minimization | `/app/scripts/platform-retention-minimization.sh` |
| External payload retention check | `/app/scripts/platform-external-payload-retention-check.sh` |
| Compliance evidence package | `/app/scripts/platform-compliance-evidence-package.sh` |
| Demo seeder | `dotnet /app/seeder/MemorySystem.DemoSeeder.dll --migrations-directory /app/migrations` |

The backup and restore scripts expect libpq-compatible PostgreSQL environment
variables such as `PGHOST`, `PGPORT`, `PGUSER`, `PGPASSWORD`, and `PGDATABASE`
from the platform secret injection layer. Erasure replay ledger export writes a
payload-safe CSV plus evidence/metrics under `/tmp/memorysystem-backup-evidence`.
Restore validation also expects `MEMORYSYSTEM_BACKUP_FILE`,
`MEMORYSYSTEM_RESTORE_CONNECTION_STRING`, `MEMORYSYSTEM_BACKUP_CREATED_AT_UTC`,
and `MEMORYSYSTEM_ERASURE_REPLAY_LEDGER_FILE`.
Retention minimization defaults to dry-run mode and writes payload-safe
governance evidence/metrics under `/tmp/memorysystem-governance-evidence`.
External payload retention check defaults to audit-only disabled-policy mode and
writes payload-safe pointer-state evidence/metrics under the same governance
evidence directory.
Compliance evidence package defaults to draft mode and writes a payload-safe
manifest, artifact index, hash sidecar, and metrics under
`/tmp/memorysystem-compliance-evidence`.
Benchmark-gate and platform-smoke jobs remain future job contracts for later
slices, but their commands are referenced by the PI-07 release checklist
contract so release records can point at one stable evidence path.

Deploy by immutable image digest, for example:

```text
123456789012.dkr.ecr.eu-west-2.amazonaws.com/memorysystem@sha256:<64 hex chars>
```

## PostgreSQL Contract

The `memorysystem-postgres` module provisions:

- `aws_db_instance` for PostgreSQL with storage encryption enabled by default
- `aws_db_subnet_group` using the private subnet inputs
- a database security group with explicit client security group and IPv4 CIDR
  ingress lists
- RDS-managed master user credential material through
  `manage_master_user_password`
- backup retention, final snapshot, maintenance window, and log export inputs
- a `pgvector_validation` output that records the migration SQL and validation
  query for the `vector` extension

The SQL migration remains the owner of extension creation:

```sql
CREATE EXTENSION IF NOT EXISTS vector;
```

## Backup And Restore Jobs

PI-04 defines two platform job commands in `memorysystem-runtime`:

- `backup_export`: scheduled job that runs `pg_dump`, inspects the custom-format
  archive with `pg_restore --list`, writes backup export evidence JSON, and
  emits `memorysystem_backup_*` metrics.
- `restore_validation`: run-task job that restores a selected backup into a
  fresh validation database, reruns migrations, checks the central restore table
  manifest and `pgvector`, writes restore evidence JSON, and emits
  `memorysystem_restore_validation_*` metrics.

The scripts write evidence and metrics under `/tmp/memorysystem-backup-evidence`
by default. The task definition or wrapper that runs these jobs should upload
those files to the configured release evidence bucket or scrape/push the metrics
according to the target platform's observability setup.

## Release Checklist Contract

PI-07 adds `release_checklist` to the runtime module contract. It points to
`docs/production-release-checklists-pi07.md` and records the required local, CI,
pilot, and production gates:

- migration
- health
- metrics
- benchmark gate
- backup/restore
- rollback owner
- alert routing
- audit evidence

The contract also names the checked-in commands for
`scripts/observability-artifacts-smoke.sh`, `scripts/operations-metrics-smoke.sh`,
`scripts/backup-restore-smoke.sh`, `scripts/benchmark-release-gate.sh`, and
`scripts/production-pilot-deployment-smoke.sh`. Pilot and production evidence
should be uploaded to `release_evidence_bucket` or an equivalent controlled
audit store.

## Platform Rehearsal Contract

PI-08 records the first isolated platform rehearsal in
`docs/production-platform-rehearsal-pi08.md`. The runtime module exposes
`platform_rehearsal` with the `scripts/production-pilot-deployment-smoke.sh`
command, Scenario 0001, required migrator/API/worker roles, backup/restore and
rollback checks, benchmark gate, alert-routing smoke, and evidence bucket. This
keeps the local rehearsal shape aligned with the future ECS run-task/service
implementation.

## Local Checks

Run formatting after Terraform files change:

```bash
terraform fmt -recursive infra/terraform
```

Unit tests guard the platform shape and the no-secret Terraform contract:

```bash
dotnet test tests/MemorySystem.UnitTests/MemorySystem.UnitTests.csproj --no-restore
```

Validate both environment overlays after provider initialization:

```bash
terraform -chdir=infra/terraform/environments/pilot validate
terraform -chdir=infra/terraform/environments/production validate
```

Do not run `terraform apply` until the target AWS account, backend state,
secrets, VPC/subnet inputs, and release owner have been reviewed for that
environment.
