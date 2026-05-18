# Folder Structure

## Purpose

This document defines the target repository layout for the long-term memory system. It is a practical map for M1 and later implementation work.

The structure follows the architecture boundary: API entrypoints stay thin, application services own use cases, domain code stays infrastructure-free, infrastructure owns PostgreSQL and provider integrations, and workers process outbox jobs.

## Target Layout

```text
repo-root/
  MemorySystem.sln
  docker-compose.yml
  src/
    MemorySystem.Api/
    MemorySystem.Application/
    MemorySystem.Domain/
    MemorySystem.Infrastructure/
    MemorySystem.Worker/
  tests/
    MemorySystem.UnitTests/
    MemorySystem.IntegrationTests/
  migrations/
    001_initial_memory_schema.sql
  tools/
    ui/
    vault-sync/
  docs/
    decisions/
    scenarios/
  vault/
    AI Memory System/
```

`repo-root/` means the current repository root, not a nested child folder. M1 should create the .NET solution, source projects, test projects, local database runtime, and first migration path at this level.

## Root

| Path | Purpose |
| --- | --- |
| `MemorySystem.sln` | .NET solution containing API, Application, Domain, Infrastructure, Worker, and test projects. |
| `docker-compose.yml` | Local PostgreSQL plus pgvector runtime for development and integration tests, using the pinned image from [Decision 0003](decisions/0003-local-database-runtime.md). |
| `docs/` | Human-readable project documentation, roadmap, backlog, architecture notes, and decisions. |
| `migrations/` | SQL-first database migrations. These are the source of truth for schema changes. |
| `src/` | Production .NET source code. |
| `tests/` | Unit and integration tests. |
| `tools/` | TypeScript UI and local tooling. |
| `vault/` | Obsidian-compatible human workspace and exports. Not transactional storage. |

## Project References

Use project references to keep dependencies pointing inward toward domain rules and outward only through interfaces.

```text
MemorySystem.Api
  -> MemorySystem.Application

MemorySystem.Application
  -> MemorySystem.Domain

MemorySystem.Infrastructure
  -> MemorySystem.Application
  -> MemorySystem.Domain

MemorySystem.Worker
  -> MemorySystem.Application
  -> MemorySystem.Infrastructure

MemorySystem.UnitTests
  -> MemorySystem.Domain
  -> MemorySystem.Application

MemorySystem.IntegrationTests
  -> MemorySystem.Api
  -> MemorySystem.Infrastructure
  -> MemorySystem.Application
  -> MemorySystem.Domain
```

Rules:

- Domain has no project references to API, Application, Infrastructure, or Worker.
- Application depends on Domain contracts and exposes use-case interfaces.
- Infrastructure implements persistence and provider interfaces needed by Application.
- API and Worker compose the application with infrastructure at runtime.
- Tests may reference multiple projects to verify boundaries and end-to-end behavior.

## Source Projects

### `src/MemorySystem.Api`

Owns:

- ASP.NET Core host and route registration
- authentication and coarse authorization boundaries
- request and response DTOs
- OpenAPI configuration
- ProblemDetails responses
- health endpoints

Rules:

- Keep route handlers thin.
- Call application services for business workflows.
- Do not expose persistence entities as API contracts.

### `src/MemorySystem.Application`

Owns:

- Memory Broker orchestration
- Context Builder orchestration
- scope and namespace resolution
- fine-grained access decisions
- memory proposal, review, supersession, expiry, deletion, and redaction workflows
- ranking and filtering policies

Rules:

- Express use cases in service interfaces and application models.
- Keep durable write policy here, not in API route handlers or SQL helpers.
- Do not depend on ASP.NET Core types where simple application contracts are enough.

### `src/MemorySystem.Domain`

Owns:

- entities and value objects
- memory status lifecycle concepts
- scope, namespace, role, visibility, trust level, and confidence concepts
- domain rules that do not require IO

Rules:

- No database, HTTP, filesystem, or provider dependencies.
- Keep domain rules deterministic and easy to unit test.

### `src/MemorySystem.Infrastructure`

Owns:

- raw Npgsql data access for core memory queries
- SQL migration runner integration if used by the app or tests
- PostgreSQL full-text search queries
- pgvector queries
- idempotency and outbox persistence
- embedding provider adapters
- Obsidian vault import/export adapters

Rules:

- Keep SQL visible for core event, memory, idempotency, access, and retrieval queries.
- Dapper may be used only as a small mapping convenience.
- Do not decide memory policy here; enforce decisions made by application services.

### `src/MemorySystem.Worker`

Owns:

- outbox job polling and leasing
- embedding generation
- chunk indexing
- summary jobs
- review notifications
- expiry and redaction jobs
- vault export jobs

Rules:

- Workers process jobs idempotently.
- Workers must not create durable memory outside broker-approved workflows.

## Tests

### `tests/MemorySystem.UnitTests`

Use for:

- domain value objects
- status lifecycle rules
- scope and namespace parsing
- broker decision logic without database dependencies
- ranking formula behavior

### `tests/MemorySystem.IntegrationTests`

Use for:

- migration application
- PostgreSQL constraints and indexes
- Npgsql repository behavior
- transaction behavior
- API endpoints
- authorization filters
- idempotent retries
- outbox job persistence

Integration tests should run against real PostgreSQL with pgvector enabled.

## Migrations

`migrations/` contains ordered SQL files.

Rules:

- Do not rely on ORM-generated migrations for core schema.
- Keep constraints, indexes, triggers, and functions visible in SQL.
- Add schema changes through new migration files instead of editing applied migrations after they are shared.
- Keep model-specific vector indexes out of the first migration until embedding model, dimension, distance operator, and index type are selected.

## Tools

### `tools/ui`

TypeScript review and administration dashboard.

### `tools/vault-sync`

TypeScript tooling for Obsidian export and later import workflows.

Rules:

- Tools can support review and export.
- Tools must not become the source of truth for memory state.

## Documentation

| Path | Purpose |
| --- | --- |
| `docs/README.md` | Documentation index and glossary. |
| `docs/project-goal.md` | Project north star. |
| `docs/architecture.md` | Short architecture overview. |
| `docs/long-term-memory-system-plan.md` | Detailed system plan. |
| `docs/roadmap.md` | Milestone roadmap. |
| `docs/backlog.md` | Milestone backlog and acceptance criteria. |
| `docs/folder-structure.md` | Repository layout and ownership guide. |
| `docs/decisions/` | Accepted architecture and implementation decisions. |
| `docs/scenarios/` | End-to-end implementation scenarios with sample data and milestone expectations. |

## Vault

`vault/AI Memory System/` is the Obsidian-compatible human workspace.

Use it for:

- architecture decisions
- project notes
- role summaries
- approved skills
- memory review exports
- human-readable summaries

Do not use it for:

- high-volume event history
- transactional state
- concurrent writes
- permission enforcement
- authoritative user preference storage
