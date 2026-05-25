# Testing Commands

## Purpose

This document records the basic local and CI-ready commands for the .NET solution. The same commands should stay runnable on a developer machine and in a Linux CI worker.

## Fast Verification

Use this path for normal code changes that do not require a live PostgreSQL dependency:

```bash
dotnet restore MemorySystem.sln
dotnet build MemorySystem.sln --configuration Release --no-restore
dotnet test MemorySystem.sln --configuration Release --no-build --filter "Category!=Database"
```

The fast test command intentionally excludes tests marked `Category=Database`. Database tests use `MEMORYSYSTEM_TEST_POSTGRES_CONNECTION_STRING` when it is set; otherwise they automatically use the default Docker Compose PostgreSQL connection when `127.0.0.1:55432` is reachable. If neither is available, unfiltered local solution test runs skip database tests cleanly instead of failing during setup.

## Database-Backed Integration Verification

Use this path after the Release build when schema, migration, PostgreSQL health, Npgsql, or pgvector behavior changes:

```bash
docker compose up -d --wait postgres
dotnet test tests/MemorySystem.IntegrationTests/MemorySystem.IntegrationTests.csproj --configuration Release --no-build --filter "Category=Database"
docker compose stop postgres
```

Docker Compose maps PostgreSQL to local port `55432` by default to avoid colliding with a separately installed PostgreSQL on `5432`. If local port `55432` is already occupied, run the Docker service on a different host port and use the same port in the test connection string:

```bash
MEMORYSYSTEM_POSTGRES_PORT=55433 docker compose up -d --wait postgres
MEMORYSYSTEM_TEST_POSTGRES_CONNECTION_STRING="Host=127.0.0.1;Port=55433;Database=memory_system;Username=memory_system;Password=memory_system_dev_password" \
  dotnet test tests/MemorySystem.IntegrationTests/MemorySystem.IntegrationTests.csproj --configuration Release --no-build --filter "Category=Database"
docker compose stop postgres
```

If Docker reports PostgreSQL unhealthy, or the database tests fail because the documented `memory_system` role or database does not exist, the named local development volume may have been created with older credentials. For local development only, reset it with:

```bash
docker compose down -v
docker compose up -d --wait postgres
```

## CI Command Sequence

The repository includes a GitHub Actions workflow at `.github/workflows/ci.yml` that runs both the fast slice and the PostgreSQL-backed `Category=Database` slice. The database test attributes skip locally when neither `MEMORYSYSTEM_TEST_POSTGRES_CONNECTION_STRING` nor the default local Docker PostgreSQL is available, but they do not skip when `CI=true`; a CI worker missing the database connection fails instead of reporting a green run with the database suite absent.

A minimal CI job should run the database-backed section in a shell that stops PostgreSQL even when tests fail:

```bash
set -euo pipefail

dotnet restore MemorySystem.sln
dotnet build MemorySystem.sln --configuration Release --no-restore
dotnet test MemorySystem.sln --configuration Release --no-build --filter "Category!=Database"

docker compose up -d --wait postgres
trap 'docker compose stop postgres' EXIT

dotnet test tests/MemorySystem.IntegrationTests/MemorySystem.IntegrationTests.csproj --configuration Release --no-build --filter "Category=Database"
```

The fast test command covers deterministic application policy, including namespace parsing, scope normalization, broker decisions, broker candidate classification, confidence scoring, session-only task instruction handling, active-memory dedupe and contradiction review routing, embedding provider configuration and deterministic adapter behavior, access authorization, memory status lifecycle policy, and direct memory read authorization.

The database-backed command currently covers the migration runner, migration checksum mismatch and history-gap rejection, PostgreSQL and outbox health endpoint signals, scoped event constraints, project/organization scope consistency, memory fact scope/owner/namespace consistency, memory fact status filtering, structured memory fact search, authorized full-text memory search, authorized pgvector semantic search, hybrid memory ranking, role memory lens repository round-trips and indexing outbox writes, role-lens base fact scope validation, API-key principal resolution, request idempotency, event append including global/session grant checks, minimal broker proposal decisions, candidate kind and confidence propagation, session-only proposal handling, exact duplicate proposal reuse, role-lens duplicate reuse, similar and conflicting active-memory review routing, transactional memory proposal writes, role-lens proposal writes, API-to-worker indexing completion with embedding row creation, provenance enforcement, request scope resolution, membership/grant access-control checks, the blocked cross-project direct read case, and memory fact repository round-trips with indexing outbox writes. Later milestones should extend this path with context retrieval and deeper broker tests.
