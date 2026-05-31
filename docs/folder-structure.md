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
    MemorySystem.Migrator/
    MemorySystem.Worker/
  tests/
    MemorySystem.UnitTests/
    MemorySystem.IntegrationTests/
  benchmarks/
    llm-outcome-v0/
  infra/
    terraform/
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
| `MemorySystem.sln` | .NET solution containing API, Application, Domain, Infrastructure, Migrator, Worker, and test projects. |
| `docker-compose.yml` | Local PostgreSQL plus pgvector runtime for development and integration tests, using the pinned image from [Decision 0003](decisions/0003-local-database-runtime.md). |
| `benchmarks/` | Benchmark fixtures, prompt-pack generators, rubrics, and generated report workspace. |
| `docs/` | Human-readable project documentation, roadmap, backlog, architecture notes, and decisions. |
| `infra/` | Production platform infrastructure. PI-02 starts Terraform module and environment contracts under `infra/terraform`; PI-03 adds managed RDS PostgreSQL resources; PI-04 adds backup/export and restore-validation job contracts. |
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
  -> MemorySystem.Infrastructure

MemorySystem.Application
  -> MemorySystem.Domain

MemorySystem.Infrastructure
  -> MemorySystem.Application
  -> MemorySystem.Domain

MemorySystem.Migrator
  -> MemorySystem.Infrastructure

MemorySystem.Worker
  -> MemorySystem.Application
  -> MemorySystem.Infrastructure

MemorySystem.UnitTests
  -> MemorySystem.Domain
  -> MemorySystem.Application
  -> MemorySystem.Infrastructure
  -> MemorySystem.Migrator
  -> MemorySystem.Worker

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

- target home for stable entities and value objects
- target home for memory status lifecycle concepts
- target home for scope, namespace, role, visibility, trust level, and confidence concepts
- domain rules that do not require IO, once extracted from Application

Rules:

- No database, HTTP, filesystem, or provider dependencies.
- Keep domain rules deterministic and easy to unit test.

Current status: the project is intentionally thin today. Most durable concepts
still live in Application and Infrastructure while the production-pilot surface
stabilizes. [Domain Model Extraction LR-04](domain-model-extraction-lr04.md)
tracks the staged extraction into this project with compatibility tests and no
schema or endpoint churn.

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

### `src/MemorySystem.Migrator`

Owns:

- local command-line migration execution
- connection-string and migration-directory option parsing
- process exit codes for migration success or failure

Rules:

- Delegate schema policy to `MemorySystem.Infrastructure`.
- Do not duplicate migration ordering, checksum, advisory-lock, or tracking-table behavior.

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
- fast infrastructure, worker, and migrator behavior that can run without external services
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

## Observability

`observability/` contains versioned production-pilot observability artifacts:
Prometheus-compatible alert rules, a Grafana-compatible dashboard, trace
coverage, and metric input manifests. Use `scripts/observability-artifacts-smoke.sh`
after changing these files.

## Production Platform

[Production Platform Integration LR-05](production-platform-integration-lr05.md)
defines the future boundary for infrastructure-as-code, managed PostgreSQL,
backup exporter evidence, runtime OpenTelemetry exporters, alert routing, and
environment-specific release checklists.

[Production Platform Baseline PI-01](production-platform-baseline-pi01.md)
selects AWS ECS Fargate, Amazon RDS PostgreSQL with pgvector, Amazon ECR, and
Terraform for the first pilot baseline. PI-02 adds `infra/terraform` with
pilot and production environment overlays plus runtime, PostgreSQL, and
observability module contracts. PI-03 turns the PostgreSQL module into managed
RDS resources with private network access, backup/PITR settings, and pgvector
validation metadata. PI-04 adds backup/export and restore-validation commands
to the runtime contract while keeping the central validation table manifest
under `scripts/`. That folder should own deployment resources and environment
overlays, while SQL schema history remains in `migrations/` and application
behavior remains in `src/`.

## Benchmarks

`benchmarks/` contains benchmark suites, scorecard templates, summarizers, and
the MR-12 release gate. Generated local runs live under
`benchmarks/outputs/`; curated fixtures and runners remain committed.

## Tools

### `tools/ui`

TypeScript review and administration dashboard. The source lives under
`tools/ui/src/`; the build writes generated JavaScript into the API static asset
tree at `src/MemorySystem.Api/wwwroot/reviews/`.

```bash
cd tools/ui
npm run build
npm run check
```

### `tools/vault-sync`

TypeScript tooling for Obsidian export and later import workflows. The source
lives under `tools/vault-sync/src/`; the build writes generated JavaScript into
`tools/vault-sync/dist/`.

```bash
cd tools/vault-sync
npm run build
npm run check
```

Rules:

- Tools can support review and export.
- Tools must not become the source of truth for memory state.

## Benchmarks

`benchmarks/` contains benchmark fixtures, runners, rubrics, and generated
report templates.

Current suite:

- `benchmarks/llm-outcome-v0`: manual benchmark for whether governed memory
  improves LLM task output over a memory-off baseline.
- `benchmarks/agent-contract-usefulness-v1`: LMSS v1 benchmark tasks for
  contract-aware fact finding, evidence use, contradiction handling, role
  targeting, feedback hygiene, and scoped safety.

Rules:

- Commit benchmark fixtures, rubrics, and runners.
- Keep generated prompt packs, answer captures, local run metadata, and reports
  under `benchmarks/outputs/` unless an intentionally curated summary should be
  committed.
- Use [Benchmarking Plan](benchmarking.md) as the product-level benchmark guide.

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
| `docs/production-platform-integration-lr05.md` | LR-05 platform integration boundary and follow-on production implementation plan. |
| `docs/production-platform-baseline-pi01.md` | PI-01 selected platform, IaC, artifact, state/secrets, environment, and ownership baseline. |
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
