# Long-Term Memory System

A durable, auditable memory service for AI agents.

The system stores long-lived memory in PostgreSQL, uses pgvector for semantic
recall, preserves source events as evidence, and keeps memory writes and reads
behind explicit governance boundaries.

Private Alpha 0.1 is complete. See the
[release notes](docs/private-alpha-0.1-release.md) and the
[product improvement plan](docs/product-improvement-plan.md) for the current
status and next milestones.

## Contents

- [What it does](#what-it-does)
- [Architecture](#architecture)
- [Prerequisites](#prerequisites)
- [Quick start](#quick-start)
- [Run locally](#run-locally)
- [Testing](#testing)
- [TypeScript tooling](#typescript-tooling)
- [Useful endpoints](#useful-endpoints)
- [Repository layout](#repository-layout)
- [Documentation](#documentation)

## What it does

This project is not a prompt dump or a notes app. Its goal is trustworthy AI
continuity: durable memories should have scope, provenance, confidence,
lifecycle state, and an access boundary.

Core capabilities:

- Append source events as evidence.
- Propose durable memory through a Memory Broker.
- Route low-confidence, duplicate, similar, or conflicting memory to review.
- Retrieve scoped context packets through authorized full-text, semantic, and
  hybrid search.
- Record context-packet feedback without storing raw query text.
- Export approved memory to a human-readable vault.
- Expose operational health for API readiness, worker heartbeat, outbox backlog,
  pending reviews, and stale vault exports.
- Run repeatable private-alpha seed and backup/restore smoke checks.

## Architecture

The service is built around PostgreSQL as the source of truth.

| Area | Implementation |
| --- | --- |
| API | ASP.NET Core Web API |
| Domain/application | C#/.NET 10 projects |
| Storage | PostgreSQL 17 with pgvector |
| Migrations | SQL-first migration runner |
| Retrieval | Structured search, full-text search, pgvector semantic search, hybrid ranking |
| Background work | Outbox worker for indexing, export, review, expiry, redaction, and retention tasks |
| UI/tooling | TypeScript review dashboard and local tooling |
| Tests | Unit tests plus PostgreSQL-backed integration tests |

Read the detailed architecture guide in
[docs/architecture.md](docs/architecture.md).

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/)
- Docker with Docker Compose
- Node.js for TypeScript UI and tool builds

Docker Compose runs PostgreSQL with pgvector on local port `55432` by default.

## Quick start

Start PostgreSQL:

```bash
docker compose up -d --wait postgres
```

Create the private-alpha Scenario 0001 demo data:

```bash
./scripts/seed-private-alpha-demo.sh
```

Build and run the fast verification suite:

```bash
dotnet restore MemorySystem.sln
dotnet build MemorySystem.sln --configuration Release --no-restore
dotnet test MemorySystem.sln --configuration Release --no-build --filter "Category!=Database"
```

Run the API with the seeded local principal:

```bash
Authentication__ApiKey__Keys__local-jack__Key=private-alpha-local-key \
Authentication__ApiKey__Keys__local-jack__PrincipalId=11111111-1111-4111-8111-111111111111 \
Authentication__ApiKey__Keys__local-jack__DisplayName="Jack Tam" \
dotnet run --project src/MemorySystem.Api
```

Run the worker in another terminal:

```bash
dotnet run --project src/MemorySystem.Worker
```

Most API endpoints require the configured `X-Api-Key` header.

## Run locally

The seed runner is the fastest way to create a useful local database:

```bash
./scripts/seed-private-alpha-demo.sh
```

It starts PostgreSQL if needed, applies migrations, and upserts the Scenario
0001 actors, memberships, grants, source events, memory facts, role lenses,
chunks, outbox jobs, and deterministic embeddings.

Use the private-alpha workflow to walk through the product path:

1. Append source evidence.
2. Propose durable memory.
3. Review or correct governed memory.
4. Retrieve a scoped context packet.
5. Record retrieval feedback.
6. Export human-readable memory.
7. Check operational state.

Workflow details are in
[docs/private-alpha-workflow.md](docs/private-alpha-workflow.md). The seed story
is in
[docs/scenarios/0001-user-preference-project-decision-cto-context.md](docs/scenarios/0001-user-preference-project-decision-cto-context.md).

## Testing

Fast local verification:

```bash
dotnet restore MemorySystem.sln
dotnet build MemorySystem.sln --configuration Release --no-restore
dotnet test MemorySystem.sln --configuration Release --no-build --filter "Category!=Database"
```

Database-backed integration verification:

```bash
docker compose up -d --wait postgres
MEMORYSYSTEM_REQUIRE_DATABASE_TESTS=true dotnet test tests/MemorySystem.IntegrationTests/MemorySystem.IntegrationTests.csproj --configuration Release --no-build --filter "Category=Database"
docker compose stop postgres
```

Backup/restore smoke verification:

```bash
./scripts/backup-restore-smoke.sh
```

More testing notes, including alternate PostgreSQL ports and CI behavior, are in
[docs/testing.md](docs/testing.md).

## TypeScript tooling

The dashboard and vault-sync tools are dependency-free Node.js projects. Use
these commands when changing TypeScript sources under `tools/`:

```bash
cd tools/ui
npm run build
npm run check
```

```bash
cd tools/vault-sync
npm run build
npm run check
```

The UI build writes the static review dashboard asset under
`src/MemorySystem.Api/wwwroot/reviews/`. The vault-sync build writes
`tools/vault-sync/dist/vault-sync.js`.

## Useful endpoints

| Endpoint | Purpose |
| --- | --- |
| `GET /health/live` | Liveness check |
| `GET /health/ready` | Readiness check, including database and provider health |
| `GET /api/operations/summary` | API, worker, outbox, review, and vault-export summary |
| `POST /api/events` | Append source evidence |
| `POST /api/memory/proposals` | Propose durable memory |
| `GET /api/memory/context` | Build a scoped context packet |
| `POST /api/memory/context/feedback` | Record hashed retrieval feedback |
| `GET /reviews/` | Local review dashboard |

## Repository layout

```text
src/
  MemorySystem.Api/             ASP.NET Core API and review dashboard assets
  MemorySystem.Application/     Application contracts and use-case services
  MemorySystem.Domain/          Domain models and policies
  MemorySystem.Infrastructure/  PostgreSQL, providers, repositories, and stores
  MemorySystem.Migrator/        SQL migration runner
  MemorySystem.Worker/          Outbox and retention worker
  MemorySystem.DemoSeeder/      Private-alpha seed/demo runner
tests/
  MemorySystem.UnitTests/
  MemorySystem.IntegrationTests/
benchmarks/                     Benchmark fixtures, prompt packs, rubrics, and reports
migrations/                     SQL schema migrations
scripts/                        Local seed and release hygiene scripts
docs/                           Architecture, decisions, runbooks, and roadmap
vault/                          Human-readable memory export workspace
```

## Documentation

Start with these documents:

- [Documentation index](docs/README.md)
- [Project goal](docs/project-goal.md)
- [Architecture overview](docs/architecture.md)
- [Private alpha workflow](docs/private-alpha-workflow.md)
- [Product improvement plan](docs/product-improvement-plan.md)
- [Agent-facing memory contract](docs/agent-facing-memory-contract.md)
- [Agent Memory OpenAPI v1](docs/api/agent-memory-v1.openapi.json)
- [Agent Memory v1 client examples](docs/api/agent-memory-v1-examples.md)
- [API `memory.queryFacts` implementation plan](docs/api/memory-query-facts-implementation-plan.md)
- [Roadmap](docs/roadmap.md)
- [Testing commands](docs/testing.md)
- [Benchmarking plan](docs/benchmarking.md)
- [Backup and restore runbook](docs/backup-restore.md)
- [Production secret handling](docs/production-secrets.md)
- [Private Alpha 0.1 release notes](docs/private-alpha-0.1-release.md)
