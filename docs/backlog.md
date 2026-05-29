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
| M1-05 | P0 | Done | Implement health endpoints. | `GET /health/live`, `GET /health/ready`, and `GET /health` expose process, readiness, and aggregate health behavior. |
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
| M3-01 | P0 | Done | Implement scope resolver. | Requests resolve user, organization, project, role, agent, and session scope. |
| M3-02 | P0 | Done | Implement membership and grant checks. | Fine-grained read/write/review/admin decisions use memberships, role assignments, and memory grants. |
| M3-03 | P0 | Done | Enforce memory fact scope consistency. | `scope_type`, `scope_id`, namespace, and owner columns cannot drift. |
| M3-04 | P0 | Done | Add blocked cross-project read test. | A principal in Project A cannot read Project B memory without explicit access. |
| M3-05 | P1 | Done | Add namespace parser. | Namespace strings are parsed and validated against scope metadata. |

## M4 Structured and Role Memory

| ID | Priority | Status | Item | Acceptance Criteria |
| --- | --- | --- | --- | --- |
| M4-01 | P0 | Done | Implement memory facts repository. | Stores and retrieves user preference, project decision, role-scoped memory, and agent-private memory. |
| M4-02 | P0 | Done | Implement memory status lifecycle. | Active, tentative, superseded, contradicted, expired, deleted, and redacted states are represented and filtered correctly. |
| M4-03 | P0 | Done | Implement role memory lens repository. | Shared role principles and project-role lenses are stored separately. |
| M4-04 | P0 | Done | Validate role-lens base fact scope. | Shared role principles cannot reference project facts; project-role lenses reference only target project or org facts. |
| M4-05 | P1 | Done | Add simple structured search. | Search by scope, type, subject, and status works without vector retrieval. |

## M5 Broker Intelligence

| ID | Priority | Status | Item | Acceptance Criteria |
| --- | --- | --- | --- | --- |
| M5-01 | P0 | Done | Add candidate classification. | Broker distinguishes preference, project fact, decision, role lens, agent-private memory, and session-only instruction. |
| M5-02 | P0 | Done | Add session-only rejection path. | One-off task instructions do not become durable memory. |
| M5-03 | P0 | Done | Add deduplication. | Similar active memories are updated, ignored, or reviewed rather than blindly duplicated. |
| M5-04 | P0 | Done | Add contradiction detection. | Conflicting memories are flagged, superseded, or sent to review. |
| M5-05 | P1 | Done | Add confidence scoring. | Broker assigns confidence and review-required state based on evidence and trust level. |

## M6 Hybrid Retrieval

| ID | Priority | Status | Item | Acceptance Criteria |
| --- | --- | --- | --- | --- |
| M6-01 | P0 | Done | Add full-text search. | `memory_chunks.search_vector` is populated and queried inside authorized predicates. |
| M6-02 | P0 | Done | Add embedding provider adapter. | Chunks can be embedded with a selected model and dimension. |
| M6-03 | P0 | Done | Add pgvector semantic search. | Vector queries use the chosen distance operator and never rank unauthorized rows. |
| M6-04 | P0 | Done | Implement hybrid ranking. | Relevance, confidence, recency, authority, and scope match are combined into a final score. |
| M6-05 | P0 | Done | Implement context packet builder. | Context packet is compact, source-linked, permission-aware, and explainable. |
| M6-06 | P1 | Done | Add retrieval evaluation tests. | Relevance, compactness, write precision, false positives, and contradiction quality can be measured. |

## M7 Review and Vault Workflow

| ID | Priority | Status | Item | Acceptance Criteria |
| --- | --- | --- | --- | --- |
| M7-01 | P0 | Done | Build pending review API. | Pending memories can be listed with source event links. |
| M7-02 | P0 | Done | Build review dashboard. | TypeScript UI supports approve, reject, edit, expire, delete, and supersede workflows. |
| M7-03 | P0 | Done | Implement Obsidian export. | Approved summaries and decisions export with source IDs. |
| M7-04 | P0 | Done | Handle stale exports. | Deleted or redacted memory marks vault exports stale or regenerates them. |
| M7-05 | P1 | Done | Add archive export. | Old or superseded memory can be exported in a readable archive format. |

## M8 Operational Readiness

| ID | Priority | Status | Item | Acceptance Criteria |
| --- | --- | --- | --- | --- |
| M8-01 | P0 | Done | Add operational health checks. | API readiness reports database, outbox backlog, worker heartbeat freshness, and embedding provider usability. |
| M8-02 | P0 | Done | Add structured logging. | Broker decisions, retrieval decisions, review actions, and delete/expire redaction actions are logged without proposal, query, note, or memory payload leakage. |
| M8-03 | P0 | Done | Add retention policy. | Raw event payload retention, erasure, legal hold, and audit preservation are documented. |
| M8-04 | P0 | Done | Add backup and restore notes. | Database backup and restore process is documented and tested locally. |
| M8-05 | P1 | Done | Add production secret handling. | API keys, connection strings, and embedding provider credentials are configured safely. |

## Immediate Next Items

M8 operational readiness backlog is complete. Short Run is complete. Middle Run
now has two connected tracks: production-pilot operations and the
agent-facing LLM Memory Support Service v1 contract. The first LMSS v1 contract
slice is documented, implemented, and benchmarkable; retrieval feedback is now
visible in the operator summary. Next up: complete the LMSS benchmark smoke
fixture, then define the production deployment shape.

## Middle Run Production Pilot

| ID | Priority | Status | Item | Acceptance Criteria |
| --- | --- | --- | --- | --- |
| MR-01 | P0 | Done | Add context-packet feedback log. | Authenticated callers can record useful, stale, missing, or noisy retrieval feedback for context packets; raw query text is not stored; database-backed API tests cover storage and validation. |
| MR-02 | P0 | Done | Turn retrieval feedback into operator metrics. | `/api/operations/summary` shows retrieval feedback counts, shares, and per-hour rates by feedback type over the recent 24-hour operator window. |
| MR-03 | P0 | Done | Add contradiction overlay for full LMSS benchmark smoke. | The `fact_finding_contradiction_overlay` fixture exists, can be loaded repeatably, and the agent-contract usefulness smoke can run all 8 tasks with ACU-003 enabled. |
| MR-04 | P0 | Todo | Define production deployment shape. | A production-pilot deployment plan defines separate migrator, API, and worker processes, managed PostgreSQL or equivalent, secret-store expectations, rollback procedure, and restore validation path. |

## LLM Memory Support Service v1

| ID | Priority | Status | Item | Acceptance Criteria |
| --- | --- | --- | --- | --- |
| LMSS-01 | P0 | Done | Define agent-facing memory contract. | [Agent-Facing Memory Contract](agent-facing-memory-contract.md) names the v1 tool surface, shared targeting fields, existing endpoint mappings, planned `memory.queryFacts` shape, safety semantics, and follow-on contract work. |
| LMSS-02 | P0 | Done | Publish OpenAPI or tool schema for existing endpoints. | [Agent Memory OpenAPI v1](api/agent-memory-v1.openapi.json) documents event append, memory proposal, context retrieval, context feedback, direct memory read, and source evidence read with authentication and idempotency expectations. |
| LMSS-03 | P0 | Done | Add client examples for the v1 memory workflow. | [Agent Memory v1 Client Examples](api/agent-memory-v1-examples.md) show append evidence, propose memory, retrieve context, record feedback, read evidence, and handle idempotent retries without storing raw query text. |
| LMSS-04 | P0 | Done | Design `memory.queryFacts` implementation plan. | [API `memory.queryFacts` Implementation Plan](api/memory-query-facts-implementation-plan.md) maps the planned fact-finding contract to repositories, authorization predicates, contradiction handling, response DTOs, and database-backed tests before implementation. |
| LMSS-05 | P0 | Done | Implement fact-finding endpoint with evidence and policy metadata. | `POST /api/memory/query-facts` lets agents query authorized facts and receive claims, source ids, confidence, lifecycle status, contradiction summaries, exclusion summaries, and policy metadata. |
| LMSS-06 | P1 | Done | Document policy targeting for agent callers. | [Policy Targeting For Agent Callers](api/policy-targeting-for-agent-callers.md) documents principal resolution, target scope, namespace, role id, trust level, retention class, sensitivity, and source event rules with examples. |
| LMSS-07 | P1 | Done | Add benchmark tasks for agent contract usefulness. | [Agent Contract Usefulness v1](../benchmarks/agent-contract-usefulness-v1/README.md) benchmark tasks measure whether the agent-facing contract improves fact finding, evidence use, contradiction handling, role targeting, feedback hygiene, and safe scoped answers. |
