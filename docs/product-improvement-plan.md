# Product Improvement Plan

Last reviewed: 2026-05-30

## Purpose

This document captures a product-owner view of the long-term memory system after
the Short Run private-alpha baseline and Middle Run production-pilot hardening
baseline. It is not a replacement for the delivery [Roadmap](roadmap.md) or
[Backlog](backlog.md). It reframes the next work around product maturity: trust,
usability, production readiness, measurable memory quality, and operator
confidence.

## Product-Owner Read

The project is a strong technical MVP, but not yet a polished product.

The core idea is sound: PostgreSQL stores truth, pgvector supports recall, source events preserve evidence, the Memory Broker controls writes, the Context Builder controls reads, and humans can review or correct durable memory. That direction is consistent with the [Project Goal](project-goal.md), [Architecture Overview](architecture.md), and [Long-Term AI Memory System Plan](long-term-memory-system-plan.md).

The next product challenge is to turn "memory system that works" into "memory product people trust."

## Current Strengths

- The north star is clear: trustworthy, auditable, permission-aware long-term memory for AI agents.
- The architecture is grounded in durable evidence and human correction rather than uncontrolled prompt accumulation.
- PostgreSQL, pgvector, SQL-first migrations, API idempotency, source events, access checks, review flows, vault exports, health checks, and database-backed tests are already present.
- The project has strong correctness instincts: real PostgreSQL tests, migration checks, authorization-before-retrieval rules, and explicit retention/erasure policy.
- The service layout separates API, Application, Infrastructure, Worker, Migrator, UnitTests, and IntegrationTests.

## Main Gaps

- The product experience is still thin. The review dashboard and first admin memory/source inspection slices exist, but there is no full memory console, onboarding path, role-assignment workflow, or polished operator workflow.
- Retention, legal hold, and erasure now have first authenticated operator endpoints, but broader production automation for external payload stores, backup pruning, and environment-specific retention policy is still future work.
- The `MemorySystem.Domain` project does not yet carry many of the durable business concepts described in the architecture.
- Pilot observability now has metrics export, alert rules, dashboard artifacts, trace coverage, and smoke checks. Runtime OpenTelemetry wiring, platform exporters, and environment-specific alert routing remain future work.
- Deployment now has a production-pilot shape and executable local deployment smoke. Managed infrastructure modules, release environments, and environment-specific deployment checklists remain future work.
- Several important tests and repositories are large enough that ongoing changes will remain review-heavy unless split by feature area.

## Product Principles For The Next Phase

- Ship one excellent workflow before widening the product surface.
- Make every remembered item explainable: who can see it, where it came from, why it was stored, whether it is current, and how it can be fixed.
- Treat retrieval quality as a product metric, not only an implementation detail.
- Automate the policies that protect trust: retention, erasure, stale projections, failed jobs, and access drift.
- Keep PostgreSQL as the recovery authority and make every projection rebuildable.
- Avoid a big rewrite. The next phase should harden, expose, and measure the existing system.

## Short Run: 1-2 Weeks

Goal: make the current MVP shippable as a private alpha.

Status: Complete as of May 29, 2026. The private-alpha baseline now has a top-level quickstart, repeatable Scenario 0001 seed command, operator summary endpoint, release verification path, backup/restore smoke, first retention automation, and split high-churn integration tests.

Scope note: Short Run is closed. Future fixes to the quickstart, demo seed, release hygiene, retention smoke, or split tests should be treated as regression maintenance for the private-alpha baseline, not as new Short Run scope.

1. Create a clear product entrypoint.
   - Add a top-level quickstart that explains what the system does, how to run it, and what a successful demo looks like.
   - Link the product goal, architecture, roadmap, testing commands, and this improvement plan from one obvious place.

2. Define the alpha workflow.
   - Use one end-to-end story: append event, propose memory, review memory, retrieve context, export to vault.
   - Reuse the existing scenario material as seed data so every reviewer sees the same product narrative.

3. Add a small operator readout.
   - Show API readiness, worker heartbeat, outbox backlog, failed jobs, pending reviews, and stale vault exports.
   - This can be a simple endpoint or dashboard section before it becomes a full admin console.

4. Finish release hygiene.
   - Keep migrations, new source files, deleted files, and generated UI artifacts staged together.
   - Run release verification: build, fast tests, database tests, cached whitespace check, and backup/restore smoke where relevant.

5. Automate the first retention task.
   - Implement scheduled payload minimization for expired `ephemeral` events first.
   - Add verification that minimized events are not usable as normal source references.

6. Reduce near-term maintenance drag.
   - Split the largest migration schema and API integration tests by feature area.
   - Keep the test behavior unchanged; this is a reviewability improvement, not a rewrite.

Success metric: a new developer or private-alpha user can run the system, store a memory, review it, retrieve scoped context, and understand the evidence behind the result.

## Middle Run: 1-3 Months

Goal: make the product production-pilot credible.

Status: Production-pilot credible baseline complete as of May 30, 2026. The Middle Run production-pilot slices store context-packet feedback without raw query text, expose recent retrieval feedback metrics in the operator summary, load the full LMSS benchmark smoke contradiction overlay, add the first authenticated metrics export plus alert-input smoke, add the admin console memory/source-event inspection workflow, automate the first governance workflows for legal holds, erasure execution, and retention reporting, prove the local production-pilot deployment shape across separate migrator, API, and worker roles with rollback and restore validation, make pilot observability executable through versioned alert rules, dashboard definitions, trace coverage, and metric-input smokes, and add benchmark release gates for Memory Lift, Contract Lift, safety counters, stale-memory usage, source-link coverage, and agent-contract smoke. The next product track should move from Middle Run hardening into Long Run gate scoping, starting with enterprise access and context productization.

Middle Run status language:

- Runtime-shipped means the service has code, tests, and a local smoke or benchmark path.
- Designed means the contract, runbook, or deployment/observability expectation is documented but still needs executable pilot proof.
- Production-pilot credible does not mean production-ready. It means one real project can safely trial the system with clear rollback, observability, governance, and benchmark gates.

Middle Run execution gates:

1. Done: MR-08 completes the operator evidence trail with source event and audit browsing.
2. Done: MR-09 automates governance workflows for legal hold, erasure execution, and retention reporting.
3. Done: MR-10 proves the production-pilot deployment shape with a smoke run across separate migrator, API, and worker roles plus rollback/restore validation.
4. Done: MR-11 turns observability design into executable dashboards, alert rules, trace coverage, and metric-input smokes.
5. Done: MR-12 makes benchmark results a release gate using Memory Lift, Contract Lift, source-link coverage, stale-memory usage, zero scoped leaks, and agent-contract smoke.

Middle Run capability areas:

1. Add a real deployment shape.
   - Managed PostgreSQL or equivalent production database.
   - Separate migrator, API, and worker processes.
   - Secret store integration.
   - Rollback procedure and restore validation.
   - Done: define the [Production Deployment Shape](production-deployment-shape.md) for the production-pilot migrator/API/worker split, managed PostgreSQL expectations, secret-store assumptions, rollback procedure, and restore validation path.
   - Done: add `scripts/production-pilot-deployment-smoke.sh` to publish and run the separate migrator, API, and worker roles against an isolated PostgreSQL target, then restore into a fresh database and re-point API and worker at it.

2. Add production observability.
   - OpenTelemetry traces for proposal, review, retrieval, embedding, outbox, and vault export flows.
   - Metrics for request rate, latency, review backlog, outbox age, dead-letter count, retrieval quality, embedding failures, and stale exports.
   - Alerts for readiness failure, worker heartbeat staleness, outbox backlog age, and repeated embedding/provider failures.
   - Done: define [Production Observability and Alerting](production-observability.md), including required pilot metrics, payload-safe trace/log fields, alert thresholds, dashboard minimums, and operator first-response runbook actions.
   - Done: expose the first authenticated `/api/operations/metrics` Prometheus-compatible export and `scripts/operations-metrics-smoke.sh` alert-input smoke for request health, readiness, outbox, worker heartbeat, retrieval feedback, review/vault workflow, and embedding-index failures.
   - Done: add versioned Prometheus-compatible alert rules, a Grafana-compatible pilot dashboard, a trace coverage manifest, metric input manifests, and artifact/live-metric smoke validation under `observability/`.

3. Build the first real admin console.
   - Memory list and detail view.
   - Source event explorer.
   - Pending review queue.
   - Redaction and delete workflows.
   - Audit trail by memory, event, principal, and namespace.
   - Done: add the first `/admin/` memory/source inspection slice for browsing authorized memory facts, safe policy metadata, lifecycle state, confidence, and explicit source evidence opens.
   - Done: expand `/admin/` with source event search, policy filters, time-window filters, linked memory/review/export/redaction references, and redaction or erasure state without source payloads in the list.

4. Treat retrieval quality as a product capability.
   - Track precision, false positives, stale-memory rate, context compactness, source-link coverage, and review acceptance rate.
   - Add a small golden evaluation set beyond deterministic tests.
   - Record before/after results for ranking or broker changes.
   - Done: store context-packet usefulness feedback as hashed retrieval observations.
   - Done: expose retrieval feedback counts, shares, and per-hour rates in the operator summary for the recent metrics window.
   - Done: add the MR-12 benchmark release gate that combines LLM outcome scorecards, agent-contract scorecards, source-link coverage, stale-memory usage, scoped-safety counters, and the agent-contract smoke output.

5. Automate governance workflows.
   - Legal hold create/release/reporting.
   - Erasure execution that rewrites event payload markers and clears derived copies.
   - Retention reports by namespace, retention class, sensitivity, and age.
   - Done: add authenticated operator endpoints for legal hold create/release/list, erasure execution over authorized source events and derived facts/chunks/reviews/exports, and retention reports grouped by namespace, retention class, sensitivity, and age.

6. Stabilize external contracts.
   - Publish OpenAPI output or generated API docs.
   - Add client examples for event append, memory proposal, review action, and context retrieval.
   - Document idempotency behavior for client implementers.
   - Done: define the [Agent-Facing Memory Contract](agent-facing-memory-contract.md) for the v1 tool surface, targeting fields, fact-finding shape, and safety semantics.
   - Done: publish the curated [Agent Memory OpenAPI v1](api/agent-memory-v1.openapi.json) contract for the existing agent-facing memory workflow.
   - Done: add [Agent Memory v1 Client Examples](api/agent-memory-v1-examples.md) for evidence append, proposal, context retrieval, feedback, source reads, and idempotent retries.
   - Done: design the [API `memory.queryFacts` Implementation Plan](api/memory-query-facts-implementation-plan.md) before adding the endpoint.
   - Done: implement `POST /api/memory/query-facts` with authorized claims, source links, lifecycle status, contradiction summaries, safe exclusion summaries, and policy metadata.
   - Done: document [Policy Targeting For Agent Callers](api/policy-targeting-for-agent-callers.md), covering principal resolution, scope, namespace, role, trust, retention, sensitivity, and source evidence fields.
   - Done: add the [Agent Contract Usefulness v1](../benchmarks/agent-contract-usefulness-v1/README.md) benchmark suite for LMSS fact finding, evidence use, contradiction handling, scoped safety, role targeting, and feedback hygiene.

7. Move core concepts into the domain model.
   - Promote stable concepts such as scope, namespace, trust level, memory lifecycle, retention class, and source evidence into the Domain layer where useful.
   - Keep database-specific SQL in Infrastructure and HTTP-specific DTOs in API.

Success metric: one real project or team can use the system continuously, and an operator can explain, correct, remove, or restore any memory with confidence.

## Long Run: 3-12 Months

Goal: become a reliable memory platform rather than a single backend service.

Long Run should be executed as gates, not as a parallel wishlist. Each gate should have a short decision record or backlog slice before implementation starts.

1. Enterprise access gate.
   - Entry criteria: MR-08 audit browsing, MR-09 governance automation, MR-10 pilot deployment smoke, MR-11 executable observability, and MR-12 benchmark release gates are complete.
   - Outcome: OIDC or SSO, service accounts, role assignment UI, and audit export are coherent enough for a real team.
   - Done: [Enterprise Access Gate](enterprise-access-gate.md) and [Decision 0042](decisions/0042-enterprise-access-gate.md) scope the identity binding model, OIDC or SSO path, service-account lifecycle, admin access-management UI, audit export, migration phases, and pilot acceptance checks.

2. Governance and compliance gate.
   - Entry criteria: erasure, legal hold, retention reporting, and audit browsing are working in the pilot path.
   - Outcome: data residency, backup erasure replay, permission-drift reporting, and environment-specific retention policy can be implemented without weakening deletion or access guarantees.

3. Context productization gate.
   - Entry criteria: benchmark release gate exists and context feedback is visible to operators.
   - Outcome: context packets explain why memory was included, what was excluded, and how reviewers can mark memory useful, stale, wrong, sensitive, or over-broad.

4. Memory intelligence gate.
   - Entry criteria: human review outcomes and benchmark results are available as calibration signals.
   - Outcome: classification, deduplication, contradiction detection, confidence calibration, summarization, and time decay improve measured Memory Lift without increasing leaks or stale-memory usage.

5. Retrieval scale gate.
   - Entry criteria: production model and embedding dimensions are stable enough to justify specialized indexes.
   - Outcome: model-specific vector indexes, rebuild pipelines, queue separation, and provider cost controls can scale without changing the trust model.

6. Integration gate.
   - Entry criteria: identity, governance, and context packet semantics are stable.
   - Outcome: SDKs, conversation import, Git/Markdown ingestion, issue tracker connectors, and Obsidian import paths can be added without turning external content into ungoverned memory.

Long Run capability areas:

1. Add first-class identity and enterprise access.
   - OIDC or SSO.
   - Service accounts.
   - Role assignment UI.
   - Audit exports.
   - Optional SCIM or directory sync later.

2. Improve memory intelligence.
   - Better candidate classification.
   - Better deduplication and contradiction detection.
   - Confidence calibration from review outcomes.
   - Summary generation and time-decay handling.
   - Feedback loops from retrieval usefulness.

3. Scale retrieval and indexing.
   - Model-specific vector indexes after production model and dimensions stabilize.
   - Rebuild pipelines for embeddings and chunks.
   - Queue separation for embeddings, exports, retention, and review notifications.
   - Backpressure and cost controls for provider calls.

4. Expand integrations.
   - Agent runtime SDK.
   - Conversation import.
   - Git and Markdown ingestion.
   - Issue tracker and document-source connectors.
   - Obsidian import paths where the vault contains curated source material.

5. Mature governance and compliance.
   - Data residency controls.
   - Backup erasure replay.
   - Legal hold reports.
   - Redaction verification.
   - Permission-drift reports.
   - Environment-specific retention policy.

6. Productize context packets.
   - Explain why each memory was retrieved.
   - Show what was excluded and why.
   - Let reviewers mark memories as useful, stale, wrong, sensitive, or over-broad.
   - Feed those signals back into ranking and broker policy.

Success metric: teams can trust the system as shared AI memory infrastructure across projects, roles, agents, and time.

## Ultimate Goal

The ultimate product is a trustworthy memory layer for AI agents.

It is not just a vector database, prompt cache, notes app, or audit table. It is a system where agents gain continuity over months or years while humans keep control over truth, scope, provenance, correction, deletion, and access.

Every remembered thing should be able to answer:

- who can see it
- where it came from
- why it was stored
- whether it is current
- how confident the system is
- what it affects
- how it can be corrected or removed

The final product vision is simple: AI agents get durable continuity without turning memory into an uncontrolled prompt dump.

## Near-Term Product Decision

LR-01 has scoped the enterprise access gate, and LR-03 has captured the first
local benchmark release-gate baseline. The next move should be LR-02 context
productization gate scoping, then implementation of the `EA-*` enterprise access
backlog in small slices. Before inviting an external pilot user, rerun the
benchmark release gate with the intended pilot model, fresh scorecards, and a
fresh live smoke artifact from the target environment.
