# Retention Policy

## Purpose

This policy defines how the memory system keeps, minimizes, erases, and audits raw event payloads and derived memory data.

The policy separates two concerns:

- evidence preservation: enough metadata must remain to prove what happened, who requested it, and which source event authorized it
- payload minimization: raw user, assistant, tool, review, and memory text should not remain longer than its retention class requires

PostgreSQL remains the source of truth. Vault exports, chunks, embeddings, logs, and future external payload stores are projections that must follow the PostgreSQL lifecycle state.

## Retention Classes

`events.retention_class` is the canonical policy label for raw event payloads.

| Retention class | Use | Raw payload target | Audit metadata target |
| --- | --- | --- | --- |
| `ephemeral` | Short-lived task or debugging evidence that should not become the long-lived record. | Up to 7 days. May be minimized earlier after derived memory is accepted or rejected. | Keep event id, actor, scope, event type, trust level, sensitivity, timestamps, content hash, redaction status, and source links while referenced. |
| `standard` | Default source event retention for normal memory proposals and reads. | Up to 90 days. | Keep audit metadata indefinitely while memory or idempotency records reference the event. |
| `audit` | Events that explain review, deletion, expiry, supersession, access, or operational decisions. | Up to 1 year unless legal hold applies. | Keep audit metadata indefinitely. |
| `legal_hold` | Events under an active preservation requirement. | Keep raw payload until the hold is released by an authorized operator. | Keep audit metadata indefinitely. |
| `erasure_requested` | Events approved for erasure or payload removal. | Remove or replace raw payload as soon as the erasure workflow completes; target completion is 30 days. | Keep only audit-safe metadata, content hash, and redaction pointers needed to prove action. |

If a future legal or contractual rule requires a different duration, configure that environment to use a stricter policy. The stricter policy wins.

## Sensitivity Rules

`events.sensitivity` controls handling inside each retention class.

| Sensitivity | Handling rule |
| --- | --- |
| `none` | Follow the retention class. |
| `personal` | Follow the retention class; prefer payload minimization once memory facts, chunks, and exports no longer need the raw text. |
| `secret` | Avoid storing unless required for source evidence. Route related durable memory proposals to review and minimize raw payloads quickly. |
| `regulated` | Treat like `secret`, and require an operator-approved policy before preserving raw payload beyond the standard target. |

Sensitive payloads must not be copied into structured logs, health checks, vault stale markers, or error messages.

## Legal Hold

Legal hold freezes payload minimization and erasure for the affected event or memory scope.

Rules:

- `legal_hold` overrides `ephemeral`, `standard`, `audit`, and `erasure_requested` until the hold is released.
- A hold must reference a source event that records who requested the hold, why it exists, and which target scope or event ids it covers.
- Erasure requests received during legal hold remain recorded but stay pending until release.
- Releasing a hold must be a separate auditable action; after release, the target returns to its prior retention or erasure path.

The current schema can represent held event payloads through `events.retention_class = 'legal_hold'`. A fuller hold-management endpoint and operator workflow remain future implementation work.

## Erasure Workflow

Erasure removes raw payload content while preserving audit-safe evidence.

When erasure is approved:

1. Append or identify a source event that proves the erasure request.
2. Insert a `memory_redactions` row for each target with `redaction_type = 'redact'` or `delete`, the target id, requester, reason, and source event id.
3. Mark affected events so normal reads and source-reference lookups stop using them: `retention_class = 'erasure_requested'` or `redaction_status <> 'none'`.
4. Replace `events.content` with a minimal marker that does not repeat sensitive text. Keep `content_hash`, `redaction_event_id`, `redacted_at`, event type, actor, scope, trust level, sensitivity, and timestamps.
5. For memory facts, use the lifecycle state that matches the action:
   - `deleted` for tombstoned memory whose body can remain in audit-only storage
   - `redacted` when the memory body itself must be removed or masked
   - `expired` when the memory is no longer current but does not require erasure
6. Redact or invalidate derived copies:
   - set affected chunks out of normal retrieval and clear sensitive chunk text when required
   - delete or rebuild embeddings for redacted chunks
   - mark vault exports stale or replace them with stale markers
   - prevent archive exports from returning deleted or redacted bodies

Normal API reads must hide `erasure_requested` and redacted source events. Existing event reads and source-reference lookups already exclude `retention_class = 'erasure_requested'` and non-`none` redaction status.

## Audit Preservation

Audit records should preserve proof without keeping payload text.

Keep:

- event id
- target type and target id
- source event id for the decision
- requested-by principal id when available
- scope type and scope id
- namespace when applicable
- event type and memory type
- trust level and sensitivity labels
- content hash or external payload pointer
- lifecycle status, redaction status, and timestamps
- idempotency record id for mutating API calls when applicable

Do not keep in audit-only records:

- raw event payload text
- memory subject, predicate, or object when the target is redacted
- review notes that repeat sensitive payload
- search query text
- embedding vectors derived from redacted text
- vault document bodies for deleted or redacted memory

## Current Implementation Status

Implemented:

- The schema has `events.retention_class`, `events.redaction_status`, `events.redacted_at`, `events.redaction_event_id`, `events.external_payload_uri`, and `memory_redactions`.
- Event append defaults to `retentionClass = standard` and `sensitivity = none`.
- Event reads and source-reference lookups exclude erasure-requested or redacted events.
- Database guards block new memory facts and role lenses from referencing erasure-requested or redacted source events.
- Normal retrieval excludes inactive and redacted memory through lifecycle and chunk filters.
- Vault export marks deleted, redacted, expired, superseded, and contradicted exported memory stale; archive export does not return deleted or redacted bodies.
- Structured operational logs avoid proposal content, search text, review notes, memory body text, and raw event payloads.

Not yet automated:

- scheduled payload minimization for expired retention windows
- legal-hold create, release, and reporting endpoints
- erasure execution that rewrites event payload markers and clears derived chunk bodies in one transaction
- external payload store retention checks for `external_payload_uri`
- backup pruning or selective restore procedures for erased payloads

Until those workers and operator endpoints exist, retention and erasure actions are policy-defined and schema-supported but require controlled operational execution.

## Operator Checklist

For retention review:

1. Identify the event ids, memory ids, chunks, embeddings, and vault exports that may contain the payload.
2. Check for `legal_hold`; stop if one is active.
3. Confirm the source event authorizing expiry, deletion, redaction, or hold release.
4. Record the action in `memory_redactions` when a target is expired, deleted, or redacted.
5. Update lifecycle and redaction fields before modifying derived projections.
6. Verify normal API reads, retrieval, context packets, and vault exports no longer expose redacted payload.
7. Preserve audit-safe ids, hashes, scope metadata, timestamps, and source links.
