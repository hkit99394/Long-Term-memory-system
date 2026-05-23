# 0013 Memory Facts Repository

## Status

Accepted.

## Context

M3 added direct memory fact reads and access checks, but the read path started as a narrow store. M4-01 needs a repository boundary for structured memory facts so application code can store and retrieve user, project, role, and agent-private memory without repeating raw SQL at each call site.

The transactional proposal write path still owns its memory fact, chunk, outbox, and idempotency transaction. The repository is the first general-purpose memory fact data access surface and can be folded into later write workflows when the transaction boundary is broadened.

## Decision

Promote the direct read store into `IMemoryFactRepository`.

The repository supports:

- `FindAsync(id)` for direct memory fact lookup.
- `FindByScopeAsync(query)` for active structured reads by scope, status, optional memory type, and limit.
- `SearchAsync(query)` for simple structured search by scope, status, optional memory type, optional subject text, and limit.
- `StoreAsync(command)` for inserting one memory fact with owner columns derived from a resolved scope.

The PostgreSQL implementation keeps SQL explicit and maps `MemoryScopeResolution` to the correct owner columns for global, organization, user, project, role, agent, and session scopes.

M4-02 extends this boundary with the memory status lifecycle policy recorded in [Decision 0014](0014-memory-status-lifecycle.md).

M4-05 extends this boundary with structured search recorded in [Decision 0017](0017-structured-memory-fact-search.md).

Database-backed tests round-trip:

- user preference memory.
- project decision memory.
- role-scoped memory.
- agent-private memory.

## Consequences

- Direct `GET /api/memory/{id}` now reads through the same repository boundary that future structured memory features can use.
- Repository writes still require callers to provide valid source-event provenance.
- Proposal writes continue to use their existing transaction-specific store until a later refactor can preserve the full idempotency/chunk/outbox transaction through the repository.
