# Product Improvement Plan

Last reviewed: 2026-05-29

## Purpose

This document captures a product-owner view of the long-term memory system after the M0-M8 technical milestones. It is not a replacement for the delivery [Roadmap](roadmap.md) or [Backlog](backlog.md). It reframes the next work around product maturity: trust, usability, production readiness, measurable memory quality, and operator confidence.

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

- The product experience is still thin. The review dashboard exists, but there is no full memory console, onboarding path, source-event browser, or operator workflow.
- Retention, legal hold, and erasure are policy-defined and schema-supported, but not yet fully automated.
- The `MemorySystem.Domain` project does not yet carry many of the durable business concepts described in the architecture.
- Observability is mostly health checks and structured logs. Production-grade traces, metrics, dashboards, and alerts remain future work.
- Deployment is not yet a first-class product artifact. There is CI and local Docker, but no visible infrastructure module, release environment, or deployment checklist as code.
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

Status: Started as of May 29, 2026. The first Middle Run production-pilot slices store context-packet feedback without raw query text and expose recent retrieval feedback metrics in the operator summary. The parallel product track is LLM Memory Support Service v1, which turns the private-alpha backend into a stable agent-facing contract.

1. Add a real deployment shape.
   - Managed PostgreSQL or equivalent production database.
   - Separate migrator, API, and worker processes.
   - Secret store integration.
   - Rollback procedure and restore validation.

2. Add production observability.
   - OpenTelemetry traces for proposal, review, retrieval, embedding, outbox, and vault export flows.
   - Metrics for request rate, latency, review backlog, outbox age, dead-letter count, retrieval quality, embedding failures, and stale exports.
   - Alerts for readiness failure, worker heartbeat staleness, outbox backlog age, and repeated embedding/provider failures.

3. Build the first real admin console.
   - Memory list and detail view.
   - Source event explorer.
   - Pending review queue.
   - Redaction and delete workflows.
   - Audit trail by memory, event, principal, and namespace.

4. Treat retrieval quality as a product capability.
   - Track precision, false positives, stale-memory rate, context compactness, source-link coverage, and review acceptance rate.
   - Add a small golden evaluation set beyond deterministic tests.
   - Record before/after results for ranking or broker changes.
   - Done: store context-packet usefulness feedback as hashed retrieval observations.
   - Done: expose retrieval feedback counts, shares, and per-hour rates in the operator summary for the recent metrics window.

5. Automate governance workflows.
   - Legal hold create/release/reporting.
   - Erasure execution that rewrites event payload markers and clears derived copies.
   - Retention reports by namespace, retention class, sensitivity, and age.

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

The next move should be a private alpha around one excellent workflow, followed by measurement of whether retrieved context actually improves the agent's decisions.
