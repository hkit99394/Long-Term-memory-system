# 0005 Event Append Contract

## Status

Accepted.

## Context

M2 needs `POST /api/events` before memory proposals can prove source provenance. Scenario 0001 already models source events with `eventType`, authenticated principal identity, a scope, and a JSON payload.

The event table stores raw evidence in `events.content` with scope columns that must agree with the selected scope type.

## Decision

`POST /api/events` accepts JSON requests shaped like this:

```json
{
  "eventType": "user_message",
  "scopeType": "user",
  "scopeId": "11111111-1111-4111-8111-111111111111",
  "payload": {
    "message": "For technical planning, I prefer concise decision logs."
  }
}
```

The authenticated API principal is always stored as `events.principal_id`. A request may include `principalId` only when it matches the authenticated principal.

The event payload is required, must be a JSON object, and is stored inline in `events.content` for M2. `events.content_hash` stores a `sha256:` hash of the payload JSON text.

The endpoint supports these first scope shapes:

- `global`: `scopeId` is omitted or `global`
- `org`: `scopeId` is the organization id
- `user`: `scopeId` is the authenticated principal id
- `project`: `scopeId` is the project id; the API derives `scope_org_id` from the project row
- `role`: `scopeId` is one supported role id
- `agent`: `scopeId` is the agent principal id
- `session`: `scopeId` is a non-`global` session id

Default metadata:

- `trustLevel`: `user_scoped`
- `retentionClass`: `standard`
- `sensitivity`: `none`

## Runtime Behavior

- The endpoint requires `Idempotency-Key`.
- A successful append returns `201` with `{ "id": "<event id>" }`.
- A retry with the same principal, endpoint, idempotency key, and request hash returns the original event id.
- Reusing the same idempotency key with a different body returns `409 Conflict`.

## Consequences

- M2 source events are durable, auditable, and replay-safe.
- Raw event payload retention is intentionally simple for MVP: store inline first, then apply redaction/erasure policy in later milestones.
- M3 access checks now require scope-appropriate membership, role assignment, or agent identity before project, organization, role, and agent events are appended.
