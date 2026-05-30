# Architecture Overview

## Purpose

This document is the short architecture guide for the long-term memory system. The deeper system design lives in [Long-Term AI Memory System Plan](long-term-memory-system-plan.md).

The system stores durable AI memory in a way that is auditable, permission-aware, source-linked, and human-correctable. The central rule is simple: Postgres stores truth, pgvector supports recall, the Memory Broker controls writes, and the Context Builder controls reads.

## Component Map

| Component | Responsibility | Must Not Do |
| --- | --- | --- |
| ASP.NET Core Memory API | HTTP endpoints, authentication, authorization boundaries, OpenAPI, ProblemDetails, health checks. | Put business rules directly in route handlers. |
| Application layer | Memory Broker orchestration, Context Builder orchestration, scope resolution, permission decisions, lifecycle workflows. | Depend on ASP.NET Core or PostgreSQL-specific implementation details. |
| Domain layer | Core entities, value objects, status rules, scope concepts, role and trust concepts. | Reach into database, HTTP, filesystem, or embedding providers. |
| Infrastructure layer | Npgsql data access, SQL migrations, pgvector queries, full-text search, embedding adapters, vault adapters. | Decide memory policy or bypass application authorization. |
| Worker | Outbox processing for embedding, indexing, summaries, review notifications, expiry, redaction, and vault export. | Create durable memory outside the broker workflow. |
| TypeScript tools | Review dashboard, local admin workflows, vault sync tooling. | Become the source of truth for memory. |
| Obsidian vault | Human-readable export and review workspace. | Act as transactional storage, permission enforcement, or authoritative user memory. |

## Write Path

Durable writes must pass through the Memory Broker.

```text
Request or tool event
  -> append or reference source event
  -> extract candidate memory
  -> classify candidate
  -> resolve scope and namespace
  -> enforce write permission
  -> deduplicate and check contradictions
  -> assign confidence and review state
  -> write memory fact, derived chunk, audit event, and outbox jobs transactionally
  -> return idempotent broker decision
```

Write-path rules:

- Every durable memory must have source evidence.
- API mutations must use request-level idempotency.
- Broker writes must be transactional with event, memory, chunk, and outbox records.
- One-off task instructions should usually become session memory, not durable memory.
- Sensitive or uncertain data should be rejected, scoped narrowly, or sent to review.

## Read Path

Context construction must pass through the Context Builder.

```text
Agent or user request
  -> resolve principal, project, organization, role, agent, and session scope
  -> build authorized scope and namespace predicates
  -> retrieve structured, keyword, and vector matches inside authorized predicates
  -> exclude deleted, redacted, expired, superseded, and contradicted memory
  -> add role memory only when role and project access allow it
  -> rank, compress, and source-link results
  -> return compact context packet
```

Read-path rules:

- Unauthorized rows must not enter candidate sets for full-text or vector search.
- Retrieval counts, timings, and ranking should reflect only rows the principal can read.
- Retrieved content is context, not instruction authority.
- Context packets must stay small, explainable, and source-linked.

## Data Boundaries

Postgres owns:

- principals, memberships, role assignments, and grants
- events and source evidence
- memory facts and role lenses
- chunks, embeddings, reviews, redactions, idempotency keys, retrieval feedback, and outbox jobs
- lifecycle status and audit metadata

pgvector owns semantic recall rows, not truth. Embeddings must be rebuildable from source chunks or approved memory records.

Markdown owns human-readable documentation and trusted procedural guidance. It does not own transactional memory state.

## Trust and Access

Trust levels separate the authority of memory sources:

- `system_trusted`
- `human_approved`
- `user_scoped`
- `agent_private`
- `tool_output`
- `retrieved_untrusted`
- `web_content`

Access is enforced through principal identity, organization/project membership, role assignments, and memory grants. Namespace strings help group and query memory, but they do not replace executable authorization.

## Role Memory

Roles are lenses over shared truth, not separate realities.

Shared role principles may be reused across projects only when backed by global or organization truth. Project-role lenses interpret one project's facts for one role and must not leak project-specific facts into other projects.

## Current Architecture Boundary

The core MVP loop is complete when the system can:

```text
Observe event
Extract candidate memory
Broker validates write
Store structured memory
Index for retrieval
Build scoped context
Return compact context packet
```

The private-alpha baseline now extends that loop with:

- human review through the `/reviews/` dashboard
- source-linked Obsidian export and archive export
- stale export markers for inactive memory
- operational liveness, readiness, and summary endpoints
- worker heartbeat and outbox backlog visibility
- authenticated operations metrics export and alert-input smoke verification
- expired unreferenced `ephemeral` event payload minimization
- local backup/restore smoke verification
- context-packet retrieval feedback stored as hashed observations

The next architecture concern is production-pilot maturity: deployment shape,
observability, admin workflows, automated governance, and retrieval-quality
metrics.
