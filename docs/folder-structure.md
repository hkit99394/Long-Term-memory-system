# Folder Structure

## Purpose

This document maps the current repository layout for the long-term memory
system. Use it to decide where new code, tests, migrations, tools, benchmark
fixtures, infrastructure contracts, and docs should live.

The structure follows the architecture boundary: API entrypoints stay thin,
application services own use cases, domain code stays infrastructure-free,
infrastructure owns PostgreSQL and provider integrations, and workers process
outbox jobs.

## Layout

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
    agent-contract-usefulness-v1/
    context-product-v1/
    llm-outcome-v0/
    release-gate/
  infra/
    terraform/
  migrations/
    *.sql
  observability/
  tools/
    ui/
    vault-sync/
  docs/
    api/
    decisions/
    scenarios/
  vault/
    AI Memory System/
```

`repo-root/` means the current repository root, not a nested child folder.

## Root

| Path | Purpose |
| --- | --- |
| `MemorySystem.sln` | .NET solution containing API, Application, Domain, Infrastructure, Migrator, Worker, and test projects. |
| `docker-compose.yml` | Local PostgreSQL plus pgvector runtime for development and integration tests, using the pinned image from [Decision 0003](decisions/0003-local-database-runtime.md). |
| `benchmarks/` | Benchmark fixtures, prompt-pack generators, rubrics, and generated report workspace. |
| `docs/` | Human-readable project documentation, roadmap, backlog, architecture notes, decisions, and source-of-truth policy. |
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
coverage, metric input manifests, and the page/ticket/info alert-routing
contract. Use `scripts/observability-artifacts-smoke.sh` after changing these
files.

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
under `scripts/`; GC-03 adds the payload-safe erasure replay ledger export and
restore-time replay validation path under the same script boundary. GC-04 adds
the standard/audit retention minimization operator job under `scripts/`, and
GC-05 adds the external payload-store retention check under the same boundary.
GC-06 adds the compliance evidence package command under `scripts/`. GC-07 adds
the governance/compliance admin console contract under `docs/` and the
payload-safe `/admin/` status surface under the existing API static assets.
GC-08 adds the governance/compliance release smoke command under `scripts/` and
its release-smoke contract under `docs/`.
PI-05 wires
runtime telemetry exporters, and PI-06 makes
alert routing, owners, silencing, and environment route tests explicit in the
observability contract. PI-07 adds
`docs/production-release-checklists-pi07.md` as the release evidence contract
for local, CI, pilot, and production. That folder should own deployment
resources and environment overlays, while SQL schema history remains in
`migrations/` and application behavior remains in `src/`. PI-08 adds
`docs/production-platform-rehearsal-pi08.md` as the written evidence from the
first isolated platform rehearsal.

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

## Source-Of-Truth Policy

[Memory vs Markdown Policy](memory-vs-markdown-policy.md) defines what belongs
in Markdown, memory, backlog, source evidence, role lenses, release evidence,
and vault exports. Use it before turning operator instructions or retrieved
memory into durable project plans.

Repository plans, policies, architecture records, API contracts, runbooks,
release decisions, and backlog status belong in committed Markdown. Project
memory stores compact, source-linked summaries that help agents retrieve the
right context quickly.

## Benchmarks

`benchmarks/` contains benchmark fixtures, runners, rubrics, and generated
report templates. Generated local runs live under `benchmarks/outputs/`;
curated fixtures and runners remain committed.

Current suite:

- `benchmarks/llm-outcome-v0`: manual benchmark for whether governed memory
  improves LLM task output over a memory-off baseline.
- `benchmarks/agent-contract-usefulness-v1`: LMSS v1 benchmark tasks for
  contract-aware fact finding, evidence use, contradiction handling, role
  targeting, feedback hygiene, and scoped safety.
- `benchmarks/context-product-v1`: context packet, safe exclusion, and feedback
  benchmark smoke.
- `benchmarks/release-gate`: MR-12 release-gate fixtures and runner.

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
| `docs/memory-vs-markdown-policy.md` | IP-01 source-of-truth policy for Markdown, memory, backlog, source evidence, role lenses, release evidence, and vault exports. |
| `docs/project-defined-roles-ip05.md` | IP-05 project-defined role vocabulary, default templates, admin API, and validation contract. |
| `docs/canonical-memory-types-ip06.md` | IP-06 canonical durable memory type vocabulary and compatibility aliases. |
| `docs/source-backed-memory-hygiene-ip07.md` | IP-07 source-backed memory hygiene automation contract and seed validation rules. |
| `docs/agent-memory-client-wrapper-ip08.md` | IP-08 local agent memory wrapper contract for prework, fact queries, and feedback closeout. |
| `docs/weekly-admin-review-workflow-ip09.md` | IP-09 weekly admin review workflow and payload-safe queue contract. |
| `docs/production-backup-restore-drill-schedule-ip10.md` | IP-10 recurring backup/restore drill cadence, evidence, RPO/RTO, protected-volume, and export checks. |
| `docs/access-boundary-review-ip11.md` | IP-11 access-boundary review contract for memberships, roles, grants, service accounts, OIDC bindings, break-glass posture, and permission drift. |
| `docs/backlog-roadmap-memory-sync-ip12.md` | IP-12 roadmap/backlog Markdown-to-memory sync guard and source-backed seed workflow. |
| `docs/role-lens-first-content-pass-ip13.md` | IP-13 source-backed first content pass for project role-lens memory. |
| `docs/admin-ux-polish-ip14.md` | IP-14 admin console polish contract for filters, source/review routing, credential state, and operations status. |
| `docs/memory-quality-metrics-ip15.md` | IP-15 memory-quality metrics contract for operations summary, Prometheus export, and admin Operations view. |
| `docs/project-onboarding-runbook-ip16.md` | IP-16 payload-safe new-project onboarding flow for scope, roles, grants, seed docs, source evidence, and review cadence. |
| `docs/release-evidence-bundle-automation-ip17.md` | IP-17 release evidence bundle automation for tests, migration, health, operations, benchmark, backup/restore, and rollback. |
| `docs/production-platform-integration-lr05.md` | LR-05 platform integration boundary and follow-on production implementation plan. |
| `docs/governance-compliance-gate-lr06.md` | LR-06 governance/compliance boundary and follow-on implementation plan. |
| `docs/environment-governance-policy-gc01.md` | GC-01 environment governance policy contract for local, CI, pilot, and production. |
| `docs/permission-drift-report-gc02.md` | GC-02 payload-safe permission-drift report contract. |
| `docs/backup-erasure-replay-validation-gc03.md` | GC-03 payload-safe erasure replay ledger and restore-validation contract. |
| `docs/standard-audit-retention-minimization-gc04.md` | GC-04 standard/audit retention minimization operator-job contract. |
| `docs/external-payload-retention-check-gc05.md` | GC-05 external payload-store retention check contract. |
| `docs/compliance-evidence-package-gc06.md` | GC-06 compliance evidence package manifest contract. |
| `docs/governance-compliance-admin-console-gc07.md` | GC-07 governance/compliance admin console status contract. |
| `docs/governance-compliance-release-smoke-gc08.md` | GC-08 governance/compliance release smoke contract. |
| `docs/pilot-readiness-evidence-review-2026-06-01.md` | Pilot readiness evidence review and external invite go/no-go decision. |
| `docs/target-environment-pilot-rehearsal-p0.md` | P0 target-environment pilot rehearsal runbook, evidence checklist, and go/no-go template. |
| `docs/target-environment-evidence-hardening-ip04.md` | IP-04 target-environment evidence manifest, checksum verifier, and completion rule for real target artifacts. |
| `docs/target-environment-evidence-manifest.schema.json` | JSON Schema for the payload-safe target evidence manifest. |
| `docs/target-environment-evidence-manifest.example.json` | Copyable example manifest for a real target evidence bundle. |
| `docs/pilot-release-evidence-epr03-2026-06-04.md` | Payload-safe EPR-02/EPR-03 local pilot-equivalent release evidence record. |
| `docs/external-pilot-go-no-go-epr04-2026-06-04.md` | Historical EPR-04 external-pilot NO-GO decision record and GO replacement requirements. |
| `docs/external-pilot-go-epr04-v1.0.0-2026-06-04.md` | Owner-approved EPR-04 GO replacement for version 1.0.0 external pilot. |
| `docs/documentation-truth-cleanup-p1-2026-06-04.md` | P1 documentation reconciliation record for external-pilot readiness state. |
| `docs/release-readiness-status-contract-p2.md` | P2 machine-readable release-readiness status contract notes. |
| `docs/external-pilot-readiness-status.json` | Canonical current external-pilot readiness status for EPR gates and blockers. |
| `docs/external-pilot-readiness-status.schema.json` | JSON Schema for the external-pilot readiness status contract. |
| `docs/pilot-operator-cockpit-p3.md` | P3 admin cockpit record for external-pilot readiness. |
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

- authoritative project policy or backlog status
- high-volume event history
- transactional state
- concurrent writes
- permission enforcement
- authoritative user preference storage
