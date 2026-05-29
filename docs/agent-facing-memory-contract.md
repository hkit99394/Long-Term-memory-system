# Agent-Facing Memory Contract

Last reviewed: 2026-05-29

## Purpose

This document defines the v1 product contract for agents that use the long-term
memory system.

The service is already a governed memory backend. The next product boundary is a
stable agent-facing memory support service: agents should be able to find,
store, verify, and correct memory through predictable tool calls without
learning the internal database or endpoint layout.

This contract is the LMSS-01 deliverable. It does not implement new endpoints.
It names the v1 capabilities, targeting rules, response expectations, and
follow-on work needed for OpenAPI, SDK, MCP-style tools, and fact finding.

## Product Boundary

The v1 contract should make the system useful as an LLM memory support service,
not only as an HTTP backend.

An agent should be able to:

- append source evidence before asking the system to remember something
- propose durable memory with an explicit source event
- retrieve scoped context for a task
- query known facts with evidence and policy metadata
- report whether retrieved memory was useful, stale, missing, or noisy
- read source evidence when it is authorized
- understand why memory was included, excluded, rejected, or sent to review

The contract should hide storage details but preserve governance details. Agents
do not need to know table names, index strategy, or migration shape. They do
need scope, provenance, lifecycle, confidence, and policy information.

## Contract Principles

- PostgreSQL remains the source of truth. Tool responses are projections over
  governed records, not independent memory stores.
- Authorization happens before ranking, summarization, or fact packaging.
- Writes are evidence-backed. Durable memory proposals must reference source
  events.
- Idempotent writes are part of the public behavior. Agents should retry safely.
- Targeting is explicit. Agents must provide task scope and role context when
  asking for scoped memory.
- Responses explain provenance. Memory-derived facts should include source ids,
  source links, confidence, and lifecycle state where applicable.
- Sensitive data is minimized. Contracts must not expose raw query text,
  redacted payloads, or unauthorized memory.
- Safety is a gate. A response that leaks cross-scope, deleted, redacted, or
  unauthorized facts is a failed response regardless of usefulness.
- The agent contract is versioned. Breaking shape changes should create a new
  contract version rather than silently changing v1 behavior.

## Shared Targeting Fields

These fields should be documented consistently across OpenAPI, SDK, and tool
schemas.

| Field | Meaning | Required |
| --- | --- | --- |
| `principalId` | Authenticated human, agent, or service principal. Usually resolved from authentication, not supplied by the agent. | Auth |
| `targetScope` | The memory scope the task is about, represented by `scopeType` and `scopeId`. | Read tools |
| `namespace` | Path-like memory namespace, such as `/project/{id}/decisions`. | Write tools, optional filters |
| `roleId` | Role perspective for the task, such as `cto`, `designer`, or `developer`. | Optional |
| `trustLevel` | Source trust level for a write or evidence event. | Write tools |
| `sensitivity` | Sensitivity label used by retention, logging, and redaction policy. | Write tools |
| `retentionClass` | Raw event payload retention class. | Evidence writes |
| `sourceEventId` | Evidence event that justifies a durable memory write or review action. | Durable writes |
| `idempotencyKey` | Stable client key for retrying mutating calls. | Mutating tools |

`principalId` should normally come from the API key, OIDC token, service
account, or future agent identity. The public contract should avoid asking an
LLM to invent a principal.

For current HTTP endpoints, `idempotencyKey` maps to the `Idempotency-Key`
request header. Agent tools and SDKs may expose it as a normal input field, but
the transport mapping must stay explicit in generated examples.

## V1 Tool Surface

The first agent contract should wrap existing behavior before adding new
capabilities.

| Tool | Current API | Purpose | Status |
| --- | --- | --- | --- |
| `memory.appendEvent` | `POST /api/events` | Store source evidence for a later memory or review action. | Existing |
| `memory.propose` | `POST /api/memory/proposals` | Ask the broker whether a candidate should be stored, rejected, reused, or reviewed. | Existing |
| `memory.getContext` | `GET /api/memory/context` | Return a compact, source-linked task context packet. | Existing |
| `memory.recordContextFeedback` | `POST /api/memory/context/feedback` | Record useful, stale, missing, or noisy retrieval feedback without raw query storage. | Existing |
| `memory.readFact` | `GET /api/memory/{id}` | Read one authorized memory fact. | Existing |
| `memory.readEvidence` | `GET /api/events/{id}` | Read one authorized source event. | Existing |
| `memory.queryFacts` | New or facade endpoint | Return fact-finding results with evidence, confidence, contradictions, and policy metadata. | Planned |

The v1 OpenAPI/tool schema should publish these names even if the first
implementation uses the current HTTP endpoints internally.

The input examples below use canonical agent-tool shapes. The current HTTP
surface may map some fields to headers or query parameters rather than JSON
body fields.

## Existing Capability Contracts

### `memory.appendEvent`

Use when an agent has source evidence that may justify future memory.

Input shape:

```json
{
  "idempotencyKey": "client-generated-key",
  "eventType": "user_message",
  "scopeType": "project",
  "scopeId": "project-a",
  "roleId": "cto",
  "trustLevel": "user",
  "retentionClass": "standard",
  "sensitivity": "internal",
  "payload": {}
}
```

Expected output:

```json
{
  "id": "source-event-id"
}
```

Contract rules:

- A mutating call must be retry-safe through `idempotencyKey`.
- The response should not echo sensitive payload text unless a read endpoint is
  explicitly called later by an authorized principal.
- The event id is the durable evidence handle for proposal and review calls.

### `memory.propose`

Use when an agent believes something should become durable memory.

Input shape:

```json
{
  "idempotencyKey": "client-generated-key",
  "sourceEventId": "source-event-id",
  "memoryType": "decision",
  "scopeType": "project",
  "scopeId": "project-a",
  "namespace": "/project/project-a/decisions",
  "subject": "schema migrations",
  "predicate": "use",
  "object": "SQL-first migrations plus raw Npgsql",
  "confidence": 0.92,
  "trustLevel": "human_approved",
  "sensitivity": "internal",
  "roleId": "cto"
}
```

Expected output:

```json
{
  "decision": "stored",
  "reason": "accepted",
  "memoryId": "memory-fact-id",
  "sourceEventId": "source-event-id",
  "candidateKind": "decision",
  "confidence": 0.92
}
```

Contract rules:

- Durable memory must reference source evidence.
- `decision` should stay in the known set: `stored`, `rejected`,
  `review_required`, or `session_only`.
- Rejections and review decisions should include a usable reason.
- Policy-level writes from untrusted evidence should be rejected or routed to
  review instead of silently stored.

### `memory.getContext`

Use before an LLM answers a task that may benefit from memory.

Input shape:

```json
{
  "query": "Plan the next migration for retrieval feedback metrics.",
  "targetScope": {
    "scopeType": "project",
    "scopeId": "project-a"
  },
  "roleId": "cto",
  "limit": 12
}
```

Expected output groups:

- `userPreferences`
- `projectMemory`
- `roleMemory`
- `relevantDecisions`
- `sourceEvents`

Each item should include:

- memory kind
- source type and source id
- namespace and scope
- content
- rank
- trust level
- source event id and link
- ranking explanation

Contract rules:

- The context packet is for answer construction, not for unrestricted browsing.
- Retrieved memory must already be authorized.
- Current, active memory should be preferred over stale, deleted, redacted, or
  superseded memory.
- Agents should cite or preserve source ids when making project-specific claims.

### `memory.recordContextFeedback`

Use after an agent or reviewer evaluates a context packet item.

Input shape:

```json
{
  "query": "Plan the next migration for retrieval feedback metrics.",
  "targetScopeType": "project",
  "targetScopeId": "project-a",
  "roleId": "cto",
  "sourceType": "memory_fact",
  "sourceId": "memory-fact-id",
  "feedbackType": "useful"
}
```

Expected output:

```json
{
  "id": "feedback-id",
  "retrievalMode": "context_packet",
  "queryHash": "sha256:...",
  "feedbackType": "useful",
  "createdAt": "2026-05-29T00:00:00Z"
}
```

Contract rules:

- `feedbackType` must be one of `useful`, `stale`, `missing`, or `noisy`.
- Raw query text must not be stored.
- `useful`, `stale`, and `noisy` feedback should identify the retrieved source.
- `missing` feedback may omit source id because it points to absent memory.

## Planned Fact-Finding Contract

`memory.queryFacts` is the main new v1 contract. It should answer "what does the
system know, with evidence and policy context?" rather than building an LLM
prompt packet.

Input shape:

```json
{
  "query": "What migration strategy is accepted for Project A?",
  "targetScope": {
    "scopeType": "project",
    "scopeId": "project-a"
  },
  "roleId": "cto",
  "namespaces": ["/project/project-a/decisions"],
  "memoryTypes": ["decision", "preference", "summary"],
  "includeContradictions": true,
  "includeExcluded": true,
  "limit": 8
}
```

Expected output shape:

```json
{
  "query": "What migration strategy is accepted for Project A?",
  "targetScope": {
    "scopeType": "project",
    "scopeId": "project-a"
  },
  "roleId": "cto",
  "facts": [
    {
      "id": "memory-fact-id",
      "claim": "Project A uses SQL-first migrations plus raw Npgsql.",
      "memoryType": "decision",
      "status": "active",
      "confidence": 0.95,
      "scopeType": "project",
      "scopeId": "project-a",
      "namespace": "/project/project-a/decisions",
      "sourceEventIds": ["source-event-id"],
      "sourceLinks": ["/api/events/source-event-id"],
      "policy": {
        "authorized": true,
        "trustLevel": "human_approved",
        "sensitivity": "internal"
      }
    }
  ],
  "contradictions": [],
  "excluded": [
    {
      "reason": "not_authorized",
      "count": 1
    }
  ],
  "warnings": [],
  "overallConfidence": 0.95
}
```

Fact-finding rules:

- Return facts, not a final prose answer.
- Include source ids for all returned facts.
- Include contradiction summaries when active, superseded, contradicted, or
  tentative records materially affect the answer.
- Include exclusion summaries without leaking unauthorized content.
- Include policy metadata: authorization status for returned items, trust level,
  sensitivity, lifecycle status, and whether evidence is current.
- Prefer active facts by default. Inactive memory should appear only when needed
  for correction, contradiction, or audit explanation.
- Do not expose Project B facts to Project A callers, even as examples.

## Error And Safety Semantics

Agent-facing errors should be stable and easy to recover from.

| Case | Expected behavior |
| --- | --- |
| Missing authentication | Return unauthorized. |
| Invalid targeting fields | Return invalid request with field-level guidance where possible. |
| Scope exists but caller lacks access | Return forbidden or no accessible result without leaking content. |
| Source evidence is inaccessible | Reject the write or review action. |
| Memory was deleted or redacted | Do not return content in normal retrieval or fact finding. |
| Provider unavailable | Return degraded or provider-specific failure without storing partial unsafe state. |
| Idempotent retry | Return the original completed response when request hash matches. |
| Idempotency conflict | Reject the retry when the same key is reused for a different body. |

## Versioning

The first public contract version is `v1`.

Versioning should cover:

- OpenAPI document version
- SDK package version
- tool schema version
- benchmark suite version
- response-shape compatibility

Compatible additions may add optional fields. Breaking changes should create a
new contract version or a named preview capability.

## Non-Goals For LMSS-01

- No new endpoint implementation.
- No OpenAPI generation.
- No SDK generation.
- No MCP server or connector implementation.
- No domain model refactor.
- No new benchmark scenario.

Those are follow-on LMSS tasks.

## Acceptance Criteria

LMSS-01 is complete when:

- the agent-facing contract is documented
- the v1 tool surface is named
- shared targeting fields are defined
- existing endpoint mappings are identified
- the planned `memory.queryFacts` contract is specified at a product/API level
- safety and error semantics are documented
- backlog and product plan point to this contract
