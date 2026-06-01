# GC-04 Standard And Audit Retention Minimization

Date: 2026-06-01

Status: Implemented

## Purpose

GC-04 adds the first non-ephemeral retention minimization job. It minimizes raw
`standard` and `audit` source event payloads after their configured windows
while preserving source ids, hashes, scope metadata, memory facts, chunks,
embeddings, vault export links, and audit-safe evidence.

This is an operator job, not a new runtime authorization path. It never exports
raw event payloads, memory bodies, review notes, embeddings, API keys, or secret
values.

## Command

Run dry-run mode first:

```bash
MEMORYSYSTEM_RETENTION_MINIMIZATION_MODE=dry-run \
MEMORYSYSTEM_RETENTION_STANDARD_MAX_AGE_DAYS=90 \
MEMORYSYSTEM_RETENTION_AUDIT_MAX_AGE_DAYS=365 \
./scripts/platform-retention-minimization.sh
```

Run execute mode only after reviewing dry-run evidence:

```bash
MEMORYSYSTEM_RETENTION_MINIMIZATION_MODE=execute \
MEMORYSYSTEM_RETENTION_STANDARD_MAX_AGE_DAYS=90 \
MEMORYSYSTEM_RETENTION_AUDIT_MAX_AGE_DAYS=365 \
MEMORYSYSTEM_RETENTION_MINIMIZATION_BATCH_SIZE=500 \
./scripts/platform-retention-minimization.sh
```

The multi-role container exposes the same job at:

```text
/app/scripts/platform-retention-minimization.sh
```

## Selection Rules

The job selects source events when all of these are true:

- `events.retention_class` is `standard` or `audit`
- `events.redaction_status` is `none`
- `events.content` has not already been minimized
- the event is older than the configured retention window
- no active legal hold covers the event
- `external_payload_uri` is null

Events with active legal holds are counted as `legalHoldSkippedEvents`.
Events with `external_payload_uri` are counted as
`externalPayloadSkippedEvents` and left unchanged for the GC-05 external
payload-store retention check.

## Execute-Mode Effects

For selected events, execute mode:

- replaces `events.content` with a payload-safe minimization marker
- preserves event id, event type, scope, trust, sensitivity, timestamp,
  `content_hash`, and source links
- keeps `redaction_status = 'none'` so durable memories can still point to the
  source event without triggering erasure semantics
- clears `memory_reviews.notes` for selected source events
- preserves `memory_facts`, `role_memory_lenses`, `memory_chunks`,
  `memory_embeddings`, and `vault_exports`

The preserved derived records are intentional. Standard and audit minimization
is not erasure; it removes old raw source payloads while keeping reviewed
memory and retrievable projections alive.

## Evidence

The job writes payload-safe evidence JSON with:

- `kind = memorysystem.retention_minimization`
- `mode`
- `standardMaxAgeDays`
- `auditMaxAgeDays`
- `candidateEvents`
- `standardCandidateEvents`
- `auditCandidateEvents`
- `minimizedEvents`
- `reviewNotesCleared`
- `legalHoldSkippedEvents`
- `externalPayloadSkippedEvents`
- `validationFailureCount`
- `targetSetHash`
- `preservedDerivedCopies`
- `clearedDerivedCopies`

Default evidence path:

```text
/tmp/memorysystem-governance-evidence/retention-minimization-evidence.json
```

## Metrics

The job emits Prometheus-compatible metrics:

- `memorysystem_retention_minimization_success`
- `memorysystem_retention_minimization_candidates`
- `memorysystem_retention_minimization_minimized_events`
- `memorysystem_retention_minimization_review_notes_cleared`
- `memorysystem_retention_minimization_legal_hold_skipped`
- `memorysystem_retention_minimization_external_payload_skipped`
- `memorysystem_retention_minimization_failures`
- `memorysystem_retention_minimization_timestamp_seconds`

Each metric is labeled with `environment` and `mode`.

## Promotion Rule

For pilot and production, run a dry run before execute mode. Execute mode passes
only when `validationFailureCount` is zero and the evidence target set matches
the reviewed dry-run target set or an operator records why the set changed.

## Remaining Work

GC-05 now adds external payload-store retention checks for events skipped by
this job because they still have `external_payload_uri`. GC-06 should link the
retention, erasure replay, external payload, audit export, release, benchmark,
alert-route, and permission-drift evidence into a compliance evidence package.
