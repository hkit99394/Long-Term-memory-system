# 0036 Retention and Erasure Policy

## Status

Accepted.

## Context

M8-03 needs a concrete retention policy for raw event payloads. Earlier decisions intentionally stored M2 event payloads inline first, while the schema already included retention and redaction fields for later operational readiness.

The system must preserve enough audit evidence for memory provenance without keeping sensitive payloads forever or copying erased content into derived stores.

## Decision

Adopt [Retention Policy](../retention-policy.md) as the MVP policy for:

- raw event payload retention windows
- sensitivity handling
- legal hold behavior
- erasure workflow expectations
- audit-safe metadata preservation
- known implementation gaps

Default raw payload retention targets:

| Retention class | Raw payload target |
| --- | --- |
| `ephemeral` | up to 7 days |
| `standard` | up to 90 days |
| `audit` | up to 1 year |
| `legal_hold` | until the hold is released |
| `erasure_requested` | removed or replaced after approved erasure, target completion 30 days |

Audit metadata can remain after payload minimization when it is needed to preserve provenance, prove an action, support idempotent retries, or explain why a memory was hidden.

Legal hold overrides payload minimization and erasure until release. Erasure must remove or mask raw payloads and derived copies while keeping ids, hashes, scope metadata, timestamps, source event ids, lifecycle status, and redaction records.

## Consequences

- M8 has a concrete policy without pretending that all retention workers already exist.
- Existing schema fields now have documented semantics.
- Future retention automation has a checklist and retention matrix to implement against.
- Backup/restore work must account for erased payloads and legal-hold preservation.
