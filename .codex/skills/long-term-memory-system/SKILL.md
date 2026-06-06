---
name: long-term-memory-system
description: Use when an agent should retrieve, store, or review durable memory through the Long-Term Memory System API, including context retrieval, source evidence, memory proposals, namespace policy, review workflows, and feedback.
metadata:
  short-description: Use project memory safely
---

# Long-Term Memory System

Use this skill when a task may depend on durable user preferences, project decisions, role-specific context, prior facts, source evidence, or memory review workflows.

## Configuration

Read these from the runtime environment or secret store:

- `MEMORYSYSTEM_API_BASE_URL`
- `MEMORYSYSTEM_API_KEY`

Never print, log, store, summarize, or expose `MEMORYSYSTEM_API_KEY`.

Send the key on every authenticated request:

```http
X-Api-Key: ${MEMORYSYSTEM_API_KEY}
```

For this project's host-local production container, the usual base URL is:

```text
http://127.0.0.1:8081
```

## Workflow

Before answering, query memory when the user's request may depend on prior preferences, project decisions, durable facts, operational history, or role-specific context.

Use `GET /api/memory/context` for contextual retrieval.

Use `POST /api/memory/query-facts` when structured facts, contradictions, source links, lifecycle status, or policy metadata matter.

Only store memory that is stable, useful later, and grounded in source evidence. Do not store API keys, passwords, tokens, raw credentials, private secrets, or sensitive personal data unless explicitly authorized and classified.

Storage flow:

1. Append source evidence with `POST /api/events`.
2. Propose durable memory with `POST /api/memory/proposals`.
3. Use an `Idempotency-Key` for both requests.
4. Preserve returned source event and memory IDs for future evidence links.

Prefer fresh user instructions over stored memory when they conflict.

## Core Endpoints

```text
GET  /health/ready
POST /api/events
POST /api/memory/proposals
GET  /api/memory/context
POST /api/memory/query-facts
POST /api/memory/context/feedback
GET  /reviews/
GET  /admin/
```

## Policy Basics

Namespaces must match the target scope and authorized grants. Never invent a broader namespace to bypass policy.

Common namespace patterns:

```text
/global/{category}
/org/{orgId}/{category}
/project/{projectId}/{category}
/user/{principalId}/{category}
/role/{roleId}/shared
/project/{projectId}/role/{roleId}/lens
/agent/{agentPrincipalId}/{category}
/session/{sessionId}/{category}
```

Role-specific memory is an access boundary, not just a tag. Only use `roleId` when the caller has the matching role assignment and the task genuinely needs that perspective.

## Feedback

If retrieved memory is useful, stale, wrong, sensitive, over-broad, or missing, call `POST /api/memory/context/feedback`.

Supported feedback values:

```text
useful
stale
wrong
sensitive
over_broad
missing
```

## Failure Handling

- `401`: missing or invalid API key.
- `403`: caller lacks scope, role, or namespace permission.
- Do not bypass authorization.
- Ask for the correct key, principal, scope, role, or grant.

## References

Load these only when needed:

- `references/api-workflows.md`: request examples for context retrieval, fact queries, event append, memory proposal, and feedback.
- `references/policy-targeting.md`: namespace, role, trust, retention, sensitivity, idempotency, and safety rules.
