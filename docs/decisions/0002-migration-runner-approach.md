# 0002 Migration Runner Approach

## Status

Accepted.

## Context

[Decision 0001](0001-data-access-approach.md) selects SQL-first migrations plus raw Npgsql for the M1-M3 initial backend path. The project still needs a repeatable way to apply those SQL files in local development and integration tests before M1 implementation starts.

The migration path should keep schema policy explicit, testable against real PostgreSQL, and easy to replace later if production operations need more tooling. It should not introduce ORM-generated migrations or a heavyweight migration framework before the schema and operational shape settle.

## Decision

Use a small in-repo Npgsql-based migration runner for M1-M3.

The runner will:

- Read ordered SQL migration files from the root-level `migrations/` directory.
- Track applied migrations and checksums in a `schema_migrations` table.
- Refuse to continue if an applied migration's checksum no longer matches the recorded checksum.
- Use a PostgreSQL advisory lock so concurrent API, worker, test, or local startup paths cannot apply migrations at the same time.
- Run each migration inside a transaction where PostgreSQL allows it.
- Be callable from local development tooling and integration tests.

Avoid ORM-generated migrations. Do not add a heavyweight migration framework for the initial path unless a later decision identifies a concrete operational need that outweighs the extra dependency and convention surface.

## Placement

The reusable runner implementation belongs in `MemorySystem.Infrastructure` or a small infrastructure-owned migration component.

API, tests, and worker processes may invoke the runner through composition, but they should not own schema policy. SQL files in `migrations/` remain the source of truth for database shape.

## Consequences

- M1 can add `migrations/001_initial_memory_schema.sql` and a runner path without introducing EF Core or framework-specific migration metadata.
- Integration tests can create a real PostgreSQL database, apply ordered SQL, and verify behavior against the same schema path used locally.
- Local development can use the same runner instead of relying on manual SQL application.
- Production deployment can later replace or wrap the runner if needed, while preserving SQL migration files and checksum history.

## Vector Index Timing

The first migration may include the `vector` extension and embedding tables if they are useful for schema completeness or integration tests.

Do not add a model-specific vector index until the embedding model, dimension, distance operator, and index type are chosen before M6. Until then, preserve model and dimension metadata alongside embedding rows and keep semantic index policy undecided.
