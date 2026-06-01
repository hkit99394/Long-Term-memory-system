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

M8 operational readiness backlog is complete. Short Run is closed and should
only receive regression-maintenance fixes. Middle Run now has two connected
tracks: production-pilot operations and the agent-facing LLM Memory Support
Service v1 contract. The first LMSS v1 contract slice is documented,
implemented, benchmarkable, and smoke-tested with the contradiction overlay.
Retrieval feedback is visible in the operator summary. MR-06 adds the first
authenticated metrics export and local alert-input smoke. MR-07 adds the first
admin console memory/source inspection slice. MR-08 expands that console into
source event and audit browsing. MR-09 automates the first governance workflows
for legal holds, erasure execution, and retention reporting. MR-10 proves the
local production-pilot deployment shape across separate migrator, API, and
worker roles with rollback and restore validation. MR-11 makes observability
executable through versioned alert rules, dashboard definitions, trace coverage,
and metric-input smokes. MR-12 adds the benchmark release gate for Memory Lift,
Contract Lift, scoped-safety leaks, stale-memory usage, source-link coverage,
and agent-contract smoke. LR-01 scopes enterprise access, LR-02 scopes context
productization, LR-04 scopes Domain model extraction, LR-05 scopes production
platform integration, PI-01 selects the first AWS/Terraform/container platform
baseline, PI-02 adds the first Terraform skeleton plus multi-role OCI image
contract, PI-03 provisions managed PostgreSQL with pgvector validation, PI-04
adds backup/export and restore-validation automation with evidence and metrics,
PI-05 wires runtime OpenTelemetry providers and OTLP exporter selection for API
and worker roles, and PI-06 connects alert routing, runbook links, silence
policy, and per-environment route tests. PI-07 adds environment-specific
release checklists for local, CI, pilot, and production evidence. PI-08 runs
the first isolated platform rehearsal and records the migration, health,
metrics, benchmark, backup/restore, rollback, and audit evidence. The next
focus is `EA-01`: add the identity-binding schema while keeping `DM-06` as a
later cleanup slice.

## Middle Run Production Pilot

| ID | Priority | Status | Item | Acceptance Criteria |
| --- | --- | --- | --- | --- |
| MR-01 | P0 | Done | Add context-packet feedback log. | Authenticated callers can record useful, stale, missing, or noisy retrieval feedback for context packets; raw query text is not stored; database-backed API tests cover storage and validation. |
| MR-02 | P0 | Done | Turn retrieval feedback into operator metrics. | `/api/operations/summary` shows retrieval feedback counts, shares, and per-hour rates by feedback type over the recent 24-hour operator window. |
| MR-03 | P0 | Done | Add contradiction overlay for full LMSS benchmark smoke. | The `fact_finding_contradiction_overlay` fixture exists, can be loaded repeatably, and the agent-contract usefulness smoke can run all 8 tasks with ACU-003 enabled. |
| MR-04 | P0 | Done | Define production deployment shape. | A production-pilot deployment plan defines separate migrator, API, and worker processes, managed PostgreSQL or equivalent, secret-store expectations, rollback procedure, and restore validation path. |
| MR-05 | P0 | Done | Define production observability and alerting. | A production-pilot observability plan defines required metrics, payload-safe traces and logs, alert thresholds, dashboard minimums, and first-response runbook actions for API, worker, PostgreSQL, retrieval, review, vault export, and backup health. |
| MR-06 | P0 | Done | Implement metrics export and alert smoke checks. | API exports first production-pilot metrics for request health, readiness, outbox age, dead letters, worker heartbeat, retrieval feedback, review/vault workflow, and embedding-index failures; `scripts/operations-metrics-smoke.sh` verifies the key alert inputs are observable against a running local API. |
| MR-07 | P0 | Done | Build the first admin console memory/source inspection slice. | `/admin/` lets an authenticated operator browse authorized memory facts with scope, lifecycle status, confidence, source links, and safe policy metadata, then open source evidence through the existing authorized event-read path without including raw source payloads in the memory list. |
| MR-08 | P0 | Done | Expand admin inspection into a source event and audit browser. | An authenticated operator can search source events by scope, sensitivity, retention class, trust level, and time window, inspect linked memory/review/export references, and see redaction or erasure state without exposing hidden payloads. |
| MR-09 | P0 | Done | Automate governance workflows. | Legal hold create/release/reporting, erasure execution, and retention reports by namespace, retention class, sensitivity, and age are executable through authenticated operator paths and covered by database-backed tests. |
| MR-10 | P0 | Done | Prove the production-pilot deployment shape. | `scripts/production-pilot-deployment-smoke.sh` verifies the documented separate migrator, API, and worker roles against an isolated PostgreSQL target, including rollback and restore-validation steps that can be repeated by an operator. |
| MR-11 | P0 | Done | Make observability executable. | Dashboard definitions, alert rules, and trace coverage exist as versioned artifacts for API, worker, PostgreSQL, retrieval, review, vault export, backup, and governance signals; local and pilot smoke checks verify alert inputs from the checked-in metric manifest. |
| MR-12 | P0 | Done | Add benchmark release gates. | Release verification records Memory Lift, Contract Lift, scoped-safety leak count, stale-memory usage, and source-link coverage for the benchmark suites; a release cannot pass with unauthorized leaks, stale-memory usage, missing source-link coverage, or failed agent-contract smoke. |

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

## Long Run Gate Scoping

| ID | Priority | Status | Item | Acceptance Criteria |
| --- | --- | --- | --- | --- |
| LR-01 | P0 | Done | Scope enterprise access gate. | [Decision 0042](decisions/0042-enterprise-access-gate.md) and [Enterprise Access Gate](enterprise-access-gate.md) define OIDC or SSO, service accounts, role assignment UI, audit export, migration from API-key-only operation, and pilot acceptance checks without weakening existing namespace grants. |
| LR-02 | P0 | Done | Scope context productization gate. | [Decision 0043](decisions/0043-context-productization-gate.md) and [Context Productization Gate](context-productization-gate.md) define context-packet inclusion explanations, safe exclusion summaries, reviewer feedback actions for useful/stale/wrong/sensitive/over-broad/missing context, and how those signals feed benchmark-visible ranking improvements. |
| LR-03 | P0 | Done | Capture first benchmark release-gate report. | [LR-03 Benchmark Release-Gate Report](benchmark-release-gate-lr03.md) records the local baseline from filled LLM outcome and agent-contract scorecards plus the existing full eight-task agent-contract smoke output; the generated ignored report records Memory Lift, Contract Lift, scoped-safety leak count, stale-memory usage, and source-link coverage. A fresh live smoke rerun remains required before external pilot release. |
| LR-04 | P1 | Done | Define Domain model extraction slice. | [Decision 0044](decisions/0044-domain-model-extraction-slice.md) and [Domain Model Extraction LR-04](domain-model-extraction-lr04.md) inventory stable concepts such as memory scope, namespace, trust level, lifecycle status, retention class, sensitivity, source evidence, and feedback type, then split staged extraction into compatibility-tested `DM-*` implementation slices without schema or endpoint churn. |
| LR-05 | P1 | Done | Scope production platform integration. | [Decision 0045](decisions/0045-production-platform-integration.md) and [Production Platform Integration LR-05](production-platform-integration-lr05.md) define infrastructure-as-code boundaries, managed PostgreSQL and backup exporter assumptions, runtime OpenTelemetry/exporter wiring, alert routing, and environment-specific release checklists. |

## Long Run Production Platform Integration Implementation Backlog

| ID | Priority | Status | Item | Acceptance Criteria |
| --- | --- | --- | --- | --- |
| PI-01 | P0 | Done | Choose production platform and IaC baseline. | [Decision 0046](decisions/0046-production-platform-and-iac-baseline.md) and [Production Platform Baseline PI-01](production-platform-baseline-pi01.md) select AWS ECS Fargate, Amazon RDS PostgreSQL with pgvector, Amazon ECR, Terraform, immutable OCI image digests, environment layout, state/secrets rules, and owner model without changing application contracts. |
| PI-02 | P0 | Done | Add Terraform IaC skeleton for runtime roles. | `infra/terraform` defines the initial module and pilot/production environment layout for separate ECS migrator, API, and worker roles, ingress/TLS assumptions, secret references, resource limits, image digest input, and environment parameters without committing secret values; the root `Dockerfile` defines the multi-role OCI image artifact contract. |
| PI-03 | P0 | Done | Provision Amazon RDS PostgreSQL with pgvector. | Terraform defines managed PostgreSQL, pgvector availability checks, network access, dedicated credentials or identity, backup/PITR settings where supported, and a validation database path. |
| PI-04 | P0 | Done | Add backup exporter and restore validation automation. | Backup status/export evidence and restore-to-new-database validation can run per environment and emit alertable metrics or logs. |
| PI-05 | P0 | Done | Wire runtime OpenTelemetry exporters. | API and worker emit payload-safe traces and metrics with service name, environment, version, instance id, and correlation ids according to the checked-in trace coverage manifest. |
| PI-06 | P1 | Done | Connect alert routing and runbook links. | Platform alert rules route page, ticket, and info alerts to named owners, include runbook links, define silencing policy, and have a test route per environment. |
| PI-07 | P1 | Done | Add environment-specific release checklists. | [Production Release Checklists PI-07](production-release-checklists-pi07.md) define local, CI, pilot, and production release checklists covering migration, health, metrics, benchmark gate, backup/restore, rollback owner, alert routing, and audit evidence. |
| PI-08 | P1 | Done | Run first platform rehearsal. | [Production Platform Rehearsal PI-08](production-platform-rehearsal-pi08.md) records an isolated pilot rehearsal that completed migrator/API/worker deployment, health checks, authenticated smoke, metrics collection, backup restore validation, rollback rehearsal, benchmark gate, alert-routing smoke, and next-move planning. |

## Long Run Domain Model Extraction Implementation Backlog

| ID | Priority | Status | Item | Acceptance Criteria |
| --- | --- | --- | --- | --- |
| DM-01 | P0 | Done | Add pure Domain value objects. | `MemorySystem.Domain` defines dependency-free value objects and vocabulary types for scope, namespace, role id, trust level, lifecycle status, retention class, sensitivity, source evidence, and feedback type; compatibility unit tests prove normalization and string round-trips match existing Application behavior without migrating callers. |
| DM-02 | P0 | Done | Move namespace parsing behind a Domain parser. | Application keeps the existing `MemoryNamespaceParser` facade and public error behavior while Domain owns the parser rules; namespace compatibility tests prove the facade returns the same scope metadata, role ids, segments, error text, and scope prefixes. |
| DM-03 | P0 | Done | Extract lifecycle, trust, retention, and sensitivity vocabularies. | Existing Application constants and policies now delegate to Domain lifecycle, trust, retention, and sensitivity vocabularies while memory fact status, broker policy, event append, governance, admin, and context tests preserve current string values and behavior. |
| DM-04 | P1 | Done | Introduce Domain source evidence references. | Proposal, query-facts, context packet, review/admin inspection, and source evidence read paths map source ids and links through Domain references while preserving current JSON and authorization behavior. |
| DM-05 | P1 | Done | Map Infrastructure repository boundaries to Domain values. | Repositories map database strings to Domain values at the boundary without SQL schema changes; database-backed proposal, read, search, context, governance, and export tests pass. |
| DM-06 | P2 | Todo | Remove duplicate string normalization helpers. | After all callers migrate, duplicate policy vocabularies are removed or replaced by thin compatibility facades; `rg` confirms stable concepts are no longer independently redefined across layers. |

## Long Run Enterprise Access Implementation Backlog

| ID | Priority | Status | Item | Acceptance Criteria |
| --- | --- | --- | --- | --- |
| EA-01 | P0 | Todo | Add identity-binding schema. | Migrations add provider, issuer, subject, principal, status, display metadata, timestamps, and uniqueness constraints; disabled bindings fail lookup; migration tests cover duplicate and deleted-binding behavior. |
| EA-02 | P0 | Todo | Introduce shared principal resolution. | API key authentication and future OIDC authentication both produce one principal-resolution result containing principal id, type, auth method, and credential or binding id; current API-key tests remain unchanged. |
| EA-03 | P0 | Todo | Add access audit event model. | Authentication, authorization denial, membership change, role assignment change, namespace grant change, service credential change, and audit export records can be written without storing raw memory payloads. |
| EA-04 | P0 | Todo | Add generic OIDC authentication. | Configured issuer, audience, JWKS, HTTPS metadata, lifetime validation, and identity binding lookup authenticate human principals; unbound, disabled, or ambiguous identities fail closed. |
| EA-05 | P0 | Todo | Add service-account lifecycle. | Service principals have owner metadata, allowed auth method, credential review or expiry date, rotation/disable path, and least-privilege namespace grants. |
| EA-06 | P0 | Todo | Add admin access-management UI. | Authorized operators can manage memberships, role assignments, and namespace grants with effective-access preview, authorizer-backed explanations, and audited changes. |
| EA-07 | P0 | Todo | Add audit export. | Operators can export access-management and auth audit records for a time window and scope as newline-delimited JSON with manifest hash and payload-safe fields. |
| EA-08 | P0 | Todo | Add migration and rollback smoke. | A smoke script proves API-key-only, OIDC-only, dual-auth, service-account, and OIDC-disabled rollback modes without weakening namespace grants. |
| EA-09 | P1 | Todo | Document pilot operator runbook. | Runbook covers provider setup, identity binding, service-account creation, role/grant review, audit export, rollback, and break-glass API-key handling. |
| EA-10 | P1 | Todo | Evaluate directory sync. | Decide whether SCIM or provider group sync is needed after the first pilot; any sync remains provisioning-only and does not bypass local grants. |

## Long Run Context Productization Implementation Backlog

| ID | Priority | Status | Item | Acceptance Criteria |
| --- | --- | --- | --- | --- |
| CP-01 | P0 | Done | Define productized context packet schema. | [Context Packet Product v1 Contract](api/context-packet-product-v1.md) defines a versioned response contract and JSON Schema with packet id, generated time, policy summary, included item explanations, exclusion summaries, review actions, feedback policy, and evaluation hints while preserving current grouped items. |
| CP-02 | P0 | Done | Add packet and item identifiers for feedback. | Context packet responses include stable packet ids and item ids; context feedback can reference `packetId` and `itemId` so item feedback no longer requires resending raw query text. |
| CP-03 | P0 | Done | Implement structured inclusion explanations. | Context packet items now return primary reason, matched signals, rank components, policy fit, lifecycle fit, source evidence, and suggested review actions; tests prove unauthorized candidate metadata stays out of the packet. |
| CP-04 | P0 | Done | Add safe exclusion summaries to context packets. | Context packets now return payload-safe excluded summaries for inactive, not-authorized, scope-mismatch, role-mismatch, rank-cutoff, source-unavailable, and sensitive omissions using disclosed counts only for safe post-policy filters and withheld disclosure for side-channel-sensitive reasons. |
| CP-05 | P0 | Done | Expand context feedback actions. | Feedback now supports useful, stale, wrong, sensitive, over_broad, and missing actions while preserving the legacy noisy path; validation requires source ids for item-level actions and stores only payload-safe metadata. |
| CP-06 | P0 | Done | Add reviewer workflow for context observations. | Admin operators can inspect context feedback observations through `GET /api/reviews/context-observations`, open pending reviews from stale/wrong/sensitive observations through `POST /api/reviews/context-observations/{id}/review`, and see source-linked evidence without raw query storage. |
| CP-07 | P0 | Done | Feed reviewer actions into ranking signals. | Hybrid ranking now consumes bounded useful, stale, wrong, sensitive, over-broad, legacy noisy, and packet-level missing feedback as a `feedbackAdjustment` rank component scoped by target scope, role, source id, and the candidate namespace; feedback can down-rank or boost retrieval but cannot rewrite facts without review or broker decisions. |
| CP-08 | P0 | Done | Add context-product benchmark checks. | [Context Product Benchmark v1](../benchmarks/context-product-v1/README.md) adds live API smoke tasks for inclusion explanations, safe exclusions, reviewer-action hygiene, source-link coverage, stale-memory avoidance, and before/after feedback ranking behavior against the LR-03 baseline. |
| CP-09 | P1 | Done | Add context product health dashboard metrics. | Operations metrics expose explanation coverage, exclusion counts by safe reason, feedback action shares, review-open counts, ranking-signal application counts, and benchmark deltas. |
| CP-10 | P1 | Done | Update caller docs and examples. | [Context Product v1 Caller Guide](api/context-product-v1-caller-guide.md) and the v1 curl workflow show how agents should read explanations, handle safe exclusions, submit reviewer actions, and avoid storing raw query text. |
