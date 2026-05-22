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

The fast test command intentionally excludes tests marked `Category=Database`. Database tests require `MEMORYSYSTEM_TEST_POSTGRES_CONNECTION_STRING` and fail clearly if the connection string is missing.

## Database-Backed Integration Verification

Use this path after the Release build when schema, migration, PostgreSQL health, Npgsql, or pgvector behavior changes:

```bash
docker compose up -d --wait postgres
MEMORYSYSTEM_TEST_POSTGRES_CONNECTION_STRING="Host=127.0.0.1;Port=55432;Database=memory_system;Username=memory_system;Password=memory_system_dev_password" \
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

A minimal CI job should run the database-backed section in a shell that stops PostgreSQL even when tests fail:

```bash
set -euo pipefail

dotnet restore MemorySystem.sln
dotnet build MemorySystem.sln --configuration Release --no-restore
dotnet test MemorySystem.sln --configuration Release --no-build --filter "Category!=Database"

docker compose up -d --wait postgres
trap 'docker compose stop postgres' EXIT

MEMORYSYSTEM_TEST_POSTGRES_CONNECTION_STRING="Host=127.0.0.1;Port=55432;Database=memory_system;Username=memory_system;Password=memory_system_dev_password" \
  dotnet test tests/MemorySystem.IntegrationTests/MemorySystem.IntegrationTests.csproj --configuration Release --no-build --filter "Category=Database"
```

The database-backed command currently covers the migration runner, PostgreSQL health endpoint, scoped event constraints, project/organization scope consistency, API-key principal resolution, request idempotency, event append, minimal broker proposal decisions, transactional memory proposal writes, provenance enforcement, request scope resolution, and membership/grant access-control checks. Later milestones should extend this path with retrieval and deeper broker tests.
