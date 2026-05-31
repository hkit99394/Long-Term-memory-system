# Decision 0044: Domain Model Extraction Slice

Date: 2026-05-31

Status: Accepted

## Context

LR-04 starts the Long Run Domain model extraction track. The project already has
stable concepts for memory scope, namespace, role id, trust level, lifecycle
status, retention class, sensitivity, source evidence, and retrieval feedback.
Those concepts currently live mostly in Application and Infrastructure while the
production-pilot API, context product, benchmark, and governance surfaces have
been stabilizing.

`MemorySystem.Domain` is intentionally thin today. The next risk is duplicated
string normalization and policy vocabulary across layers, but a large extraction
would be more dangerous than useful because these concepts sit on authorization,
source evidence, retrieval, review, and governance paths.

## Decision

Adopt [LR-04 Domain Model Extraction Plan](../domain-model-extraction-lr04.md)
as the planning-first extraction scope.

The Domain extraction track will:

- inventory stable concepts before moving code
- add pure Domain value objects and vocabulary types first
- keep API DTOs, OpenAPI shape, database columns, SQL migrations, and endpoint
  behavior stable during extraction
- preserve Application facades where existing tests or callers depend on class
  names, error wording, or string values
- use compatibility tests before each migration step
- move Infrastructure mapping to Domain value objects only at repository
  boundaries
- run benchmark smoke checks when fact-finding, context packets, or
  feedback-to-ranking paths are touched

The first target types are `MemoryScope`, `MemoryScopeType`, `MemoryScopeId`,
`MemoryNamespace`, `MemoryRoleId`, `MemoryTrustLevel`,
`MemoryLifecycleStatus`, `MemoryRetentionClass`, `MemorySensitivity`,
`SourceEvidenceReference`, and later `MemoryRetrievalFeedbackType`.

## Consequences

- LR-04 completes as a plan and guardrail, not as a hidden behavior change.
- Follow-on `DM-*` work can extract one concept family at a time.
- Existing clients keep the current API contract while internals become easier
  to reason about.
- Database schema and migration history do not change as part of this slice.
- Compatibility tests become the safety rail for refactoring authorization,
  lifecycle, retention, source evidence, and context behavior.
- If a later extraction step needs contract or schema churn, it requires a new
  decision instead of being bundled into Domain cleanup.
