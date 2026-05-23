# 0014 Memory Status Lifecycle

## Status

Accepted.

## Context

M4-02 needs memory facts to represent the full lifecycle already modeled in the database schema: active, tentative, superseded, contradicted, expired, deleted, and redacted. The structured memory repository introduced in M4-01 also needs a clear default retrieval policy so later search, context-building, review, and redaction paths do not accidentally treat inactive memory as normal recall material.

The database check constraint remains the final safety net, but application code needs a shared status vocabulary before data reaches PostgreSQL.

## Decision

Represent memory fact statuses as application constants in `MemoryFactStatuses`.

The supported statuses are:

- `active`
- `tentative`
- `superseded`
- `contradicted`
- `expired`
- `deleted`
- `redacted`

Normal retrieval defaults to `active` only. Tentative, superseded, contradicted, expired, deleted, and redacted facts can still be queried explicitly by status through the repository for review, audit, and lifecycle workflows.

The repository validates status values for both write commands and scope queries before issuing SQL. Invalid status values fail fast with an argument error instead of relying only on PostgreSQL constraint failures.

Direct memory fact reads hide non-active facts as not found before authorization. This keeps deleted, redacted, expired, contradicted, superseded, and tentative facts out of the normal API read path while preserving their database records for future review and audit workflows.

## Consequences

- Application policy has one shared source for the memory fact status vocabulary.
- Normal structured reads and direct reads exclude inactive lifecycle states by default.
- Lifecycle-specific tools can still request inactive states explicitly through repository queries.
- The database status constraint remains a defense-in-depth check for direct SQL paths and future services.
