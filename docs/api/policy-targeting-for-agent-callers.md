# Policy Targeting For Agent Callers

Last reviewed: 2026-06-06

## Purpose

This guide is the LMSS-06 caller-facing policy contract for agents and SDKs.
It explains how to target memory requests without learning the database schema.

The short rule is:

```text
Authentication identifies who is asking. Scope says what the task is about.
Namespace says which memory lane is being accessed. Role narrows the
perspective. Source events prove writes.
```

## Quick Defaults

Before planning, coding, reviewing, or releasing project work, agents must call
`GET /api/memory/context`. Use `POST /api/memory/query-facts` when decisions or
facts shape the work. After the task, record context feedback as `useful`,
`stale`, `wrong`, `sensitive`, `over_broad`, or `missing`.

Use these defaults unless the task clearly needs something stricter:

| Field | Default | Why |
| --- | --- | --- |
| `principalId` | Omit it. | The API key resolves the authenticated principal. |
| `targetScope` | Project scope for project work, user scope for personal preferences. | Keeps retrieval bounded to the task. |
| `namespace` | Match the target scope and memory category. | Authorization grants are namespace-based. |
| `roleId` | Omit unless the task needs a role perspective. | Role targeting is a privilege boundary. |
| `trustLevel` | `user_scoped` | Accepted external default for user-provided evidence. |
| `retentionClass` | `standard` | Keeps source evidence long enough for normal memory provenance. |
| `sensitivity` | `none` | Use stricter labels only when the payload requires it. |
| `sourceEventId` | Always use the event that justifies the memory. | Durable memory must be evidence-backed. |

## Principal Resolution

Current HTTP calls authenticate with `X-Api-Key`. The API key maps to one
principal id, and that principal is used for authorization.

Agent callers should normally not send `principalId`.

Current exceptions:

- `POST /api/events` accepts optional `principalId`, but it must match the
  authenticated principal.
- `POST /api/memory/proposals`, `GET /api/memory/context`,
  `POST /api/memory/query-facts`, and feedback calls use the authenticated
  principal.
- Future SDKs may expose principal information for diagnostics, but agents
  should not invent or switch principals.

Bad pattern:

```json
{
  "principalId": "someone-else",
  "eventType": "user_message"
}
```

Good pattern:

```json
{
  "eventType": "user_message",
  "scopeType": "project",
  "scopeId": "33333333-3333-4333-8333-333333333333"
}
```

## Scope Types

Supported scope types are:

| Scope type | Scope id | Use |
| --- | --- | --- |
| `global` | `global` | System-wide shared memory. Use sparingly. |
| `org` | Organization GUID | Organization-wide memory. |
| `project` | Project GUID | Project decisions, facts, and project-role lenses. |
| `user` | Authenticated principal GUID | Personal preferences and private user memory. |
| `role` | Supported role id | Shared role-level memory. |
| `agent` | Agent principal GUID | Agent-private memory. |
| `session` | Non-`global` session id | Short-lived task/session evidence or instructions. |

Supported roles are:

```text
product_owner, cto, security_professional, it_manager, developer, tester_qa,
release_manager, knowledge_steward, designer, cfo, coo, ceo
```

### Write Scope Rules

For source events:

- `scopeType` is required.
- `user` scope must use the authenticated principal as `scopeId`.
- `agentPrincipalId` is only valid for `agent` scope and must match `scopeId`
  when supplied.
- `roleId` is only valid for `role`-scoped events and must match `scopeId`.
- `project` scope uses a project GUID. `scopeOrgId` may be supplied and must
  match the active project organization when checked.
- `session` scope requires a non-`global` id. If it is a GUID and
  `conversationId` is supplied, they must match.

For memory proposals:

- `scopeType`, `scopeId`, and `namespace` are required.
- The namespace must start with the canonical scope prefix.
- User-scoped memory proposals must use the authenticated principal as
  `scopeId`.
- Durable writes require both scope access and namespace write access.

### Read Target Scope Rules

Read tools use target scope to bound retrieval:

- `GET /api/memory/context` uses query parameters `scopeType` and `scopeId`.
- `POST /api/memory/query-facts` uses a JSON `targetScope` object.
- Target scope is optional on some reads, but project work should provide it.
- If one of `scopeType` or `scopeId` is supplied, both must be supplied.

Project fact query example:

```json
{
  "query": "What migration strategy is accepted?",
  "targetScope": {
    "scopeType": "project",
    "scopeId": "33333333-3333-4333-8333-333333333333"
  },
  "memoryTypes": ["decision"],
  "limit": 8
}
```

## Namespace Rules

Namespaces are path-like strings. They must:

- start with `/`
- include a supported scope segment
- avoid empty, `.`, or `..` path segments
- match the request scope for durable memory writes
- match an authorized grant prefix for reads, writes, reviews, or admin actions

Canonical namespace patterns:

| Scope | Pattern | Example |
| --- | --- | --- |
| Global | `/global/{category}` | `/global/principles` |
| Organization | `/org/{orgId}/{category}` | `/org/22222222-2222-4222-8222-222222222222/principles` |
| Project | `/project/{projectId}/{category}` | `/project/33333333-3333-4333-8333-333333333333/decisions` |
| User | `/user/{principalId}/{category}` | `/user/11111111-1111-4111-8111-111111111111/preferences` |
| Role | `/role/{roleId}/{category}` | `/role/cto/shared` |
| Agent | `/agent/{agentPrincipalId}/{category}` | `/agent/aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa/private` |
| Session | `/session/{sessionId}/{category}` | `/session/session-1/instructions` |

Role-aware namespace patterns:

| Scope | Pattern | Example |
| --- | --- | --- |
| Organization role lens | `/org/{orgId}/role/{roleId}/lens` | `/org/22222222-2222-4222-8222-222222222222/role/cto/lens` |
| Project role lens | `/project/{projectId}/role/{roleId}/lens` | `/project/33333333-3333-4333-8333-333333333333/role/cto/lens` |

Grant prefixes apply to the exact namespace and its children. For example, a
read grant on `/project/{projectId}/decisions` covers
`/project/{projectId}/decisions/api` but not
`/project/{projectId}/role/cto/lens`.

Event append calls do not send a namespace. Most event appends authorize by
scope membership. Two otherwise broad scopes also require synthetic namespace
write grants:

- `global` events require write access to `/global/events`
- `session` events require write access to `/session/{sessionId}/events`

## Role Targeting

`roleId` has different meanings depending on the operation:

| Operation | Use |
| --- | --- |
| `memory.appendEvent` | Only valid for `role`-scoped events and must match `scopeId`. |
| `memory.propose` | Required for role-lens proposals and must match the role namespace. |
| `memory.getContext` | Optional role perspective for ranking and filtering. |
| `memory.queryFacts` | Optional role perspective for filtering role-specific facts. |
| `memory.recordContextFeedback` | Optional metadata describing the retrieval perspective being evaluated. |

Role-specific memory is not just a tag. It is an access boundary:

- role namespaces require the caller to have the matching role assignment
- role namespace grants may authorize assigned-role callers
- Project A CTO memory must not leak to Project A CFO callers or Project B
  callers

Project CTO fact query example:

```json
{
  "query": "What should the CTO know about migration risk?",
  "targetScope": {
    "scopeType": "project",
    "scopeId": "33333333-3333-4333-8333-333333333333"
  },
  "roleId": "cto",
  "namespaces": [
    "/project/33333333-3333-4333-8333-333333333333/role/cto/lens"
  ],
  "includeExcluded": true
}
```

## Trust Level

`trustLevel` describes where the evidence came from. It is not an authorization
override.

Supported values:

| Trust level | External callers | Typical use |
| --- | --- | --- |
| `user_scoped` | Yes | User-provided source evidence. |
| `agent_private` | Yes | Agent-private notes or outputs. |
| `tool_output` | Yes | Data returned by a tool. |
| `retrieved_untrusted` | Yes | Retrieved content from an untrusted source. |
| `web_content` | Yes | Web or external content. |
| `human_approved` | No | Internal reviewed or approved evidence. |
| `system_trusted` | No | Internal system-generated trusted evidence. |

Write endpoints reject externally supplied `human_approved` and
`system_trusted`. Those labels are reserved for trusted internal workflows.

When in doubt, use:

```json
{
  "trustLevel": "user_scoped"
}
```

## Retention Class

`retentionClass` applies to raw event payloads, not to every derived memory
projection.

Supported values:

| Retention class | Use |
| --- | --- |
| `ephemeral` | Short-lived task or debugging evidence. |
| `standard` | Normal evidence for memory proposals. |
| `audit` | Review, deletion, redaction, or operational decision evidence. |
| `legal_hold` | Evidence that must be preserved until the hold is released. |
| `erasure_requested` | Evidence approved for payload removal. |

Agent callers should normally use `standard`. Use `ephemeral` for short-lived
task evidence that should not become durable memory. Use `audit` when the event
documents review, deletion, redaction, or correction.

Normal reads hide events marked `erasure_requested` or redacted. Database guards
also block new memory facts from referencing redacted or erasure-requested
source events.

## Sensitivity

Supported sensitivity labels:

| Sensitivity | Use |
| --- | --- |
| `none` | Default non-sensitive evidence. |
| `personal` | Personal data. |
| `secret` | Secrets or confidential material. |
| `regulated` | Regulated data requiring stricter handling. |

Rules:

- Sensitive payloads must not be repeated in logs, health checks, error
  messages, or stale markers.
- Proposals inherit the more restrictive sensitivity between the proposal and
  its source event.
- `secret` and `regulated` memory should be proposed only when the product
  genuinely needs durable memory for that data.

## Source Event Rules

Durable memory writes are evidence-backed.

Correct write sequence:

1. Call `memory.appendEvent` with the source evidence.
2. Keep the returned event id.
3. Call `memory.propose` with `sourceEventId` and matching scope fields.
4. Retry only with the same `Idempotency-Key` and identical body for idempotent
   write endpoints.

Source event requirements:

- `sourceEventId` must exist.
- It must be accessible to the authenticated principal.
- It must match the proposed memory scope.
- It must not be redacted.
- It must not have `retentionClass = erasure_requested`.
- Durable memory proposals should not copy source payload text into logs or
  error messages.

Event append example:

```json
{
  "eventType": "user_message",
  "scopeType": "project",
  "scopeId": "33333333-3333-4333-8333-333333333333",
  "trustLevel": "user_scoped",
  "retentionClass": "standard",
  "sensitivity": "none",
  "payload": {
    "decision": "Use SQL-first migrations plus raw Npgsql."
  }
}
```

Memory proposal example:

```json
{
  "sourceEventId": "66666666-6666-4666-8666-666666666666",
  "memoryType": "decision",
  "scopeType": "project",
  "scopeId": "33333333-3333-4333-8333-333333333333",
  "namespace": "/project/33333333-3333-4333-8333-333333333333/decisions",
  "visibility": "project_shared",
  "subject": "schema migrations",
  "predicate": "use",
  "object": "SQL-first migrations plus raw Npgsql",
  "confidence": 0.92,
  "trustLevel": "user_scoped",
  "sensitivity": "none"
}
```

## Endpoint Field Map

| Tool | Policy fields |
| --- | --- |
| `memory.appendEvent` | `eventType`, `scopeType`, `scopeId`, optional `conversationId`, optional `agentPrincipalId`, optional `roleId`, `trustLevel`, `retentionClass`, `sensitivity`, `payload` |
| `memory.propose` | `sourceEventId`, `memoryType`, `scopeType`, `scopeId`, `namespace`, `visibility`, `subject`, `predicate`, `object`, `confidence`, `trustLevel`, `sensitivity`, optional `roleId`, optional `baseMemoryFactId` |
| `memory.getContext` | `q`, optional `scopeType`, optional `scopeId`, optional `roleId`, `limit` |
| `memory.queryFacts` | `query`, optional `targetScope`, optional `roleId`, optional `namespaces`, optional `memoryTypes`, optional `includeContradictions`, optional `includeExcluded`, `limit` |
| `memory.recordContextFeedback` | `query` or `packetId`, optional `itemId`, optional `targetScopeType`, optional `targetScopeId`, optional `roleId`, optional `sourceType`, optional `sourceId`, `feedbackType` |
| `memory.readFact` | Path `id`; authorization uses the authenticated principal and the fact's stored scope and namespace. |
| `memory.readEvidence` | Path `id`; authorization uses the authenticated principal and the event's stored scope. |

Canonical durable `memoryType` values are `goal`, `target`, `fact`,
`decision`, `rationale`, `risk`, `assumption`, `constraint`, `requirement`,
`release_evidence`, and `role_lens`. New role-lens proposals should use
`role_lens`.

## Common Mistakes

| Mistake | Result | Fix |
| --- | --- | --- |
| Supplying `principalId` for another user. | Invalid request. | Omit `principalId`; let auth resolve it. |
| Supplying only `scopeType` or only `scopeId`. | Invalid request. | Send both fields together. |
| Using a namespace outside the requested scope. | Invalid or forbidden proposal. | Use the canonical namespace prefix for the scope. |
| Using `roleId` on a non-role event. | Invalid event append. | Use role namespaces or read-time role targeting instead. |
| Querying role memory without assignment. | Empty result or not found without content leak. | Grant the role assignment and namespace access. |
| Sending item-level feedback without `sourceType` and `sourceId`. | Invalid request. | Send the item `sourceType` and `sourceId` returned by `memory.getContext`. |
| Sending `missing` feedback with `itemId` or `sourceId`. | Invalid request. | Send packet-level `missing` feedback with `packetId`, target scope, and role only. |
| Sending `human_approved` from an external client. | Invalid request. | Use `user_scoped`, `tool_output`, or another external trust level. |
| Referencing redacted evidence. | Proposal rejected or blocked by database guard. | Append fresh, allowed evidence. |
| Expecting unauthorized exclusion counts. | Count is withheld. | Treat `countDisclosure = withheld` as intentional. |

## Safety Expectations

Agent callers should preserve policy metadata in downstream prompts and UI:

- cite or preserve `sourceEventIds` for memory-derived claims
- treat `confidence` as a ranking and review signal, not a guarantee
- treat `lifecycleStatus != active` as correction or audit context
- avoid final answers based on redacted, deleted, or unauthorized memory
- do not infer that hidden unauthorized memory exists from missing counts

The service may return fewer facts than expected when policy filters apply.
That is the correct behavior.
