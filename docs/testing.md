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

When you intend to run full local verification, set `MEMORYSYSTEM_REQUIRE_DATABASE_TESTS=true`. That makes database tests fail loudly if PostgreSQL is missing instead of reporting a green run with the database slice skipped.

## Database-Backed Integration Verification

Use this path after the Release build when schema, migration, PostgreSQL health, Npgsql, or pgvector behavior changes:

```bash
docker compose up -d --wait postgres
MEMORYSYSTEM_REQUIRE_DATABASE_TESTS=true dotnet test tests/MemorySystem.IntegrationTests/MemorySystem.IntegrationTests.csproj --configuration Release --no-build --filter "Category=Database"
docker compose stop postgres
```

Docker Compose maps PostgreSQL to local port `55432` by default to avoid colliding with a separately installed PostgreSQL on `5432`. If local port `55432` is already occupied, run the Docker service on a different host port and use the same port in the test connection string:

```bash
MEMORYSYSTEM_POSTGRES_PORT=55433 docker compose up -d --wait postgres
MEMORYSYSTEM_TEST_POSTGRES_CONNECTION_STRING="Host=127.0.0.1;Port=55433;Database=memory_system;Username=memory_system;Password=memory_system_dev_password" \
MEMORYSYSTEM_REQUIRE_DATABASE_TESTS=true \
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

MEMORYSYSTEM_REQUIRE_DATABASE_TESTS=true dotnet test tests/MemorySystem.IntegrationTests/MemorySystem.IntegrationTests.csproj --configuration Release --no-build --filter "Category=Database"
```

The fast test command covers deterministic application policy, including namespace parsing, scope normalization, broker decisions, broker candidate classification, confidence scoring, session-only task instruction handling, active-memory dedupe and contradiction review routing, retrieval evaluation metric scoring, embedding provider configuration and deterministic adapter behavior, production secret guardrails, access authorization, memory status lifecycle policy, and direct memory read authorization.

The database-backed command currently covers the migration runner, migration checksum mismatch and history-gap rejection, PostgreSQL, outbox, worker heartbeat, and embedding provider health endpoint signals, structured API decision logging without payload leakage, scoped event constraints, project/organization scope consistency, memory fact scope/owner/namespace consistency, memory fact status filtering, structured memory fact search, authorized full-text memory search, authorized pgvector semantic search, hybrid memory ranking, context packet construction, retrieval evaluation against a real context packet, pending review listing with review-permission filtering, review action workflows, Obsidian export of approved decisions and summaries with source IDs, stale vault marker generation for deleted and redacted exports, readable archive export for superseded and expired memory, role memory lens repository round-trips and indexing outbox writes, role-lens base fact scope validation, API-key principal resolution, request idempotency, event append including global/session grant checks, minimal broker proposal decisions, candidate kind and confidence propagation, session-only proposal handling, exact duplicate proposal reuse, role-lens duplicate reuse, similar and conflicting active-memory review routing, transactional memory proposal writes, role-lens proposal writes, API-to-worker indexing completion with embedding row creation, provenance enforcement, request scope resolution, membership/grant access-control checks, the blocked cross-project direct read case, and memory fact repository round-trips with indexing outbox writes.

## TypeScript Tooling Verification

Run these checks after changing the review dashboard, admin console, or
vault-sync TypeScript sources:

```bash
cd tools/ui
npm run build
npm run check
```

After changing the IP-14 admin polish contract, run the focused static test:

```bash
dotnet test tests/MemorySystem.UnitTests/MemorySystem.UnitTests.csproj --filter FullyQualifiedName~AdminUxPolishIp14Tests
```

```bash
cd tools/vault-sync
npm run build
npm run check
```

`tools/ui` builds the generated dashboard JavaScript under
`src/MemorySystem.Api/wwwroot/reviews/` and the admin console JavaScript under
`src/MemorySystem.Api/wwwroot/admin/`. `tools/vault-sync` builds the generated
CLI output under `tools/vault-sync/dist/`.

## Backup and Restore Verification

Use [Backup and Restore Runbook](backup-restore.md) when PostgreSQL recovery behavior changes. The local verification path creates a custom-format `pg_dump`, restores it into a temporary validation database, checks migration history and table counts, then drops the validation database.

```bash
./scripts/backup-restore-smoke.sh
```

When the production PostgreSQL profile changes, render the managed profile
without connecting to a real database:

```bash
./scripts/external-postgres-profile-smoke.sh
```

When platform backup or restore job scripts change, run shell syntax checks:

```bash
bash -n scripts/production-container.sh
bash -n scripts/agent-memory-client.sh
bash -n scripts/weekly-admin-review-workflow.sh
bash -n scripts/backup-restore-drill-schedule.sh
bash -n scripts/access-boundary-review.sh
bash -n scripts/project-onboarding-runbook.sh
bash -n scripts/release-evidence-bundle.sh
bash -n scripts/directory-sync-evaluation.sh
bash -n scripts/backlog-roadmap-memory-sync.sh
bash -n scripts/seed-production-memory-boundary.sh
bash -n scripts/seed-production-knowledge-base.sh
bash -n scripts/source-backed-memory-hygiene.sh
bash -n scripts/external-postgres-profile-smoke.sh
bash -n scripts/platform-backup-export.sh
bash -n scripts/platform-compliance-evidence-package.sh
bash -n scripts/platform-erasure-replay-ledger-export.sh
bash -n scripts/platform-external-payload-retention-check.sh
bash -n scripts/platform-retention-minimization.sh
bash -n scripts/platform-restore-validation.sh
bash -n scripts/governance-compliance-release-smoke.sh
bash -n scripts/target-environment-evidence-verify.sh
```

## Production Backup And Restore Drill Schedule

Use this after changing the IP-10 drill schedule, backup/restore runbooks, or
release evidence requirements. The dry run validates the payload-safe schedule
contract without running backup, restore, or network commands:

```bash
bash -n scripts/backup-restore-drill-schedule.sh
scripts/backup-restore-drill-schedule.sh \
  --environment pilot \
  --drill-type monthly \
  --dry-run
```

## Access Boundary Review

Use this after changing the IP-11 access-boundary workflow, permission-drift
review, service-account posture, OIDC bindings, break-glass handling, or weekly
security review docs. The dry run validates the payload-safe review contract
without API calls:

```bash
bash -n scripts/access-boundary-review.sh
scripts/access-boundary-review.sh --dry-run
```

When a local API is available, collect the canonical project access review:

```bash
scripts/access-boundary-review.sh \
  --scope-type project \
  --scope-id 9f8e7d6c-5b4a-4321-9123-abcdef123002
```

## Source-Backed Memory Hygiene

Use this after changing `scripts/seed-production-knowledge-base.sh` or any
Markdown source document pinned by that seed. The hygiene gate validates source
hash drift, stale source links, curated excerpt drift, duplicate memory
identities, missing source evidence, canonical memory types, and role-lens seed
hygiene without calling the API.

```bash
./scripts/source-backed-memory-hygiene.sh
MEMORYSYSTEM_KNOWLEDGE_SEED_DRY_RUN=true ./scripts/seed-production-knowledge-base.sh
```

## Backlog And Roadmap Memory Sync

Use this after changing [Roadmap](roadmap.md), [Backlog](backlog.md), or the
roadmap/backlog entries in `scripts/seed-production-knowledge-base.sh`. The
sync report validates that Markdown remains canonical while source-backed seed
memory carries current hashes, excerpts, subjects, namespaces, and memory
types:

```bash
bash -n scripts/backlog-roadmap-memory-sync.sh
scripts/backlog-roadmap-memory-sync.sh --dry-run
scripts/source-backed-memory-hygiene.sh
MEMORYSYSTEM_KNOWLEDGE_SEED_DRY_RUN=true scripts/seed-production-knowledge-base.sh
```

## Role Lens First Content Pass

Use this after seeding the source-backed project knowledge base. The dry run
validates source excerpts and prints the planned role-lens bundle without API
calls; the live run resolves active responsibility base facts before proposing
canonical `role_lens` memories:

```bash
bash -n scripts/role-lens-first-pass.sh
scripts/role-lens-first-pass.sh --dry-run
scripts/role-lens-first-pass.sh
```

## Project Onboarding Runbook

Use this after changing the IP-16 onboarding workflow, project memory boundary
docs, role owner vocabulary, namespace grants, source-backed seed docs, or
review cadence. The dry run emits a payload-safe setup plan without API calls:

```bash
bash -n scripts/project-onboarding-runbook.sh
scripts/project-onboarding-runbook.sh --dry-run
dotnet test tests/MemorySystem.UnitTests/MemorySystem.UnitTests.csproj --filter FullyQualifiedName~ProjectOnboardingRunbookIp16Tests
```

## Release Evidence Bundle

Use this after changing the IP-17 release bundle workflow, release checklist,
evidence packaging, operations summary evidence, benchmark gate evidence,
backup/restore evidence, or rollback evidence. The dry run prints a
payload-safe manifest without writing output files:

```bash
bash -n scripts/release-evidence-bundle.sh
scripts/release-evidence-bundle.sh --dry-run --release-id local-draft
dotnet test tests/MemorySystem.UnitTests/MemorySystem.UnitTests.csproj --filter FullyQualifiedName~ReleaseEvidenceBundleAutomationIp17Tests
```

## Directory Sync Evaluation

Use this after changing the IP-18 directory sync evaluation, EA-10/Decision
0047 posture, access-boundary review docs, or local-grant authorization
boundaries. The dry run validates that sync remains provisioning-only and that
runtime authorization stays local:

```bash
bash -n scripts/directory-sync-evaluation.sh
scripts/directory-sync-evaluation.sh --dry-run
dotnet test tests/MemorySystem.UnitTests/MemorySystem.UnitTests.csproj --filter FullyQualifiedName~DirectorySyncEvaluationIp18Tests
```

## Agent Memory Client Wrapper

Use this after changing the IP-08 wrapper or its docs. The non-network checks
validate shell syntax and the local pending-state status path:

```bash
bash -n scripts/agent-memory-client.sh
scripts/agent-memory-client.sh --help
MEMORYSYSTEM_AGENT_MEMORY_STATE_FILE=/tmp/memorysystem-agent-memory-client-empty-test.json \
  scripts/agent-memory-client.sh status
```

When a local API is available, run a live prework and feedback closeout using a
temporary state file:

```bash
MEMORYSYSTEM_AGENT_MEMORY_STATE_FILE=/tmp/memorysystem-agent-memory-client-live-test.json \
  scripts/agent-memory-client.sh prework \
    --query "Agent memory wrapper smoke" \
    --scope-type project \
    --scope-id 9f8e7d6c-5b4a-4321-9123-abcdef123002 \
    --role-id developer

MEMORYSYSTEM_AGENT_MEMORY_STATE_FILE=/tmp/memorysystem-agent-memory-client-live-test.json \
  scripts/agent-memory-client.sh feedback --feedback-type missing
```

## Weekly Admin Review Workflow

Use this after changing the IP-09 weekly review workflow or related review
docs. The dry run validates the payload-safe queue contract without API calls:

```bash
bash -n scripts/weekly-admin-review-workflow.sh
scripts/weekly-admin-review-workflow.sh --dry-run
```

When a local API is available, collect the live weekly queue:

```bash
scripts/weekly-admin-review-workflow.sh \
  --scope-type project \
  --scope-id 9f8e7d6c-5b4a-4321-9123-abcdef123002
```

## Target Environment Evidence Verification

Use this before accepting post-GO target evidence for IP-04. The manifest must
list local payload-safe artifacts for every required target gate, and the
verifier checks each SHA-256 hash before the evidence prefix is accepted.

```bash
./scripts/target-environment-evidence-verify.sh /path/to/target-environment-evidence-manifest.json
```

## Governance/Compliance Release Smoke

Use this before pilot readiness review or after changing governance policy,
permission drift, audit export, retention minimization, erasure replay, or
compliance evidence packaging. The command creates an isolated database fixture,
generates payload-safe evidence, and requires the strict GC-06 evidence package
to include every required artifact.

```bash
./scripts/governance-compliance-release-smoke.sh
```

## Operations Metrics Smoke

Use this after starting the API with a configured local API key. The smoke
primes request metrics through `/api/operations/summary`, fetches
`/api/operations/metrics`, and verifies the first alert-input metrics are
present, including the IP-15 `memorysystem_memory_quality_*` metrics.

```bash
MEMORYSYSTEM_API_BASE_URL=http://127.0.0.1:5099 ./scripts/operations-metrics-smoke.sh
```

Use the focused database-backed operations endpoint test after changing
summary fields, quality calculations, or metric names:

```bash
dotnet test tests/MemorySystem.IntegrationTests/MemorySystem.IntegrationTests.csproj --filter FullyQualifiedName~OperationalSummaryEndpointTests
```

For loopback URLs only, the script uses the local demo key and prints that
choice. Set `MEMORYSYSTEM_API_KEY` for every non-loopback API, and do not use
`private-alpha-local-key` outside local demo traffic.

## Observability Artifact Smoke

Use this after changing pilot alerts, dashboard panels, trace coverage, or
metric input manifests. The smoke validates the Prometheus alert rules,
Grafana-compatible dashboard JSON, trace coverage manifest, and metric input
lists.

```bash
./scripts/observability-artifacts-smoke.sh
```

Set `MEMORYSYSTEM_OBSERVABILITY_VALIDATE_LIVE_METRICS=true` to also run the
live operations metrics smoke against `MEMORYSYSTEM_API_BASE_URL`.

## Context Product Benchmark Smoke

Use this after changing context packets, safe exclusions, context feedback, or
feedback-to-ranking behavior. It expects Scenario 0001 with benchmark overlays
and a running local API:

```bash
./scripts/context-product-benchmark-smoke.sh \
  --output benchmarks/outputs/context-product-v1/latest-smoke.run.json
```

When `--output` is omitted, the runner writes
`benchmarks/outputs/context-product-v1/latest.json`, which is the default file
read by `/api/operations/metrics` for context-product benchmark deltas.

## Benchmark Release Gate

Use this before a production-pilot release after the LLM outcome scorecard,
agent-contract scorecard, and agent-contract smoke output have been produced.
The fixture paths validate the gate itself without a running API:

```bash
./scripts/benchmark-release-gate.sh \
  --llm-scorecard benchmarks/release-gate/fixtures/llm-outcome-passing-scorecard.json \
  --contract-scorecard benchmarks/release-gate/fixtures/agent-contract-passing-scorecard.json \
  --agent-smoke benchmarks/release-gate/fixtures/agent-contract-smoke-passing.json \
  --output-dir /tmp/memorysystem-release-gate
```

## Production Pilot Deployment Smoke

Use this when changing deployment shape, runtime configuration, migrations,
worker indexing, backup/restore behavior, or release runbooks. The smoke
publishes the migrator, API, worker, and demo seeder; creates an isolated
PostgreSQL database; runs the migrator as a one-shot role; seeds Scenario 0001;
starts API and worker as separate processes; verifies health, operations,
alert inputs, read, and write paths; restores a backup into a fresh database;
then re-points API and worker at the restored database.

```bash
MEMORYSYSTEM_PRODUCTION_PILOT_SMOKE_API_KEY="$(openssl rand -hex 32)" \
  ./scripts/production-pilot-deployment-smoke.sh
```

Useful overrides:

- `MEMORYSYSTEM_PRODUCTION_PILOT_SMOKE_API_PORT=5199`
- `MEMORYSYSTEM_PRODUCTION_PILOT_SMOKE_API_KEY=<throwaway-smoke-api-key>`
- `MEMORYSYSTEM_PRODUCTION_PILOT_SMOKE_KEEP_DATABASES=true`
- `MEMORYSYSTEM_PRODUCTION_PILOT_SMOKE_KEEP_WORK_DIR=true`
