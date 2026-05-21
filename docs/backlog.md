# Long-Term Memory System Backlog

## Backlog Purpose

This backlog translates the roadmap into buildable work items. Items are grouped by milestone and written with acceptance criteria so they can become issues, tickets, or implementation checklists.

Status values:

- `Todo`: not started.
- `Doing`: actively in progress.
- `Done`: implemented and verified.
- `Blocked`: waiting on a decision or dependency.

## M0 Planning Baseline

| ID | Priority | Status | Item | Acceptance Criteria |
| --- | --- | --- | --- | --- |
| M0-01 | P0 | Done | Define project goal. | `docs/project-goal.md` states the north star and short version. |
| M0-02 | P0 | Done | Define architecture plan. | `docs/long-term-memory-system-plan.md` covers stack, architecture, schema direction, API, phases, and risks. |
| M0-03 | P0 | Done | Resolve document review findings. | Permission-filtered retrieval, retention/redaction, scope constraints, role-lens semantics, and API idempotency are reflected in the plan. |
| M0-04 | P0 | Done | Define roadmap and backlog. | Roadmap and backlog documents exist and are linked from the documentation index. |
| M0-05 | P0 | Done | Confirm Phase 1 data-access approach. | [Decision 0001](decisions/0001-data-access-approach.md) selects SQL-first migrations plus raw Npgsql for the M1-M3 initial backend path. |
| M0-06 | P0 | Done | Confirm migration runner approach. | [Decision 0002](decisions/0002-migration-runner-approach.md) selects a small in-repo Npgsql-based migration runner for local development and integration tests. |
| M0-07 | P0 | Done | Confirm local Docker image and pgvector version. | [Decision 0003](decisions/0003-local-database-runtime.md) selects `pgvector/pgvector:0.8.2-pg17-bookworm` for M1-M3 local development and integration tests. |
| M0-08 | P1 | Done | Confirm first scenario. | [Scenario 0001](scenarios/0001-user-preference-project-decision-cto-context.md) defines the M1-M6 throughline: user preference plus project decision plus CTO role context. |
| M0-09 | P1 | Done | Define folder structure and architecture overview. | `docs/folder-structure.md` and `docs/architecture.md` exist and are linked from the documentation index. |

## M1 Foundation Slice

| ID | Priority | Status | Item | Acceptance Criteria |
| --- | --- | --- | --- | --- |
| M1-01 | P0 | Done | Create .NET solution. | `MemorySystem.sln` contains API, Application, Domain, Infrastructure, Worker, UnitTests, and IntegrationTests projects. |
| M1-02 | P0 | Done | Add local database runtime. | `docker-compose.yml` starts PostgreSQL with pgvector enabled. |
| M1-03 | P0 | Done | Add first migration file. | `migrations/001_initial_memory_schema.sql` creates identity, access, event, memory, lens, chunk, embedding, review, redaction, idempotency, and outbox tables. |
| M1-04 | P0 | Done | Add migration runner path. | `MemorySystem.Infrastructure` owns an Npgsql migration runner with checksums and advisory locking; `MemorySystem.Migrator` and integration tests can apply migrations repeatably. |
| M1-05 | P0 | Done | Implement health endpoint. | `GET /health` returns healthy when API starts and PostgreSQL is reachable. |
| M1-06 | P1 | Done | Add basic CI-ready test commands. | `docs/testing.md` documents restore, build, default test, and database-backed integration test commands that run locally and can be used in CI. |

## M2 Provenance Write Path

| ID | Priority | Status | Item | Acceptance Criteria |
| --- | --- | --- | --- | --- |
| M2-01 | P0 | Done | Implement API-key principal resolution. | Requests map local API keys to active principals. |
| M2-02 | P0 | Done | Implement request idempotency. | Mutating endpoints store principal, endpoint, idempotency key, request hash, response, and expiry. |
| M2-03 | P0 | Done | Implement event append endpoint. | `POST /api/events` stores an event and returns an id; retry with the same idempotency key returns the original response. |
| M2-04 | P0 | Done | Implement minimal broker decision. | `POST /api/memory/proposals` returns `stored`, `rejected`, `review_required`, or `session_only`. |
| M2-05 | P0 | Done | Implement transactional memory write. | Stored proposals commit source event, memory fact, chunk, outbox job, and broker response in one transaction. |
| M2-06 | P1 | Done | Add provenance tests. | Durable memory cannot be stored without a source event. |

## M3 Access and Scope Enforcement

| ID | Priority | Status | Item | Acceptance Criteria |
| --- | --- | --- | --- | --- |
| M3-01 | P0 | Todo | Implement scope resolver. | Requests resolve user, organization, project, role, agent, and session scope. |
| M3-02 | P0 | Todo | Implement membership and grant checks. | Fine-grained read/write/review/admin decisions use memberships, role assignments, and memory grants. |
| M3-03 | P0 | Todo | Enforce memory fact scope consistency. | `scope_type`, `scope_id`, namespace, and owner columns cannot drift. |
| M3-04 | P0 | Todo | Add blocked cross-project read test. | A principal in Project A cannot read Project B memory without explicit access. |
| M3-05 | P1 | Todo | Add namespace parser. | Namespace strings are parsed and validated against scope metadata. |

## M4 Structured and Role Memory

| ID | Priority | Status | Item | Acceptance Criteria |
| --- | --- | --- | --- | --- |
| M4-01 | P0 | Todo | Implement memory facts repository. | Stores and retrieves user preference, project decision, role-scoped memory, and agent-private memory. |
| M4-02 | P0 | Todo | Implement memory status lifecycle. | Active, tentative, superseded, contradicted, expired, deleted, and redacted states are represented and filtered correctly. |
| M4-03 | P0 | Todo | Implement role memory lens repository. | Shared role principles and project-role lenses are stored separately. |
| M4-04 | P0 | Todo | Validate role-lens base fact scope. | Shared role principles cannot reference project facts; project-role lenses reference only target project or org facts. |
| M4-05 | P1 | Todo | Add simple structured search. | Search by scope, type, subject, and status works without vector retrieval. |

## M5 Broker Intelligence

| ID | Priority | Status | Item | Acceptance Criteria |
| --- | --- | --- | --- | --- |
| M5-01 | P0 | Todo | Add candidate classification. | Broker distinguishes preference, project fact, decision, role lens, agent-private memory, and session-only instruction. |
| M5-02 | P0 | Todo | Add session-only rejection path. | One-off task instructions do not become durable memory. |
| M5-03 | P0 | Todo | Add deduplication. | Similar active memories are updated, ignored, or reviewed rather than blindly duplicated. |
| M5-04 | P0 | Todo | Add contradiction detection. | Conflicting memories are flagged, superseded, or sent to review. |
| M5-05 | P1 | Todo | Add confidence scoring. | Broker assigns confidence and review-required state based on evidence and trust level. |

## M6 Hybrid Retrieval

| ID | Priority | Status | Item | Acceptance Criteria |
| --- | --- | --- | --- | --- |
| M6-01 | P0 | Todo | Add full-text search. | `memory_chunks.search_vector` is populated and queried inside authorized predicates. |
| M6-02 | P0 | Todo | Add embedding provider adapter. | Chunks can be embedded with a selected model and dimension. |
| M6-03 | P0 | Todo | Add pgvector semantic search. | Vector queries use the chosen distance operator and never rank unauthorized rows. |
| M6-04 | P0 | Todo | Implement hybrid ranking. | Relevance, confidence, recency, authority, and scope match are combined into a final score. |
| M6-05 | P0 | Todo | Implement context packet builder. | Context packet is compact, source-linked, permission-aware, and explainable. |
| M6-06 | P1 | Todo | Add retrieval evaluation tests. | Relevance, compactness, write precision, false positives, and contradiction quality can be measured. |

## M7 Review and Vault Workflow

| ID | Priority | Status | Item | Acceptance Criteria |
| --- | --- | --- | --- | --- |
| M7-01 | P0 | Todo | Build pending review API. | Pending memories can be listed with source event links. |
| M7-02 | P0 | Todo | Build review dashboard. | TypeScript UI supports approve, reject, edit, expire, delete, and supersede workflows. |
| M7-03 | P0 | Todo | Implement Obsidian export. | Approved summaries and decisions export with source IDs. |
| M7-04 | P0 | Todo | Handle stale exports. | Deleted or redacted memory marks vault exports stale or regenerates them. |
| M7-05 | P1 | Todo | Add archive export. | Old or superseded memory can be exported in a readable archive format. |

## M8 Operational Readiness

| ID | Priority | Status | Item | Acceptance Criteria |
| --- | --- | --- | --- | --- |
| M8-01 | P0 | Todo | Add operational health checks. | API reports database, worker, and embedding provider health. |
| M8-02 | P0 | Todo | Add structured logging. | Broker decisions, retrieval decisions, and redaction actions are logged without leaking sensitive payloads. |
| M8-03 | P0 | Todo | Add retention policy. | Raw event payload retention, erasure, legal hold, and audit preservation are documented. |
| M8-04 | P0 | Todo | Add backup and restore notes. | Database backup and restore process is documented and tested locally. |
| M8-05 | P1 | Todo | Add production secret handling. | API keys, connection strings, and embedding provider credentials are configured safely. |

## Immediate Next Items

Continue here:

1. `M3-01`: Implement scope resolver.
