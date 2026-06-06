# Long-Term Memory System

A durable, auditable memory service for AI agents.

This project stores long-lived memory in PostgreSQL, uses pgvector for semantic
recall, preserves source events as evidence, and keeps memory reads and writes
behind explicit governance boundaries.

Status: `v1.0.0` is marked GO for an external pilot. That means the pilot
workflow, local verification, governance evidence, and operator checks are
documented and repeatable. Start with the [GO record](docs/external-pilot-go-epr04-v1.0.0-2026-06-04.md),
[release readiness status](docs/external-pilot-readiness-status.json), and
[product improvement plan](docs/product-improvement-plan.md) when you need the
current release context.

## What It Does

The system is not a prompt dump or a notes app. Its goal is trustworthy AI
continuity: durable memories should have scope, provenance, confidence,
lifecycle state, and an access boundary.

Core capabilities:

- Append source events as evidence.
- Propose durable memory through a Memory Broker.
- Route low-confidence, duplicate, similar, or conflicting memory to review.
- Retrieve scoped context packets through authorized structured, full-text,
  semantic, and hybrid search.
- Record context-packet feedback without storing raw query text.
- Inspect authorized memory facts, source events, and audit references in the
  admin console.
- Run authenticated governance workflows for legal holds, erasure, and
  retention reporting.
- Export approved memory to a human-readable vault.
- Expose health, readiness, worker heartbeat, outbox, review, metrics, backup,
  restore, and observability checks for pilot operation.

## How It Works

PostgreSQL is the source of truth for runtime memory records. pgvector supports
recall, but vector rows are rebuildable derived data rather than authoritative
memory. Repository plans, policy, API contracts, release decisions, and backlog
status stay authoritative in Markdown under `docs/`; memory stores compact,
source-linked summaries. See
[docs/memory-vs-markdown-policy.md](docs/memory-vs-markdown-policy.md).

| Area | Implementation |
| --- | --- |
| API | ASP.NET Core Web API |
| Domain/application | C#/.NET 10 projects |
| Storage | PostgreSQL 17 with pgvector |
| Migrations | SQL-first migration runner |
| Retrieval | Structured search, full-text search, semantic search, hybrid ranking |
| Background work | Outbox worker for indexing, export, review, expiry, redaction, and retention tasks |
| UI/tooling | TypeScript review dashboard, admin console, and vault tooling |
| Tests | Unit tests plus PostgreSQL-backed integration tests |

Read [docs/architecture.md](docs/architecture.md) for the detailed component
map, write path, read path, trust model, and current architecture boundary.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/)
- Docker with Docker Compose
- Node.js, when changing TypeScript UI or tooling

Docker Compose runs PostgreSQL with pgvector on local port `55432` by default.

## Quick Start

From the repository root:

```bash
docker compose up -d --wait postgres
./scripts/seed-private-alpha-demo.sh

dotnet restore MemorySystem.sln
dotnet build MemorySystem.sln --configuration Release --no-restore
dotnet test MemorySystem.sln --configuration Release --no-build --filter "Category!=Database"
```

Run the API with the seeded local demo principal:

```bash
Authentication__ApiKey__Keys__local_demo__Key=private-alpha-local-key \
Authentication__ApiKey__Keys__local_demo__PrincipalId=11111111-1111-4111-8111-111111111111 \
Authentication__ApiKey__Keys__local_demo__DisplayName="Local Demo User" \
ASPNETCORE_URLS=http://127.0.0.1:5099 \
dotnet run --project src/MemorySystem.Api
```

In a second terminal, run the worker:

```bash
dotnet run --project src/MemorySystem.Worker
```

Open or call:

```bash
curl http://127.0.0.1:5099/health/live
curl -H "X-Api-Key: private-alpha-local-key" http://127.0.0.1:5099/api/operations/summary
```

Then visit:

- `http://127.0.0.1:5099/reviews/` for the review dashboard
- `http://127.0.0.1:5099/admin/` for the admin console

The local API key `private-alpha-local-key` and Docker password
`memory_system_dev_password` are committed demo values. They are not secrets and
must not be reused outside local development or tests. Production secret rules
are documented in [docs/production-secrets.md](docs/production-secrets.md).

## Local Workflow

The seed runner is the fastest way to create a useful local database:

```bash
./scripts/seed-private-alpha-demo.sh
```

It starts PostgreSQL if needed, applies migrations, and upserts Scenario 0001:
actors, memberships, grants, source events, memory facts, role lenses, chunks,
outbox jobs, and deterministic embeddings.

Use [docs/private-alpha-workflow.md](docs/private-alpha-workflow.md) to walk the
product path:

1. Append source evidence.
2. Propose durable memory.
3. Review or correct governed memory.
4. Retrieve a scoped context packet.
5. Record retrieval feedback.
6. Export human-readable memory.
7. Check operational state.

The seed story is defined in
[docs/scenarios/0001-user-preference-project-decision-cto-context.md](docs/scenarios/0001-user-preference-project-decision-cto-context.md).

## Testing

Fast verification:

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

Useful smoke checks:

```bash
./scripts/backup-restore-smoke.sh
./scripts/observability-artifacts-smoke.sh
MEMORYSYSTEM_API_BASE_URL=http://127.0.0.1:5099 ./scripts/operations-metrics-smoke.sh
```

More test paths, alternate PostgreSQL ports, CI behavior, benchmark gates, and
production-pilot smoke commands are in [docs/testing.md](docs/testing.md).

## TypeScript Tooling

Run these checks after changing dashboard, admin console, or vault-sync sources:

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

The UI build writes static assets under
`src/MemorySystem.Api/wwwroot/reviews/` and
`src/MemorySystem.Api/wwwroot/admin/`. The vault-sync build writes
`tools/vault-sync/dist/vault-sync.js`.

## Useful Endpoints

| Endpoint | Purpose |
| --- | --- |
| `GET /health/live` | Liveness check |
| `GET /health/ready` | Readiness check, including database and provider health |
| `GET /api/operations/summary` | API, worker, outbox, review, and vault-export summary |
| `GET /api/operations/metrics` | Authenticated Prometheus-compatible pilot metrics |
| `GET /api/admin/memory/facts` | Authenticated memory fact inspection for the admin console |
| `GET /api/admin/source-events` | Authenticated source event and audit reference inspection for the admin console |
| `POST /api/events` | Append source evidence |
| `POST /api/memory/proposals` | Propose durable memory |
| `GET /api/memory/context` | Build a scoped context packet |
| `POST /api/memory/context/feedback` | Record hashed retrieval feedback |
| `POST /api/memory/query-facts` | Query authorized facts with source, lifecycle, contradiction, and policy metadata |
| `GET /reviews/` | Local review dashboard |
| `GET /admin/` | Local memory admin console |

The curated agent-facing API contract lives in
[docs/api/agent-memory-v1.openapi.json](docs/api/agent-memory-v1.openapi.json).
Runnable curl examples are in
[docs/api/agent-memory-v1-examples.md](docs/api/agent-memory-v1-examples.md).

## Repository Layout

```text
src/
  MemorySystem.Api/             ASP.NET Core API and static UI assets
  MemorySystem.Application/     Application contracts and use-case services
  MemorySystem.Domain/          Domain boundary and extracted IO-free concepts
  MemorySystem.Infrastructure/  PostgreSQL, providers, repositories, and stores
  MemorySystem.Migrator/        SQL migration runner
  MemorySystem.Worker/          Outbox and retention worker
  MemorySystem.DemoSeeder/      Scenario 0001 seed/demo runner
tests/
  MemorySystem.UnitTests/
  MemorySystem.IntegrationTests/
benchmarks/                     Benchmark fixtures, runners, rubrics, and reports
migrations/                     SQL schema migrations
observability/                  Alerts, dashboards, trace coverage, and metric inputs
scripts/                        Local seed, smoke, release, and operator scripts
docs/                           Architecture, API, runbooks, decisions, and roadmap
vault/                          Human-readable memory export workspace
infra/                          Terraform platform contracts and modules
```

## Documentation

New readers should start here:

- [Documentation hub](docs/README.md)
- [Project goal](docs/project-goal.md)
- [Architecture overview](docs/architecture.md)
- [Local demo workflow](docs/private-alpha-workflow.md)
- [API contracts](docs/api/README.md)
- [Memory vs Markdown policy](docs/memory-vs-markdown-policy.md)
- [Testing commands](docs/testing.md)
- [Production secret handling](docs/production-secrets.md)

Planning, release, governance, platform, and architecture-decision records are
kept under `docs/` for traceability. Historical pilot evidence is intentionally
preserved; use the linked release readiness status and GO record for the current
state.
