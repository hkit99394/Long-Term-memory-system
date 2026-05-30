# Long-Term Memory System Roadmap

## Roadmap Purpose

This roadmap turns the architecture plan into delivery milestones. The milestones are ordered around risk reduction: prove the durable write path first, then retrieval, role-aware context, human review, and operations.

## Current Track

Current milestone: Middle Run production-pilot hardening is underway.

Completed milestones: M0 Planning Baseline through M8 Operational Readiness.

Next milestone: production metrics export and alert smoke checks.

The first production-shaped win is in place: a local API and database can accept an event, broker a memory proposal, persist memory with provenance, enforce scoped reads, and return authorized hybrid context packets.

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
