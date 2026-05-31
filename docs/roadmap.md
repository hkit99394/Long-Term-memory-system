# Long-Term Memory System Roadmap

## Roadmap Purpose

This roadmap turns the architecture plan into delivery milestones. The milestones are ordered around risk reduction: prove the durable write path first, then retrieval, role-aware context, human review, and operations.

## Current Track

Current milestone: Middle Run production-pilot hardening baseline is complete;
Long Run gate scoping is active.

Completed milestones: M0 Planning Baseline through M8 Operational Readiness.

Next milestone: production platform integration implementation, remaining
Domain extraction cleanup, and enterprise access implementation.

The first production-shaped win is in place: a local API and database can accept an event, broker a memory proposal, persist memory with provenance, enforce scoped reads, and return authorized hybrid context packets.

The production-pilot docs define the target deployment and observability shape.
MR-10 adds executable deployment proof for the local migrator/API/worker split,
rollback, and restore-validation path. MR-11 adds versioned alert rules,
dashboard definitions, trace coverage, and observability smoke checks. MR-12
adds benchmark release gates for Memory Lift, Contract Lift, scoped-safety
leaks, stale-memory usage, source-link coverage, and agent-contract smoke.
MR-08 operator evidence browsing and MR-09 governance automation are also
implemented. LR-01, LR-02, LR-04, LR-05, PI-01, PI-02, PI-03, and PI-04 now
define implementable gate backlogs, platform baseline choices, the first
Terraform platform layout, managed PostgreSQL/pgvector resources, and
backup/restore automation for enterprise access, context productization, Domain
extraction, and production platform integration.

## Milestones

| Milestone | Theme | Outcome | Exit Criteria |
| --- | --- | --- | --- |
| M0 | Planning Baseline | Architecture, roadmap, and backlog are clear enough to build from. | Project goal, system plan, roadmap, backlog, and first M1-M6 scenario exist; Phase 1 decisions are named. |
| M1 | Foundation Slice | The backend skeleton and database can run locally. | .NET solution starts; Postgres with pgvector runs; first migration applies; health endpoints pass. |
| M2 | Provenance Write Path | Durable memory writes are brokered, auditable, and idempotent. | `POST /api/events` and `POST /api/memory/proposals` work; source events are required; request idempotency returns stable retries. |
| M3 | Access and Scope Enforcement | Memory cannot cross user, project, role, or agent boundaries accidentally. | Resolved principals from M2 are enforced through memberships and grants; cross-project reads are blocked in tests. |
| M4 | Structured and Role Memory | User, project, agent-private, shared-role, and project-role memory are distinct. | Repositories and lifecycle rules exist; shared role principles cannot use project-specific facts; expired/deleted/superseded facts are excluded. |
| M5 | Broker Intelligence | The broker separates durable memory from temporary instructions. | Candidate classification, dedupe, contradiction checks, confidence, and review-required decisions are tested. |
| M6 | Hybrid Retrieval | Context packets combine structured, keyword, and vector recall safely. | Full-text and pgvector search run inside authorized predicates; ranking is explainable; context packets include source links. |
| M7 | Review and Vault Workflow | Humans can inspect, approve, correct, export, and remove memories. | Review dashboard supports approve/reject/edit/expire/delete/supersede; Obsidian export is source-linked and stale-aware. |
| M8 | Operational Readiness | The system can be run, observed, backed up, and recovered. | Health checks, structured logs, retention policy, backup/restore notes, and production secret handling are documented and guarded at runtime. |

## MVP Boundary

The MVP ends at M6.

MVP must demonstrate this loop:

```text
Observe event
Extract candidate memory
Broker validates write
Store structured memory
Index for retrieval
Build scoped context
Return compact context packet
```

Review UI, vault sync, and production operations are important, but they should not block proof of the core memory loop.

## Milestone Dependencies

| Dependency | Needed By | Reason |
| --- | --- | --- |
| Local Postgres plus pgvector | M1 | Migrations and vector schema must be tested against the real extension. |
| API idempotency model | M2 | Event and proposal writes must be retry-safe before agents depend on them. |
| Scope and namespace validation | M3 | Retrieval safety depends on consistent stored scope metadata. |
| Role-lens validation | M4 | Shared role memory must not leak project facts across projects. |
| Broker write transaction | M5 | Classification and review decisions must commit with evidence and outbox jobs. |
| Authorized retrieval query shape | M6 | Full-text and vector search must not rank unauthorized candidate sets. |
| Redaction and stale-export model | M7 | Human review and vault export must honor deletion and erasure decisions. |
| Retention policy | M8 | Operational readiness needs a clear answer for raw event payload handling. |

## Decision Gates

Before M1 implementation:

- Data access is decided in [Decision 0001](decisions/0001-data-access-approach.md): SQL-first migrations plus raw Npgsql for the M1-M3 initial backend path, with Dapper allowed only as a small mapping convenience and EF Core deferred.
- Migration runner approach is decided in [Decision 0002](decisions/0002-migration-runner-approach.md): a small in-repo Npgsql-based runner applies ordered root-level SQL migrations for local development and integration tests.
- Local database runtime is decided in [Decision 0003](decisions/0003-local-database-runtime.md): `pgvector/pgvector:0.8.2-pg17-bookworm` for M1-M3 local development and integration tests.
- The first real scenario is confirmed in [Scenario 0001](scenarios/0001-user-preference-project-decision-cto-context.md): user preference plus project decision plus CTO role context, used as the M1-M6 throughline.

Before M2 implementation:

- Request hash rules for API idempotency are defined in [Decision 0004](decisions/0004-api-idempotency-request-hash.md).
- First event content shapes are defined in [Decision 0005](decisions/0005-event-append-contract.md) and grounded in [Scenario 0001](scenarios/0001-user-preference-project-decision-cto-context.md).
- Raw event payloads are stored inline for M2, as defined in [Decision 0005](decisions/0005-event-append-contract.md).

Before M4 implementation:

- Define validation for `role_memory_lenses.base_memory_fact_id`.
- Define canonical namespace parsing rules.

Before M6 implementation:

- Embedding provider and vector dimension are decided in [Decision 0024](decisions/0024-embedding-provider-adapter.md): deterministic embeddings remain the Development/Testing adapter, while non-testing semantic retrieval and indexing require a production provider. The current production provider is OpenAI embeddings with `text-embedding-3-small`, 1536 dimensions, and a HTTPS endpoint by default.
- Vector distance operator is decided in [Decision 0025](decisions/0025-authorized-pgvector-semantic-search.md): pgvector cosine distance remains the retrieval operator. Migration 017 removes the provisional 32-dimensional HNSW index; production OpenAI rows continue to use the `(embedding_model, embedding_dimension)` narrowing index until a model-specific ANN index is chosen.
- Hybrid ranking formula is decided in [Decision 0026](decisions/0026-hybrid-memory-ranking.md): relevance, confidence, recency, authority, and scope match combine into the MVP final score.
- Context packet size and source-link expectations are decided in [Decision 0027](decisions/0027-context-packet-builder.md): packets cap at 12 memories, group by memory category, and include source event ids and links to the event-read endpoint.
- Retrieval evaluation metrics are decided in [Decision 0028](decisions/0028-retrieval-evaluation-tests.md): deterministic tests measure relevance, compactness, write precision, false positives, and contradiction quality.

Before M7 implementation:

- Pending review queue behavior is decided in [Decision 0029](decisions/0029-pending-review-api.md): `GET /api/reviews/pending` lists pending memory reviews only when the principal has review permission for the memory scope and namespace.
- Review dashboard behavior is decided in [Decision 0030](decisions/0030-review-dashboard.md): the TypeScript dashboard serves at `/reviews/` and completes approve, reject, edit, expire, delete, and supersede workflows through provenance-backed review action endpoints.
- Obsidian export behavior is decided in [Decision 0031](decisions/0031-obsidian-export.md): `GET /api/vault/exports/obsidian` renders approved decisions and summaries with source IDs, and `tools/vault-sync` writes those documents to the export-only vault.
- Stale vault export behavior is decided in [Decision 0032](decisions/0032-stale-vault-exports.md): Obsidian exports are tracked in PostgreSQL, and deleted, redacted, expired, superseded, or contradicted memories return audit-safe stale marker documents.
- Archive vault export behavior is decided in [Decision 0033](decisions/0033-archive-vault-exports.md): `GET /api/vault/exports/obsidian/archive` exports readable superseded, expired, and contradicted memory under `90 Archive/`.

Before M8 implementation:

- Operational health behavior is decided in [Decision 0034](decisions/0034-operational-health-checks.md): `/health/live` stays process-only, while readiness reports PostgreSQL, outbox backlog, outbox worker heartbeat freshness, and embedding provider usability without probing billable embedding APIs.
- Structured logging behavior is decided in [Decision 0035](decisions/0035-structured-operational-logging.md): proposal, retrieval, review, and delete/expire redaction logs include operational metadata and counts, but omit proposal content, query text, review notes, memory body text, and raw event payloads.
- Retention and erasure behavior is decided in [Decision 0036](decisions/0036-retention-and-erasure-policy.md): raw event payload retention classes, legal hold precedence, erasure workflow expectations, audit-safe metadata, and current automation gaps are documented in [Retention Policy](retention-policy.md).
- Backup and restore behavior is decided in [Decision 0037](decisions/0037-backup-and-restore-runbook.md): PostgreSQL custom-format backups, restore validation databases, migration checks, readiness checks, and retention-aware restore caveats are documented in [Backup and Restore Runbook](backup-restore.md).
- Production secret handling is decided in [Decision 0038](decisions/0038-production-secret-handling.md): API keys, PostgreSQL credentials, and embedding provider credentials are supplied from runtime configuration or a secret store, with startup guardrails documented in [Production Secret Handling](production-secrets.md).

After M8:

- Production-pilot deployment shape is decided in [Decision 0039](decisions/0039-production-deployment-shape.md): the production pilot uses separate migrator, API, and worker roles, managed PostgreSQL with pgvector, secret-store injection, application rollback, and restore-to-new-database validation documented in [Production Deployment Shape](production-deployment-shape.md).
- Production observability and alerting is decided in [Decision 0040](decisions/0040-production-observability-and-alerting.md): the production pilot tracks API, write-path, retrieval, worker, PostgreSQL, governance, backup, and benchmark-smoke signals with payload-safe traces/logs, high-signal alerts, and operator runbook actions documented in [Production Observability and Alerting](production-observability.md).
- Benchmark release gates are decided in [Decision 0041](decisions/0041-benchmark-release-gates.md): production-pilot release verification combines LLM outcome scoring, agent-contract scoring, source-link coverage, stale-memory usage, scoped-safety counters, and agent-contract smoke.
- The first metrics export is implemented in MR-06: `/api/operations/metrics`
  exposes authenticated Prometheus-compatible request, readiness, outbox,
  worker-heartbeat, retrieval-feedback, governance, and embedding-index failure
  metrics, with `scripts/operations-metrics-smoke.sh` as the local alert-input
  smoke check.
- The first admin console slice is implemented in MR-07: `/admin/` browses
  authorized memory facts, lifecycle state, confidence, safe source policy
  metadata, and explicit source evidence opens through the existing event-read
  endpoint.
- MR-08 expands the admin console with source event search, policy and
  time-window filters, linked memory/review/export/redaction references, and
  audit-safe redaction or erasure state without including source payloads in
  the list.
- MR-09 adds authenticated governance workflow endpoints for legal-hold
  create/release/reporting, erasure execution over source events and derived
  copies, and retention reports by namespace, retention class, sensitivity, and
  age.
- MR-10 adds `scripts/production-pilot-deployment-smoke.sh`, which publishes
  the migrator, API, and worker roles, runs them against an isolated PostgreSQL
  target, validates health/read/write/operator paths, restores into a fresh
  database, and re-points API and worker at the restored database.
- MR-11 adds versioned observability artifacts under `observability/`:
  Prometheus-compatible alert rules, a Grafana-compatible pilot dashboard, a
  trace coverage manifest, API and external metric input manifests, and local
  artifact/live-metric smoke checks.
- MR-12 adds `benchmarks/release-gate/run_release_gate.py` and
  `scripts/benchmark-release-gate.sh`, which combine filled LLM outcome
  scorecards, filled agent-contract scorecards, source-link coverage counters,
  stale-memory usage counters, scoped-safety counters, and agent-contract smoke
  output into a pass/fail production-pilot release report.
- Domain model extraction is decided in [Decision 0044](decisions/0044-domain-model-extraction-slice.md):
  stable IO-free concepts move into `MemorySystem.Domain` through
  compatibility-tested `DM-*` slices without schema or endpoint churn.
- Production platform integration is decided in [Decision 0045](decisions/0045-production-platform-integration.md):
  infrastructure-as-code boundaries, managed PostgreSQL and backup exporter
  assumptions, runtime OpenTelemetry/exporter wiring, alert routing, and
  environment-specific release checklists are scoped before platform-specific
  implementation starts.
- Production platform and IaC baseline is decided in [Decision 0046](decisions/0046-production-platform-and-iac-baseline.md):
  the first pilot target is AWS ECS Fargate plus Amazon RDS PostgreSQL with
  pgvector, Amazon ECR, Terraform under `infra/terraform`, Secrets Manager or
  SSM references, and immutable multi-role OCI image digests.
- PI-02 adds `infra/terraform` with pilot/production overlays, runtime,
  PostgreSQL, and observability module contracts, plus a root `Dockerfile` for
  the immutable multi-role OCI image.
- PI-03 turns the PostgreSQL module into managed RDS PostgreSQL resources with
  private subnet placement, client ingress controls, RDS-managed master
  credential material, backup/PITR settings, and pgvector validation metadata.
- PI-04 adds platform backup/export and restore-validation job commands that
  emit evidence JSON plus alertable `memorysystem_backup_*` and
  `memorysystem_restore_validation_*` metrics.

## First Build Sequence

1. Create the .NET solution and project layout.
2. Add Docker Compose for Postgres plus pgvector.
3. Add `migrations/001_initial_memory_schema.sql`.
4. Add a migration runner path for local development and tests.
5. Implement `GET /health/live`, `GET /health/ready`, and `GET /health`.
6. Implement API-key principal resolution.
7. Implement `POST /api/events` with request idempotency.
8. Implement `POST /api/memory/proposals` with a minimal broker decision.
9. Add integration tests for migration, event append, proposal write, idempotent retry, and blocked cross-project read.
