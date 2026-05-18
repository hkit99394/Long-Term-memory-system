# 0001 Data-Access Approach

## Status

Accepted.

## Context

Phase 1 in the architecture plan is broader than the narrow M1 skeleton. It spans the foundation slice, event and proposal writes, auth and access enforcement, and tests that are currently tracked across M1, M2, and M3.

The initial backend path needs predictable SQL behavior for provenance writes, idempotency, access predicates, and later retrieval queries. It also needs to avoid committing too early to a higher-level ORM shape before the schema, authorization rules, and query patterns settle.

## Decision

Use SQL-first migrations plus raw Npgsql for core memory queries through the M1-M3 initial backend path.

Dapper is allowed only as a small mapping convenience if row-to-object mapping becomes noisy. It should not hide important SQL, authorization predicates, transactions, or query plans.

Defer EF Core for now. Reconsider it later only if CRUD convenience clearly outweighs the clarity and control of direct SQL for non-core tables or admin workflows.

## Consequences

- Migrations remain explicit SQL files and are the source of truth for schema changes.
- Repositories and services should keep core event, memory, idempotency, access, and scope queries visible as SQL.
- Transactions for event append, memory proposal writes, idempotency, and outbox work should be managed directly with Npgsql.
- Tests should exercise the SQL against real PostgreSQL rather than relying on ORM behavior.
- EF Core is not part of the initial backend path, but the project can still add it later for bounded CRUD surfaces if the tradeoff changes.

## Vector Index Timing

M1 may create the `vector` extension and embedding tables if they are useful for schema completeness or integration tests. If the first migration includes embedding rows before the embedding provider is selected, keep the column generic as `embedding vector` and record `embedding_model` plus `embedding_dimension` alongside it.

Do not require a fixed model-specific vector index in M1. Model-specific vector indexes should wait until the embedding model, dimension, distance operator, and index type are chosen before M6.
