# Long-Term AI Memory System Plan

## Decision

Build the long-term memory system with this stack:

- Core backend: C# / ASP.NET Core on .NET 10 LTS
- Database: PostgreSQL
- Vector retrieval: pgvector inside PostgreSQL
- Migrations: SQL-first migrations
- UI and local tooling: TypeScript
- Experiments and evaluations: Python only when useful
- Human-readable workspace: Obsidian-compatible Markdown vault
- Trusted procedural memory: Git-tracked Markdown

The core memory system should be a durable backend service, not a collection of scripts. TypeScript and Python can support the system, but they should not own the source of truth.

## Guiding Principles

- Postgres is truth.
- pgvector is recall, not truth.
- Event log is evidence.
- Obsidian is a human workspace, not the transactional backend.
- Git is trusted procedural memory.
- The Memory Broker controls writes.
- The Context Builder controls reads.
- Roles are lenses over shared truth, not separate realities.
- Every durable memory needs scope, provenance, confidence, status, and lifecycle metadata.
- Store broadly, retrieve selectively, write cautiously.

## System Goals

The system should support:

- User preferences and profile memory
- Project facts and decisions
- Role-specific memory for Designer, Developer, CTO, CFO, COO, and CEO perspectives
- Agent-private memory
- Session working memory
- Conversation and tool-call event history
- Semantic retrieval over curated notes, facts, decisions, and summaries
- Permission-aware context construction
- Human review, correction, expiry, deletion, and supersession

The MVP should prove the full memory loop before adding a large UI:

```text
Observe event
Extract candidate memory
Broker validates write
Store structured memory
Index for retrieval
Build scoped context
Return compact context packet
```

## Architecture

```text
User / Agent Request
        |
        v
ASP.NET Core Memory API
        |
        v
Permission + Scope Resolver
        |
        v
Memory Retrieval
   +----------------+----------------+-------------------+
   |                |                |                   |
   v                v                v                   v
Postgres facts   pgvector index   Full-text search   Approved chunks
   +----------------+----------------+-------------------+
        |
        v
Context Builder
        |
        v
LLM / Agent Runtime
        |
        v
Memory Proposal
        |
        v
Memory Broker
        |
        v
Postgres + pgvector + event log + optional vault export
```

## Recommended Solution Layout

```text
repo-root/
  src/
    MemorySystem.Api/
    MemorySystem.Application/
    MemorySystem.Domain/
    MemorySystem.Infrastructure/
    MemorySystem.Worker/
  tests/
    MemorySystem.UnitTests/
    MemorySystem.IntegrationTests/
  tools/
    ui/
    vault-sync/
  migrations/
  docs/
  vault/
```

`repo-root/` means the current repository root, not a nested child folder.

### Project Responsibilities

`MemorySystem.Api`

- ASP.NET Core HTTP API
- Authentication and authorization boundaries
- OpenAPI contract
- ProblemDetails error responses
- Health checks

`MemorySystem.Application`

- Memory Broker orchestration
- Context Builder orchestration
- Retrieval use cases
- Write lifecycle
- Permission decisions
- Ranking and filtering policies

`MemorySystem.Domain`

- Core entities and value objects
- Scope, namespace, role, visibility, trust level, memory status
- Domain rules that do not depend on ASP.NET Core or Postgres

`MemorySystem.Infrastructure`

- Postgres access
- SQL migration runner integration if used
- pgvector queries
- full-text search
- embedding provider adapters
- vault import/export adapters

`MemorySystem.Worker`

- Background indexing
- summarization jobs
- expiry jobs
- contradiction review jobs
- vault sync jobs

`tools/ui`

- TypeScript dashboard for review and administration

`tools/vault-sync`

- TypeScript tooling for Obsidian import/export and local Markdown workflows

## ASP.NET Core Application Model

Use a focused ASP.NET Core Web API.

Recommended API style:

- Minimal APIs for the first MVP if the surface remains small and route-grouped.
- Switch to controller-based APIs if the API grows into many resources with richer filters and conventions.
- Keep route handlers thin either way.
- Put business rules in application services.
- Use request and response DTOs, not persistence entities, as API contracts.
- Use authorization at endpoint or route-group boundaries.
- Use ProblemDetails-compatible failures.

Default framework choices:

- Target framework: `net10.0`
- Runtime: ASP.NET Core 10
- Validation: built-in ASP.NET Core validation where sufficient
- Logging: built-in structured logging, with OpenTelemetry later
- Health checks: database, embedding provider, worker state

## Pre-Phase 1 Foundations

These decisions must be designed before the first migration is finalized. They affect schema shape, API contracts, tests, and retrieval behavior.

### Identity and Access Model

Access control must be executable, not only described in prose.

Initial model:

- Principal: a human user, agent, or service account.
- Organization: a workspace or company boundary.
- Project: a memory boundary inside an organization.
- Role: a reusable position or lens, such as CTO, CFO, COO, CEO, Designer, or Developer.
- Membership: a principal's access to an organization or project.
- Role assignment: a principal acting as a role within a scope.
- Memory grant: the effective permission to read, write, review, or administer a namespace.

Rules:

- Every API request resolves to one principal.
- Every memory read is filtered by the principal's effective access before or inside retrieval queries.
- Every durable memory write goes through the Memory Broker.
- The Broker may write on behalf of a principal, but must record the proposing principal and source event.
- Namespaces are useful for grouping, but authorization must be backed by explicit membership, role assignment, or grant records.

For MVP, use local API keys mapped to principals. Later, replace or extend this with full user authentication and policy-based authorization.

### Cross-Project Role Memory

Role memory should be reusable across projects without leaking project-specific facts.

Use three layers:

```text
Shared role memory
  durable principles for a role across projects

Project memory
  facts and decisions for one project

Project-role lens
  how a role interprets one project using shared role memory plus project truth
```

Example:

```text
Shared CTO memory:
"Prefer auditable systems with explicit source-of-truth boundaries."

Project A memory:
"Project A uses Postgres plus pgvector."

Project B memory:
"Project B uses Postgres plus Redis, with no vector retrieval yet."

CTO context for Project A:
shared CTO memory + Project A truth + Project A CTO lens

CTO context for Project B:
shared CTO memory + Project B truth + Project B CTO lens
```

Rules:

- Role memory can be shared across projects.
- Project facts cannot leak across projects unless explicitly promoted to organization or global memory.
- Role lenses can interpret project truth.
- Role lenses cannot replace project truth.
- Cross-project retrieval must require both role access and project access.

### Provenance Requirement

Every durable memory fact, role lens, review decision, redaction, and deletion must have source evidence.

Rules:

- `memory_facts.source_event_id` is required.
- durable writes happen in the same transaction as their audit event or reference a pre-existing source event.
- session-only memory may omit durable storage, but if it is persisted for debugging it still needs an event.
- imported Markdown, documents, and tool outputs must become events before they can create memory.

### Deletion, Redaction, and Retention

Memory deletion is not only a status change. The system must account for copies in events, chunks, embeddings, summaries, and vault exports.

Use three operations:

- Expire: hide memory from normal retrieval after a time or condition.
- Delete: tombstone a memory so it is no longer returned, while retaining minimal audit metadata.
- Redact: remove or mask sensitive content from facts, chunks, exports, and event payloads where policy requires erasure.

Rules:

- Deleting a memory must remove or invalidate derived chunks and embeddings.
- Vault exports must be regenerated or marked stale after deletion/redaction.
- Event log retention must distinguish audit preservation from user-requested erasure.
- Redaction records should preserve who/what/when/why without retaining the sensitive text.
- Raw event payloads may be replaced with a hash, redaction marker, or external retained payload pointer when policy requires erasure.
- Context Builder must exclude deleted, redacted, expired, superseded, and contradicted memory by default.

### Outbox and Idempotent Indexing

API mutations and background work must be retry-safe.

Mutating API endpoints require an idempotency key and store the request fingerprint before side effects. Retries with the same key and same fingerprint return the original response. Reuse with a different fingerprint is a conflict.

Use an outbox table for:

- embedding generation
- chunk indexing
- vault export
- review notifications
- expiry and redaction jobs
- summary generation

Rules:

- Broker writes memory facts and outbox jobs in one transaction.
- Every job has an idempotency key.
- Workers use leases to avoid duplicate processing.
- Failed jobs keep retry count and last error.
- Poison jobs move to a dead-letter state.
- Rebuilding embeddings from source chunks must be safe.

## Database Direction

Use SQL-first migrations so the schema remains explicit and portable.

Postgres owns:

- structured memory facts
- event history
- access metadata
- namespace metadata
- review status
- embedding rows
- full-text search indexes

pgvector owns:

- semantic retrieval embeddings
- chunk-level vector search

Do not treat pgvector rows as authoritative. If an embedding row is deleted, it should be rebuildable from Postgres truth or curated Markdown sources.

## Core Tables

### principals

Humans, agents, and service accounts.

```sql
CREATE TABLE principals (
    id UUID PRIMARY KEY,
    principal_type TEXT NOT NULL,
    display_name TEXT NOT NULL,
    status TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CHECK (principal_type IN ('human', 'agent', 'service')),
    CHECK (status IN ('active', 'disabled', 'deleted'))
);
```

### organizations and projects

Top-level sharing and isolation boundaries.

```sql
CREATE TABLE organizations (
    id UUID PRIMARY KEY,
    name TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE projects (
    id UUID PRIMARY KEY,
    org_id UUID NOT NULL REFERENCES organizations(id),
    name TEXT NOT NULL,
    status TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CHECK (status IN ('active', 'archived', 'deleted'))
);
```

### memberships and role assignments

Executable access-control foundation.

```sql
CREATE TABLE organization_memberships (
    org_id UUID NOT NULL REFERENCES organizations(id),
    principal_id UUID NOT NULL REFERENCES principals(id),
    access_level TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (org_id, principal_id),
    CHECK (access_level IN ('reader', 'contributor', 'reviewer', 'admin', 'owner'))
);

CREATE TABLE project_memberships (
    project_id UUID NOT NULL REFERENCES projects(id),
    principal_id UUID NOT NULL REFERENCES principals(id),
    access_level TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (project_id, principal_id),
    CHECK (access_level IN ('reader', 'contributor', 'reviewer', 'admin'))
);

CREATE TABLE role_assignments (
    id UUID PRIMARY KEY,
    principal_id UUID NOT NULL REFERENCES principals(id),
    role_id TEXT NOT NULL,
    scope_type TEXT NOT NULL,
    scope_id UUID,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CHECK (role_id IN ('designer', 'developer', 'cto', 'cfo', 'coo', 'ceo')),
    CHECK (scope_type IN ('global', 'org', 'project'))
);

CREATE TABLE memory_access_grants (
    id UUID PRIMARY KEY,
    principal_id UUID REFERENCES principals(id),
    role_id TEXT,
    namespace_prefix TEXT NOT NULL,
    permission TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CHECK (permission IN ('read', 'write', 'review', 'admin')),
    CHECK (principal_id IS NOT NULL OR role_id IS NOT NULL)
);
```

`scope_id` is null only when `scope_type = 'global'`. The migration should enforce this with a check constraint.

### events

Append-only raw evidence.

```sql
CREATE TABLE events (
    id UUID PRIMARY KEY,
    principal_id UUID REFERENCES principals(id),
    conversation_id UUID,
    agent_principal_id UUID REFERENCES principals(id),
    role_id TEXT,
    event_type TEXT NOT NULL,
    content JSONB NOT NULL,
    content_hash TEXT,
    external_payload_uri TEXT,
    retention_class TEXT NOT NULL,
    sensitivity TEXT NOT NULL,
    redaction_status TEXT NOT NULL DEFAULT 'none',
    redacted_at TIMESTAMPTZ,
    redaction_event_id UUID REFERENCES events(id),
    trust_level TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CHECK (event_type IN (
        'user_message',
        'assistant_message',
        'tool_call',
        'memory_proposed',
        'memory_written',
        'memory_deleted',
        'memory_redacted',
        'memory_reviewed'
    )),
    CHECK (trust_level IN (
        'system_trusted',
        'human_approved',
        'user_scoped',
        'agent_private',
        'tool_output',
        'retrieved_untrusted',
        'web_content'
    )),
    CHECK (retention_class IN ('ephemeral', 'standard', 'audit', 'legal_hold', 'erasure_requested')),
    CHECK (sensitivity IN ('none', 'personal', 'secret', 'regulated')),
    CHECK (redaction_status IN ('none', 'pending', 'redacted', 'erased'))
);
```

`content` holds raw evidence while retention allows it. When erasure is required, keep the event id, actor, type, trust level, timestamps, hash, and redaction metadata, then replace sensitive payload content with a minimal marker or move it behind an approved external payload pointer with its own retention policy.

### memory_facts

Structured durable memory.

```sql
CREATE TABLE memory_facts (
    id UUID PRIMARY KEY,
    scope_type TEXT NOT NULL,
    scope_id TEXT NOT NULL,
    namespace TEXT NOT NULL,
    user_principal_id UUID REFERENCES principals(id),
    project_id UUID REFERENCES projects(id),
    org_id UUID REFERENCES organizations(id),
    role_id TEXT,
    agent_principal_id UUID REFERENCES principals(id),
    memory_type TEXT NOT NULL,
    visibility TEXT NOT NULL,
    subject TEXT NOT NULL,
    predicate TEXT NOT NULL,
    object TEXT NOT NULL,
    confidence NUMERIC(4,3) NOT NULL,
    status TEXT NOT NULL,
    source_event_id UUID NOT NULL REFERENCES events(id),
    proposed_by_principal_id UUID REFERENCES principals(id),
    superseded_by UUID REFERENCES memory_facts(id),
    valid_from TIMESTAMPTZ,
    valid_until TIMESTAMPTZ,
    expires_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CHECK (scope_type IN ('global', 'org', 'user', 'project', 'role', 'agent', 'session')),
    CHECK ((scope_type = 'global') = (scope_id = 'global')),
    CHECK ((scope_type = 'org') = (org_id IS NOT NULL AND project_id IS NULL AND user_principal_id IS NULL AND agent_principal_id IS NULL AND role_id IS NULL)),
    CHECK ((scope_type = 'project') = (project_id IS NOT NULL AND org_id IS NOT NULL AND user_principal_id IS NULL AND agent_principal_id IS NULL AND role_id IS NULL)),
    CHECK ((scope_type = 'user') = (user_principal_id IS NOT NULL AND org_id IS NULL AND project_id IS NULL AND agent_principal_id IS NULL AND role_id IS NULL)),
    CHECK ((scope_type = 'role') = (role_id IS NOT NULL AND org_id IS NULL AND project_id IS NULL AND user_principal_id IS NULL AND agent_principal_id IS NULL)),
    CHECK ((scope_type = 'agent') = (agent_principal_id IS NOT NULL AND org_id IS NULL AND project_id IS NULL AND user_principal_id IS NULL AND role_id IS NULL)),
    CHECK ((scope_type = 'session') = (org_id IS NULL AND project_id IS NULL AND user_principal_id IS NULL AND agent_principal_id IS NULL AND role_id IS NULL AND scope_id <> 'global')),
    CHECK (role_id IS NULL OR role_id IN ('designer', 'developer', 'cto', 'cfo', 'coo', 'ceo')),
    CHECK (visibility IN ('private', 'role_shared', 'project_shared', 'org_shared', 'system')),
    CHECK (status IN ('active', 'tentative', 'superseded', 'contradicted', 'expired', 'deleted', 'redacted')),
    CHECK (confidence >= 0 AND confidence <= 1)
);
```

`scope_type`, `scope_id`, `namespace`, and the optional owner columns must agree. `scope_id` is the canonical string id for the selected scope: `global`, the org id, project id, user principal id, role id, agent principal id, or session id. A migration should add generated columns or triggers if needed so `scope_id` cannot drift from the typed UUID/text column. Namespaces must be valid for the scope, such as `/project/{project_id}/decisions` only when `scope_type = 'project'` and `project_id` matches.

### role_memory_lenses

Role-specific interpretation of shared truth or project truth.

```sql
CREATE TABLE role_memory_lenses (
    id UUID PRIMARY KEY,
    role_id TEXT NOT NULL,
    project_id UUID REFERENCES projects(id),
    base_memory_fact_id UUID NOT NULL REFERENCES memory_facts(id),
    interpretation TEXT NOT NULL,
    confidence NUMERIC(4,3) NOT NULL,
    status TEXT NOT NULL,
    source_event_id UUID NOT NULL REFERENCES events(id),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CHECK (role_id IN ('designer', 'developer', 'cto', 'cfo', 'coo', 'ceo')),
    CHECK (status IN ('active', 'tentative', 'superseded', 'contradicted', 'expired', 'deleted', 'redacted')),
    CHECK (confidence >= 0 AND confidence <= 1)
);
```

Role lenses are not separate facts. They are interpretations over base facts. When `project_id` is null, the lens is a shared role principle backed only by global or organization truth. When `project_id` is set, the lens is project-specific role interpretation backed by that project's facts or its organization facts. Project-specific truth must never be used as the base for a shared role principle.

### memory_chunks

Searchable text derived from facts, documents, notes, decisions, and summaries.

```sql
CREATE TABLE memory_chunks (
    id UUID PRIMARY KEY,
    source_type TEXT NOT NULL,
    source_id UUID NOT NULL,
    namespace TEXT NOT NULL,
    scope_type TEXT NOT NULL,
    scope_id TEXT NOT NULL,
    title TEXT,
    content TEXT NOT NULL,
    content_hash TEXT NOT NULL,
    trust_level TEXT NOT NULL,
    source_event_id UUID NOT NULL REFERENCES events(id),
    search_vector TSVECTOR,
    redacted_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CHECK (source_type IN ('memory_fact', 'role_memory_lens', 'document', 'summary', 'vault_export')),
    CHECK (trust_level IN (
        'system_trusted',
        'human_approved',
        'user_scoped',
        'agent_private',
        'tool_output',
        'retrieved_untrusted',
        'web_content'
    ))
);
```

### memory_embeddings

Vector index rows. Requires pgvector.

```sql
CREATE EXTENSION IF NOT EXISTS vector;

CREATE TABLE memory_embeddings (
    chunk_id UUID NOT NULL REFERENCES memory_chunks(id) ON DELETE CASCADE,
    embedding_model TEXT NOT NULL,
    embedding_dimension INT NOT NULL,
    embedding vector NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (chunk_id, embedding_model)
);
```

The vector dimension must match the chosen embedding model. If a fixed-dimensional index is preferred, use a model-specific embedding table or a fixed `vector(n)` column after the provider decision is made.

### memory_reviews

Human or broker review state.

```sql
CREATE TABLE memory_reviews (
    id UUID PRIMARY KEY,
    memory_fact_id UUID NOT NULL REFERENCES memory_facts(id),
    review_status TEXT NOT NULL,
    reviewer_id UUID REFERENCES principals(id),
    notes TEXT,
    source_event_id UUID NOT NULL REFERENCES events(id),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CHECK (review_status IN ('pending', 'approved', 'rejected', 'needs_changes'))
);
```

### memory_redactions

Audit-safe record of deletion or redaction decisions.

```sql
CREATE TABLE memory_redactions (
    id UUID PRIMARY KEY,
    target_type TEXT NOT NULL,
    target_id UUID NOT NULL,
    redaction_type TEXT NOT NULL,
    reason TEXT NOT NULL,
    requested_by_principal_id UUID REFERENCES principals(id),
    source_event_id UUID NOT NULL REFERENCES events(id),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CHECK (target_type IN ('event', 'memory_fact', 'role_memory_lens', 'memory_chunk', 'vault_export')),
    CHECK (redaction_type IN ('expire', 'delete', 'redact'))
);
```

Redaction execution must update the target row, remove or replace sensitive payload fields, delete derived chunks and embeddings when required, and leave only audit-safe metadata plus hashes or external pointers needed to prove what was acted on.

### api_idempotency_keys

Request-level idempotency for mutating endpoints.

```sql
CREATE TABLE api_idempotency_keys (
    id UUID PRIMARY KEY,
    principal_id UUID NOT NULL REFERENCES principals(id),
    endpoint TEXT NOT NULL,
    idempotency_key TEXT NOT NULL,
    request_hash TEXT NOT NULL,
    response_status INT,
    response_body JSONB,
    response_content_type TEXT,
    resource_type TEXT,
    resource_id UUID,
    status TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    expires_at TIMESTAMPTZ NOT NULL,
    UNIQUE (principal_id, endpoint, idempotency_key),
    CHECK (status IN ('processing', 'completed', 'failed'))
);
```

Use this for `POST /api/events`, `POST /api/memory/proposals`, and all other mutating endpoints. The outbox still owns worker idempotency; this table owns client request idempotency.

### outbox_jobs

Retry-safe background work queue.

```sql
CREATE TABLE outbox_jobs (
    id UUID PRIMARY KEY,
    job_type TEXT NOT NULL,
    aggregate_type TEXT NOT NULL,
    aggregate_id UUID NOT NULL,
    idempotency_key TEXT NOT NULL UNIQUE,
    payload JSONB NOT NULL,
    status TEXT NOT NULL,
    attempts INT NOT NULL DEFAULT 0,
    available_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    locked_until TIMESTAMPTZ,
    locked_by TEXT,
    last_error TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CHECK (status IN ('pending', 'processing', 'completed', 'failed', 'dead_letter'))
);
```

### Required indexes and triggers

The first migration should include:

- indexes on memory scope: `(scope_type, scope_id, status)`
- indexes on namespace and visibility
- indexes on `source_event_id`
- full-text GIN index on `memory_chunks.search_vector`
- pgvector extension and embedding tables if useful for the initial schema
- unique idempotency index on `(principal_id, endpoint, idempotency_key)`
- `updated_at` trigger for mutable tables
- uniqueness or dedupe indexes for obvious duplicates, such as active memory with the same scope, subject, predicate, and object

Do not require a model-specific vector index in the first migration. If embedding rows are included before the embedding provider is selected, keep `memory_embeddings.embedding` as generic `vector` and store `embedding_model` plus `embedding_dimension`. Add model-specific vector indexes only after the embedding model, dimension, distance operator, and index type are chosen before M6.

## Memory Namespaces

Use namespace strings for access control, retrieval, and UI grouping.

```text
/global/instructions
/global/skills
/org/{org_id}/strategy
/org/{org_id}/policies
/user/{user_id}/profile
/user/{user_id}/preferences
/user/{user_id}/history
/project/{project_id}/facts
/project/{project_id}/decisions
/project/{project_id}/documents
/role/designer/shared
/role/developer/shared
/role/cto/shared
/role/cfo/shared
/role/coo/shared
/role/ceo/shared
/project/{project_id}/role/designer/lens
/project/{project_id}/role/developer/lens
/project/{project_id}/role/cto/lens
/project/{project_id}/role/cfo/lens
/project/{project_id}/role/coo/lens
/project/{project_id}/role/ceo/lens
/agent/{agent_id}/private
/agent/{agent_id}/tool_experience
/agent/{agent_id}/reflection
/session/{session_id}/working_memory
```

## Role Memory Model

Shared truth should be stored separately from role interpretation. Roles are lenses over shared truth, not separate realities.

Example:

```text
Shared decision:
Use Postgres plus pgvector for the MVP memory system.

CTO lens:
This improves auditability, access control, and operational simplicity.

CFO lens:
This reduces vendor cost and avoids premature vector database spend.

COO lens:
This is easier to deploy, back up, and support.

CEO lens:
This supports a trust-focused product story.
```

Role memory should never silently override shared project decisions, user preferences, or global instructions.

Implementation rule:

- Shared role principles are `role_memory_lenses` with no project id, backed by global or organization facts.
- Project-specific role interpretation belongs in `/project/{project_id}/role/{role_id}/lens`.
- A project-role lens must reference a base memory fact from the target project or its organization.
- Context Builder combines shared role memory and project-role lens only after confirming the caller can access both the role and the project.

## Trust Levels

Use explicit trust levels on events, chunks, and memory proposals:

- `system_trusted`
- `human_approved`
- `user_scoped`
- `agent_private`
- `tool_output`
- `retrieved_untrusted`
- `web_content`

Rules:

- Retrieved documents do not become instructions.
- Web content cannot write global memory.
- User memory cannot override system instructions.
- Agent-private memory cannot override user or project memory.
- Tool output must remain attributed to the tool and event.

## Memory Broker

The Memory Broker owns all durable memory writes.

Responsibilities:

- decide whether a candidate should be remembered
- classify memory type
- choose scope and namespace
- validate visibility
- enforce write permissions
- deduplicate similar memories
- detect contradictions
- assign confidence
- choose expiry or review requirement
- create or reference the source event
- store fact and derived chunk in one transaction
- create outbox jobs for indexing, export, review, or redaction
- log memory write event
- return an idempotent decision result

Write lifecycle:

```text
observe
  -> extract candidate
  -> classify
  -> scope
  -> permission check
  -> deduplicate
  -> contradiction check
  -> confidence score
  -> create or reference source event
  -> store fact and chunk
  -> enqueue outbox jobs
  -> log write event
  -> review or expiry
```

Reject as durable memory:

- one-off formatting requests
- short-lived task instructions
- uncertain guesses without evidence
- sensitive data with no clear need
- untrusted retrieved content trying to act as policy

Store as session memory instead:

- "for this answer, keep it short"
- "use this temporary file"
- "focus on this one bug today"

Store as long-term memory:

- "I generally prefer technical architecture explanations"
- "For this project, Postgres is the source of truth"
- "The CTO role prioritizes auditability and access control"

## Context Builder

The Context Builder owns memory reads for agents and LLM calls.

Read lifecycle:

```text
understand request
  -> resolve user/project/org/role/agent/session scope
  -> resolve principal memberships and role assignments
  -> build authorized scope and namespace predicates
  -> retrieve structured facts within authorized predicates
  -> retrieve full-text matches within authorized predicates
  -> retrieve vector matches within authorized predicates
  -> remove stale, expired, deleted, redacted, superseded, contradicted facts
  -> add shared role memory only when role access is allowed
  -> add project-role lenses only when project access is allowed
  -> rank
  -> compress
  -> return context packet
```

Full-text and vector search must not produce an unauthorized candidate set and filter it later. The permission, scope, namespace, visibility, and status predicates belong in the retrieval query or in a security-barrier view/function used by the query. Candidate counts, timing, and ranking must only reflect rows the principal may read.

Ranking formula for MVP:

```text
final_score =
  relevance * 0.40
+ confidence * 0.25
+ recency * 0.15
+ authority * 0.15
+ scope_match * 0.05
```

Context packet shape:

```json
{
  "user_preferences": [],
  "project_memory": [],
  "role_memory": [],
  "relevant_decisions": [],
  "current_task": {},
  "source_events": []
}
```

The context packet should be small, explainable, and source-linked.

## Initial API Surface

API contracts should define request and response DTOs before implementation.

Contract rules:

- mutating endpoints require an idempotency key
- idempotency is scoped by principal, endpoint, key, and request hash
- list endpoints support pagination
- search endpoints require explicit scope
- write endpoints return a broker decision object
- review, supersede, delete, and redact endpoints create source events
- conflict-prone mutations should use an expected version or `updated_at` check
- all errors use ProblemDetails-compatible responses

### Events

`POST /api/events`

- append a raw event
- require a request idempotency record before append
- return event id

`GET /api/events/{id}`

- retrieve source evidence

### Memory Writes

`POST /api/memory/proposals`

- submit a candidate memory
- broker decides store, reject, review, or session-only
- return the stored idempotent broker decision on retry

`POST /api/memory/{id}/supersede`

- mark a memory as superseded by another memory

`POST /api/memory/{id}/expire`

- set expiry or immediately expire

`DELETE /api/memory/{id}`

- soft delete with audit event

`POST /api/memory/{id}/redact`

- redact sensitive content from memory and derived indexes according to policy

### Memory Reads

`GET /api/memory/{id}`

- retrieve one memory fact

`POST /api/memory/search`

- structured, keyword, and semantic search

`POST /api/context/build`

- build a scoped context packet for a request

### Review

`GET /api/reviews/pending`

- list pending memory reviews

`POST /api/reviews/{id}/approve`

- approve a memory

`POST /api/reviews/{id}/reject`

- reject a memory

## Access Control

Use policy-based authorization in ASP.NET Core.

Initial access rules:

| Memory | Read | Write |
| --- | --- | --- |
| Global instructions | all agents | human/admin only |
| User memory | relevant agents | Memory Broker |
| Project memory | project agents | Memory Broker or approved project agent |
| Designer memory | designer, admin | designer through broker |
| Developer memory | developer, CTO, admin | developer through broker |
| CTO memory | CTO, CEO, admin | CTO through broker |
| CFO memory | CFO, CEO, admin | CFO through broker |
| COO memory | COO, CEO, admin | COO through broker |
| CEO memory | CEO, admin | CEO or human approval |
| Agent-private memory | owning agent, admin | owning agent through broker |
| Event log | limited agents, admin | append-only |

Enforcement model:

- API key authentication resolves to a `principal_id` for MVP.
- ASP.NET Core authorization checks coarse endpoint access.
- Application services check fine-grained memory access using project memberships, role assignments, and memory grants.
- The database schema stores enough information to audit why access was allowed.
- Search and context-building queries must apply access filters before or inside retrieval, before ranking or compression.
- Cross-project role memory requires both role access and target project access.

## Obsidian Vault Role

Use the existing `vault/AI Memory System` structure as the human-facing workspace.

Obsidian should be used for:

- architecture decisions
- project notes
- role summaries
- approved skills
- memory review exports
- human-readable summaries

Obsidian should not be used for:

- high-volume event history
- transactional state
- concurrent writes
- permission enforcement
- authoritative user preference storage

MVP rule:

- Obsidian is export-only.
- Imports are a later feature and must enter through event ingestion plus Memory Broker review.
- Vault exports must include source IDs so stale or redacted exports can be regenerated or removed.

Recommended vault mapping:

```text
vault/AI Memory System/
  00 Global/
  10 Users/
  20 Projects/
  30 Roles/
  40 Shared Decisions/
  90 Archive/
```

## Implementation Phases

### Phase 1: Foundation and C# Backend Skeleton

Deliver:

- .NET solution and projects
- Docker Compose for local Postgres plus pgvector, using `pgvector/pgvector:0.8.2-pg17-bookworm` per [Decision 0003](decisions/0003-local-database-runtime.md)
- ASP.NET Core API
- health endpoint
- SQL migration folder and migration runner
- Postgres connection
- first migration with identity, access, event, memory, chunk, review, redaction, and outbox tables
- request idempotency table for mutating endpoints
- local API-key authentication mapped to principals
- coarse authorization policies
- fine-grained access-checking service
- event append endpoint
- memory proposal endpoint
- outbox job creation for derived indexing work
- unit test project
- integration test project

Success criteria:

- local database setup is repeatable
- API starts locally
- tests run
- migrations create constrained core tables and indexes
- event append works
- broker can accept or reject a simple memory proposal
- durable memory cannot be written without a source event
- broker writes event, memory fact, chunk, and outbox job transactionally
- access filters can block a principal from reading another project's memory

### Phase 2: Structured and Role Memory

Deliver:

- `memory_facts` repository
- scope resolver
- namespace model
- principal, project, and role assignment model
- memory status lifecycle
- `role_memory_lenses` repository
- simple search by scope, type, and subject
- source event requirement

Success criteria:

- can store user preference, project decision, role memory, and agent-private memory
- can store shared role memory and project-role lens memory separately
- shared role memory can be reused across projects without leaking project facts
- cannot write memory without provenance
- expired, deleted, and superseded memories are excluded from normal retrieval

### Phase 3: Broker Rules

Deliver:

- candidate classification
- session-only vs durable memory decision
- deduplication
- contradiction detection
- confidence scoring
- review-required state

Success criteria:

- one-off instructions do not become durable memory
- long-term preferences are stored
- contradictory memories are flagged or superseded

### Phase 4: Hybrid Retrieval

Deliver:

- full-text search indexes
- `memory_chunks`
- pgvector embeddings
- semantic search
- hybrid ranking
- permission-aware filtering

Success criteria:

- context builder combines structured, keyword, and vector recall
- vector results are filtered by authority and scope
- context packet includes source links

### Phase 5: Role Agent Workflows

Deliver:

- role-specific context packet templates
- role-specific retrieval policies
- cross-project role memory reuse
- role review workflow

Success criteria:

- CTO, CFO, COO, CEO, Designer, and Developer contexts differ appropriately
- role memory does not create separate conflicting realities
- project facts do not leak across projects unless explicitly promoted

### Phase 6: Review UI and Vault Sync

Deliver:

- TypeScript review dashboard
- pending review queue
- approve/reject/edit flows
- Obsidian export
- archive export

Success criteria:

- human can inspect source event for every memory
- human can approve, reject, edit, expire, delete, or supersede memory
- vault stays readable without becoming the source of truth

### Phase 7: Operations

Deliver:

- health checks
- structured logging
- OpenTelemetry-ready traces
- backup and restore notes
- data retention policy
- production secret handling

Success criteria:

- production deployment and recovery paths are documented
- database health is visible
- backup and restore validation has a documented local path
- memory deletion and expiry behavior is auditable
- production secrets are supplied through runtime configuration and guarded against local/test placeholders

## Testing Strategy

Unit tests:

- domain value objects
- scope resolution
- broker decisions
- ranking formula
- status lifecycle

Integration tests:

- API endpoints
- Postgres migrations
- transaction behavior
- authorization policies
- retrieval filters

End-to-end tests:

- event to memory proposal to context packet
- role-isolated retrieval
- contradiction and supersession flow
- review approval flow

Evaluation tests:

- retrieval relevance
- context packet compactness
- memory write precision
- false-positive durable memory rate
- contradiction detection quality

## First Concrete Build Step

Create the .NET solution and the first migration:

```text
MemorySystem.sln
src/MemorySystem.Api
src/MemorySystem.Application
src/MemorySystem.Domain
src/MemorySystem.Infrastructure
src/MemorySystem.Worker
tests/MemorySystem.UnitTests
tests/MemorySystem.IntegrationTests
docker-compose.yml
migrations/001_initial_memory_schema.sql
```

Then implement:

- `GET /health`
- `POST /api/events`
- `POST /api/memory/proposals`
- local API-key to principal resolution
- first access-checking service
- transactional event + memory fact + chunk + outbox write
- repeated event and memory proposal requests return the original result for the same idempotency key
- a basic broker decision result:

```json
{
  "decision": "stored | rejected | review_required | session_only",
  "reason": "string",
  "memoryId": "uuid or null",
  "sourceEventId": "uuid"
}
```

## Open Decisions

Decide before or during Phase 1 implementation:

- Which embedding provider and vector dimension should be the default?
- Which retention automation jobs should implement [Retention Policy](retention-policy.md) first?
- Which legal-hold and erasure operator endpoints are required for the first production release?

## Current Recommendation On Open Decisions

- Use SQL-first migrations and raw Npgsql for core memory queries through the M1-M3 initial backend path. Dapper may be used only as a small mapping convenience if needed; defer EF Core unless CRUD convenience later outweighs direct SQL clarity.
- Add Docker Compose in Phase 1 because pgvector setup should be repeatable; use `pgvector/pgvector:0.8.2-pg17-bookworm` for the M1-M3 local database runtime.
- Start with local API-key auth mapped to principals, then add full user auth later.
- Make Obsidian export-only in the first version.
- Use [Scenario 0001](scenarios/0001-user-preference-project-decision-cto-context.md) as the first complete M1-M6 throughline: user preference plus project decision plus CTO role context.
- Treat event retention and redaction as product policy, not only technical cleanup. The current MVP policy is captured in [Decision 0036](decisions/0036-retention-and-erasure-policy.md) and [Retention Policy](retention-policy.md).

## References

- ASP.NET Core docs: https://learn.microsoft.com/aspnet/core/
- ASP.NET Core Minimal APIs: https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis
- ASP.NET Core Web API: https://learn.microsoft.com/aspnet/core/web-api/
- ASP.NET Core security: https://learn.microsoft.com/aspnet/core/security/
- .NET support policy: https://dotnet.microsoft.com/platform/support/policy
- Npgsql PostgreSQL provider: https://www.npgsql.org/
- Node.js release guidance for TypeScript tooling runtime: https://nodejs.org/en/about/releases/
