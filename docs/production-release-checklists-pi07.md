# Production Release Checklists PI-07

PI-07 turns the LR-05 environment release checklist into an explicit release
artifact. The goal is to make every local, CI, pilot, and production release
leave enough evidence for an operator to answer three questions:

- what changed
- whether the memory system is healthy
- who owns rollback if the release fails

These checklists do not change API contracts, SQL schema history, benchmark
thresholds, or Terraform provider choices. They define the evidence that must
be attached to a release record before the release is considered complete.

## Common Evidence Manifest

Every environment should attach or link a release evidence manifest with these
fields:

| Field | Required evidence |
| --- | --- |
| Release identity | Release id, environment, operator, timestamp, git commit, image digest or build artifact, and change summary. |
| Migration | Migration command, migration output, target database, and confirmation that no applied migration file was edited. |
| Health | `/health/live`, `/health/ready`, worker heartbeat when a worker is running, and authenticated smoke result when an API is running. |
| Metrics | Metrics snapshot, OpenTelemetry exporter status when configured, and operations metrics smoke result. |
| Benchmark gate | `scripts/benchmark-release-gate.sh` output or a dry-run report that explains why the gate was not required for the environment. |
| Backup/restore | Backup freshness, restore validation result, or local `scripts/backup-restore-smoke.sh` result. |
| Rollback owner | Human owner, contact route, rollback decision point, and rollback command or procedure link. |
| Alert routing | Alert owner, route destination, silence policy, runbook link, and route-test evidence such as `memorysystem_alert_route_test`. |
| Audit evidence | Evidence location, retention window, approval record, and known exceptions. |

Pilot and production evidence should be uploaded to the configured
`release_evidence_bucket` or an equivalent controlled audit store.

## Local Release Checklist

Local releases prove the developer workstation can still build, test, migrate,
observe, benchmark, back up, and restore the system.

| Gate | Required local check | Evidence |
| --- | --- | --- |
| Migration | Run the migrator against a local PostgreSQL database or use `scripts/production-pilot-deployment-smoke.sh`, which exercises the migrator role. | Migrator output and target database name. |
| Health | Start the API locally and verify `/health/live` and `/health/ready`, or use `scripts/production-pilot-deployment-smoke.sh`. | HTTP status output and API log path. |
| Metrics | Run `scripts/operations-metrics-smoke.sh` against the local API and `scripts/observability-artifacts-smoke.sh` for checked-in alert, dashboard, trace, and routing artifacts. | Metrics snapshot and observability smoke output. |
| Benchmark gate | Run `scripts/benchmark-release-gate.sh` with filled local scorecards, or record a dry run when the release has no retrieval, ranking, policy, or contract impact. | Release-gate report path. |
| Backup/restore | Run `scripts/backup-restore-smoke.sh` against the local Docker PostgreSQL database. | Backup file name, restore validation database, and table-count output. |
| Rollback owner | Name the person who will revert the local branch, stop local processes, or restore the local database if smoke tests fail. | Rollback owner and rollback note in the local release manifest. |
| Alert routing | Run `scripts/observability-artifacts-smoke.sh` and confirm the checked-in pilot and production route tests include `memorysystem_alert_route_test`. | Alert routing smoke output. |
| Audit evidence | Store the command transcript or CI attachment link with the local release manifest. | Evidence path and retention note. |

Minimum local command set:

```bash
dotnet restore MemorySystem.sln
dotnet build MemorySystem.sln --no-restore
dotnet test tests/MemorySystem.UnitTests/MemorySystem.UnitTests.csproj --no-restore
dotnet test tests/MemorySystem.IntegrationTests/MemorySystem.IntegrationTests.csproj --no-build
scripts/observability-artifacts-smoke.sh
scripts/backup-restore-smoke.sh
scripts/benchmark-release-gate.sh
```

## CI Release Checklist

CI releases prove the repository remains reproducible without local state. CI
may not have a long-lived PostgreSQL instance, but it must still record the
reason when database-backed migration or backup/restore checks are skipped.

| Gate | Required CI check | Evidence |
| --- | --- | --- |
| Migration | Run SQL migration tests and, when PostgreSQL is available, database-backed integration tests with `MEMORYSYSTEM_REQUIRE_DATABASE_TESTS=true`. | Test report and PostgreSQL availability note. |
| Health | Run build and non-database integration tests; for container jobs, verify API health through the production-pilot smoke script. | Build/test logs and health output when a container is started. |
| Metrics | Run `scripts/observability-artifacts-smoke.sh`; when CI starts the API, set `MEMORYSYSTEM_OBSERVABILITY_VALIDATE_LIVE_METRICS=true` so `scripts/operations-metrics-smoke.sh` also runs. | Observability artifact smoke output and optional metrics snapshot. |
| Benchmark gate | Run `scripts/benchmark-release-gate.sh` for release branches, or attach the latest benchmark artifact check for ordinary pull requests. | Benchmark report or explicit skip reason. |
| Backup/restore | Run `scripts/backup-restore-smoke.sh` when Docker PostgreSQL is available; otherwise attach the backup/restore test report and skip reason. | Restore smoke output or skip record. |
| Rollback owner | Assign the release engineer or merge-train owner who can revert the change and pause deployment. | CI release manifest field. |
| Alert routing | Validate alert route labels, owners, runbook links, silence policy, and `memorysystem_alert_route_test` through `scripts/observability-artifacts-smoke.sh`. | Alert routing validation output. |
| Audit evidence | Attach build logs, test results, Terraform validation, benchmark report, and skip reasons to the CI artifact bundle. | CI artifact URL or evidence path. |

Minimum CI command set:

```bash
dotnet restore MemorySystem.sln
dotnet build MemorySystem.sln --no-restore
dotnet test tests/MemorySystem.UnitTests/MemorySystem.UnitTests.csproj --no-restore
dotnet test tests/MemorySystem.IntegrationTests/MemorySystem.IntegrationTests.csproj --no-build
terraform fmt -check -recursive infra/terraform
terraform -chdir=infra/terraform/environments/pilot validate
terraform -chdir=infra/terraform/environments/production validate
scripts/observability-artifacts-smoke.sh
scripts/benchmark-release-gate.sh
```

## Pilot Release Checklist

Pilot releases prove one production-shaped environment can deploy, observe,
back up, restore, and roll back the service with real platform wiring.

| Gate | Required pilot check | Evidence |
| --- | --- | --- |
| Migration | Run the migrator one-shot role against the pilot database before API or worker rollout. | Migrator task id, exit code, logs, and migration output. |
| Health | Verify API `/health/live`, API `/health/ready`, worker heartbeat freshness, authenticated read smoke, and write-path smoke when the release touches writes, broker policy, context, indexing, review, export, or governance. | Health response, worker heartbeat timestamp, and smoke output. |
| Metrics | Verify `/api/operations/summary`, `/api/operations/metrics`, OpenTelemetry exporter health, and alert input freshness through `scripts/operations-metrics-smoke.sh` or the platform equivalent. | Metrics snapshot and exporter status. |
| Benchmark gate | Attach a fresh `scripts/benchmark-release-gate.sh` report for the pilot target model and fixture set. | Benchmark report with Memory Lift, Contract Lift, scoped-safety leaks, stale-memory usage, source-link coverage, and agent-contract smoke. |
| Backup/restore | Confirm backup freshness before migration and run restore-to-new-database validation after deployment. | Backup evidence JSON, restore validation evidence JSON, and backup/restore metrics. |
| Rollback owner | Record the pilot rollback owner, decision deadline, previous image digest, rollback command, database restore boundary, and communication route. | Release manifest and rollback note. |
| Alert routing | Fire or simulate the pilot route test with `memorysystem_alert_route_test{environment="pilot"} == 1`, verify owner, destination, silence policy, and runbook link. | Alert route-test result and receiver acknowledgement. |
| Audit evidence | Upload migration, health, metrics, benchmark, backup/restore, alert routing, approval, and rollback decision evidence to `release_evidence_bucket`. | Evidence object prefix and retention policy. |

Pilot exit criteria:

- zero unauthorized scoped-safety leaks
- zero stale-memory usage in the benchmark gate
- all page-level alert routes have named owners and runbook links
- restore validation succeeds against a fresh database
- rollback owner signs the go/no-go record

## Production Release Checklist

Production releases require pilot evidence plus stricter approval, rollback,
and audit retention. A production release cannot proceed only on local or CI
evidence.

| Gate | Required production check | Evidence |
| --- | --- | --- |
| Migration | Confirm pilot migrator evidence, review forward-only SQL migrations, take or confirm a fresh backup or PITR recovery point, then run the production migrator one-shot role. | Approval record, backup/PITR timestamp, migrator task id, exit code, and migration output. |
| Health | Verify API `/health/live`, API `/health/ready`, worker heartbeat freshness, authenticated read smoke, write-path smoke when applicable, and no elevated 5xx or readiness failures after rollout. | Health output, smoke output, dashboard snapshot, and incident channel note. |
| Metrics | Confirm OpenTelemetry exporter health, `/api/operations/metrics`, PostgreSQL health, outbox age, dead-letter count, retrieval feedback metrics, review/vault/governance metrics, and backup/restore metrics. | Metrics snapshot and dashboard link. |
| Benchmark gate | Attach the final `scripts/benchmark-release-gate.sh` report for the production-intended model and fixtures. The release fails on unauthorized leaks, stale-memory usage, missing source-link coverage, or failed agent-contract smoke. | Final benchmark report and go/no-go summary. |
| Backup/restore | Attach managed PostgreSQL backup/PITR status, latest backup export evidence, latest restore-to-new-database validation result, restore rehearsal window, and known data-loss window. | Backup/restore evidence and recovery-point note. |
| Rollback owner | Record the named rollback owner, escalation fallback, previous image digest, rollback command, database restore boundary, and the exact decision time after deployment. | Signed release manifest. |
| Alert routing | Verify page, ticket, and info alert routes, current on-call owner, escalation fallback, silence policy, runbook links, and `memorysystem_alert_route_test{environment="production"} == 1`. | Alert route-test evidence and receiver acknowledgement. |
| Audit evidence | Store release approval, migration, health, metrics, benchmark, backup/restore, alert routing, rollback owner, and final go/no-go evidence in the controlled audit store or `release_evidence_bucket`. | Immutable evidence prefix and retention period. |

Production exit criteria:

- pilot release evidence is attached and current
- backup/PITR evidence predates migration
- restore validation is recent and passing
- benchmark gate passes with no safety failures
- alert route tests have acknowledged receivers
- rollback owner signs the final release record

## Rollback Decision Rules

Rollback is owned by the named rollback owner, not by the person running the
last command. The owner should stop the release and start rollback when any of
these conditions hold:

- migration fails or leaves the schema in an unknown state
- API readiness or worker heartbeat does not recover inside the release window
- metrics show sustained 5xx, dead-letter growth, outbox age growth, or missing
  telemetry after deployment
- benchmark gate fails a safety threshold
- backup/restore validation cannot prove a recoverable database state
- alert route tests fail or no receiver acknowledges a page-level alert
- required audit evidence cannot be retained

## Post-Rehearsal Handoff

PI-08 used this checklist as the rehearsal script for an isolated pilot
environment. See [Production Platform Rehearsal PI-08](production-platform-rehearsal-pi08.md)
for the completed pilot checklist, local evidence notes, and rollback decision.
Future external pilot rehearsals should attach the release evidence object
prefix and every rollback decision made during the run.
