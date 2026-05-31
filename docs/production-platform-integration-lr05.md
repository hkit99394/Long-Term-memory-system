# LR-05 Production Platform Integration Plan

Date: 2026-05-31

Status: Planning accepted

## Goal

LR-05 defines the production platform integration boundary after the
production-pilot baseline. It turns the MR-10 deployment shape and MR-11
observability artifacts into an implementation plan for infrastructure-as-code,
managed PostgreSQL, backup/export validation, runtime OpenTelemetry wiring,
alert routing, and environment-specific release checklists.

This is a scoping slice. It should make the next platform work concrete without
choosing a cloud provider, changing the database schema, or changing public API
contracts.

## Non-Goals

- Do not choose a vendor or managed runtime in this slice.
- Do not introduce new migrations, endpoints, DTOs, or auth behavior.
- Do not commit secret values, production credentials, or provider-specific
  account identifiers.
- Do not replace the local production-pilot smoke scripts.
- Do not move SQL schema ownership out of `migrations/`.

## Current Architecture Evidence

The repo already has the pieces a platform integration plan can build on:

- [Production Deployment Shape](production-deployment-shape.md) defines the
  migrator, API, and worker runtime split, managed PostgreSQL expectations,
  rollback procedure, and restore validation path.
- [Production Observability and Alerting](production-observability.md) defines
  required metrics, payload-safe tracing and logging, alert inputs, dashboard
  minimums, and first-response operator actions.
- [Backup and Restore Runbook](backup-restore.md) defines custom-format backup,
  restore-to-new-database validation, and retention-aware recovery rules.
- `scripts/production-pilot-deployment-smoke.sh` proves the local role split,
  health checks, write/read smoke, rollback shape, backup, and restore
  validation path.
- `observability/` contains versioned pilot alert rules, dashboard definitions,
  metric input manifests, and a trace coverage manifest.

Current gaps remain intentionally visible: the repo does not yet contain
platform infrastructure-as-code, runtime OpenTelemetry exporters, alert routing
configuration, or managed PostgreSQL and backup exporter wiring.

## Target Platform Boundary

| Area | IaC owns | Application owns | Operator/runbook owns |
| --- | --- | --- | --- |
| Runtime roles | Migrator job, API service, worker service, scaling, resource limits, ingress/TLS attachment | Published binaries, host configuration, health endpoints, role-specific startup behavior | Deployment approval, rollback decision, incident ownership |
| Database | Managed PostgreSQL instance, pgvector availability, network access, credential references, backup/PITR settings | SQL migrations under `migrations/`, repository queries, readiness checks | Restore approval, validation evidence, break-glass recovery |
| Secrets | Secret references and injection wiring | Configuration names and startup guardrails | Secret creation, rotation schedule, emergency revocation |
| Observability | Metrics/tracing/log exporters, dashboard provisioning, alert rule installation, alert routing | Payload-safe metrics, logs, trace attributes, health and operations endpoints | SLO review, alert ownership, silencing and escalation |
| Release gates | Environment parameters, checklist artifacts, smoke-run hooks | Smoke scripts, benchmark gate, migration runner, health responses | Go/no-go decision, audit record, external communication |

The platform layer can wrap the service, but it must not redefine memory
policy, grant semantics, schema history, retention behavior, or payload-safety
rules.

## Infrastructure-As-Code Boundary

The future IaC baseline should:

- provision separate migrator, API, and worker roles
- attach ingress, TLS, trusted forwarding, health checks, and resource limits
- define environment-specific parameters for local, CI, pilot, and production
- reference secret-store entries without storing secret values
- provision or attach managed PostgreSQL with pgvector support
- configure backup/export jobs and restore-validation targets
- install metrics, tracing, log exporters, dashboards, alert rules, and alert
  routing hooks
- keep SQL migrations owned by `migrations/` and executed through
  `MemorySystem.Migrator`
- keep benchmark outputs and generated local reports outside platform state

No `infra/` directory is required before a platform is selected. When PI-02
starts, the chosen IaC code should live under `infra/` or a similarly explicit
platform-owned folder, with environment overlays separated from application
source projects.

## Managed PostgreSQL Assumptions

The first production platform target must provide PostgreSQL with:

- `pgvector` extension support
- encrypted storage and encrypted backups
- point-in-time recovery when the provider supports it
- a dedicated memory-system credential or identity
- network isolation from general public access
- backup age, backup success, connection, CPU, storage, lock, and availability
  metrics
- a path to restore into a separate validation database

PostgreSQL remains the recovery authority for source events, memory facts,
chunks, embeddings, reviews, idempotency records, outbox jobs, feedback,
governance actions, vault export tracking, and worker heartbeats.

## Backup Exporter Assumptions

The backup exporter can be provider-native or a service-owned logical dump path,
but it must produce alertable evidence:

- latest successful backup or export timestamp
- backup age against the recovery policy
- restore validation status and timestamp
- backup/export failure count
- validation database identifier for the last rehearsal
- operator or pipeline run id for the last backup and restore validation

Restore validation should follow the existing runbook: restore to a new
database, run the migrator, verify core table counts and `vector` extension
availability, start isolated API and worker processes when possible, and prove
an authenticated read path before any connection-string switch.

## Runtime OpenTelemetry And Exporter Wiring

PI work should add runtime OpenTelemetry for API and worker without changing the
payload-safety contract.

Required wiring:

- service name, environment, version, and instance id on telemetry resources
- correlation or trace id in logs once tracing is enabled
- spans that match `observability/tracing/memorysystem-pilot-trace-coverage.json`
- exporters for traces, metrics, and logs selected by the platform
- opt-in local settings so tests and local development do not require external
  collectors
- payload-safe attributes only: ids, counts, route names, status values, scope
  type, role id, memory type, lifecycle state, trust level, and hashed query
  values when needed

Forbidden telemetry content remains raw memory text, raw query text, raw event
payloads, proposal notes, review notes, API keys, embedding input text, and
secret values.

## Alert Routing

Alert rules alone are not enough for production. Platform integration must route
alerts to owners with clear severity and runbook links.

| Severity | Examples | Routing requirement |
| --- | --- | --- |
| Page | API readiness failure, managed PostgreSQL unavailable, stale worker heartbeat, dead letters above zero, backup missing beyond recovery policy | On-call or named release owner with an immediate runbook link |
| Ticket | Elevated latency, retrieval feedback drift, stale vault exports, benchmark delta missing after release, repeated auth failures above baseline | Product or platform queue with owner and triage SLA |
| Info | Release gate started, smoke passed, backup validation passed, benchmark report recorded | Release record or operations channel |

Each environment must define the owner, route destination, silence policy, test
route procedure, and escalation fallback before it is considered release-ready.

## Environment Release Checklists

### Local And CI

- `dotnet build MemorySystem.sln --no-restore`
- unit tests
- integration tests when PostgreSQL is available
- observability artifact smoke
- production-pilot deployment smoke when Docker PostgreSQL is available
- benchmark release gate dry run with filled scorecards

### Pilot

- artifact or image version recorded
- migrator one-shot role completed
- API `/health/live` and `/health/ready` pass
- worker heartbeat is current
- `/api/operations/summary` and `/api/operations/metrics` are reachable
- alert route test passes
- backup or restore point is current before migration
- restore-to-new-database validation has a recent passing record
- authenticated read path passes
- write-path smoke passes for releases touching writes, broker policy, context,
  indexing, or governance
- benchmark release gate report is attached to the release record

### Production

- all pilot checks pass
- change approval and rollback owner are recorded
- managed PostgreSQL backup/PITR status is attached
- alert owners and escalation fallback are current
- runtime OpenTelemetry exporter health is visible
- secrets were rotated or reviewed according to the release policy
- benchmark release gate passes with zero scoped leaks and zero stale-memory
  usage
- restore rehearse window and known data-loss window are documented
- release audit record includes operator, version, migration output, health
  result, smoke result, backup evidence, and rollback decision

## Implementation Backlog

The LR-05 planning output creates the `PI-*` implementation backlog:

- `PI-01`: choose production platform and IaC baseline. Done in
  [Production Platform Baseline PI-01](production-platform-baseline-pi01.md)
  and [Decision 0046](decisions/0046-production-platform-and-iac-baseline.md).
- `PI-02`: add IaC skeleton for runtime roles. Done in `infra/terraform`.
- `PI-03`: provision managed PostgreSQL with pgvector. Done in
  `infra/terraform/modules/memorysystem-postgres`.
- `PI-04`: add backup exporter and restore validation automation. Done with
  `scripts/platform-backup-export.sh`,
  `scripts/platform-restore-validation.sh`, and runtime job contracts.
- `PI-05`: wire runtime OpenTelemetry exporters
- `PI-06`: connect alert routing and runbook links
- `PI-07`: add environment-specific release checklists
- `PI-08`: run first platform rehearsal

Each slice should keep API contracts, SQL schema history, and benchmark gates
stable unless a separate decision explicitly changes them.

## Risk Register

| Risk | Mitigation |
| --- | --- |
| IaC drift from documented runtime roles | Make migrator/API/worker split explicit in IaC tests or platform smoke output. |
| Backup success without restore confidence | Require restore-to-new-database validation evidence before pilot or production release. |
| Telemetry payload leakage | Reuse the trace coverage manifest and payload-safe attribute policy as a testable contract. |
| Alert rules without routing | Treat route owner, destination, runbook link, and test route as release checklist items. |
| Platform state owning application policy | Keep schema, retention rules, grants, and broker policy in application docs and code. |
| Environment checklist skipped under pressure | Attach checklist output to release records and benchmark reports. |

## Exit Criteria

LR-05 is complete when:

- this plan exists and is linked from the documentation index
- Decision 0045 accepts the production platform integration boundary
- `docs/backlog.md` marks LR-05 done and adds `PI-*` follow-on work
- a doc guard test verifies the plan, decision, backlog, and index links
- no schema, endpoint, or runtime behavior changes are introduced by the slice
