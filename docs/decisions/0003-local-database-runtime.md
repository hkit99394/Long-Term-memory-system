# 0003 Local Database Runtime

## Status

Accepted.

## Context

M1 needs a repeatable local PostgreSQL runtime with pgvector already available so root-level Docker Compose, local development, the migration runner, and integration tests all exercise the same database shape.

The current source check found official Docker Hub `pgvector/pgvector` tags in the `0.8.2-pg17-*` and `0.8.2-pg18-*` lines, including `pgvector/pgvector:0.8.2-pg17-bookworm`, `pgvector/pgvector:0.8.2-pg17-trixie`, and `pgvector/pgvector:0.8.2-pg18-trixie`. The official Postgres image tags also include current PostgreSQL 18 and 17 lines, including PostgreSQL 17 patch tags such as `postgres:17.10-bookworm`.

For the initial path, the project needs stable local behavior more than the newest major PostgreSQL line. PostgreSQL 17 is a conservative choice for M1-M3 because it is current, well supported, and avoids making PostgreSQL 18 adoption part of the foundation slice.

## Decision

Use `pgvector/pgvector:0.8.2-pg17-bookworm` for M1-M3 local development and integration tests.

This pins:

- pgvector: `0.8.2`
- PostgreSQL major version: `17`
- base OS family: `bookworm`

M1 should add a root-level `docker-compose.yml` that starts this image as the local database service. The compose file should use a named volume for PostgreSQL data, local-only environment variables for database name, user, and password, an exposed local port, and a healthcheck if useful for test startup and developer feedback.

The migration runner and integration tests should connect to this service and apply the root-level ordered SQL migrations against the real PostgreSQL plus pgvector runtime.

## Version Pinning Rules

Do not use `latest`.

Do not use floating tags such as `pgvector/pgvector:pg17` for reproducible local development.

Prefer explicit tags that include pgvector version, PostgreSQL version line, and OS family. Revisit the tag intentionally when upgrading pgvector, PostgreSQL, or the base OS line, and record the reason in a later decision or implementation note.

## Migration Policy

[Decision 0002](0002-migration-runner-approach.md) remains in force. Database schema changes use root-level ordered SQL migration files, an in-repo Npgsql runner, checksums, and a PostgreSQL advisory lock.

Do not introduce ORM-generated migrations for the M1-M3 path.

## Vector Index Timing

M1 may create the `vector` extension and generic embedding tables if they are useful for schema completeness or integration tests.

Do not add a model-specific vector index until the embedding model, dimension, distance operator, and index type are chosen before M6. Until then, keep embedding storage generic and preserve model and dimension metadata alongside embedding rows.

## Consequences

- M1 implementation has a concrete Docker image target: `pgvector/pgvector:0.8.2-pg17-bookworm`.
- Local development and integration tests should use the same PostgreSQL major version and pgvector extension version.
- The project avoids accidental local runtime changes from floating tags.
- PostgreSQL 18 adoption is deferred until there is a deliberate upgrade decision.
