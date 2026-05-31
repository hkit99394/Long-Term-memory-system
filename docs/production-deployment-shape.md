# Production Deployment Shape

Last updated: 2026-05-30

## Purpose

This document defines the production-pilot deployment shape for the Long-Term
Memory System. It is intentionally platform-neutral: the same roles can run on
containers, app services, virtual machines, or a managed job runner, as long as
the operational boundaries stay intact.

The goal for the pilot is a reliable service shape, not a full infrastructure
product. Infrastructure-as-code, regional redundancy, and a managed admin
console remain follow-on work.

## Target Topology

```text
Agent or operator clients
  -> TLS terminator or HTTPS ingress
  -> MemorySystem.Api instances
  -> managed PostgreSQL with pgvector

Deployment pipeline
  -> one-shot MemorySystem.Migrator job
  -> MemorySystem.Api rollout
  -> MemorySystem.Worker rollout

MemorySystem.Worker instances
  -> outbox job lease loop
  -> embedding provider
  -> managed PostgreSQL with pgvector

Secret store
  -> API key configuration
  -> PostgreSQL credentials
  -> embedding provider credentials
```

## Runtime Processes

| Process | Command shape | Responsibility | Scaling rule |
| --- | --- | --- | --- |
| Migrator | `dotnet MemorySystem.Migrator.dll --connection-string "$MEMORYSYSTEM_POSTGRES_CONNECTION_STRING" --migrations-directory migrations` | Apply ordered SQL migrations before new code receives traffic. | Run as a single one-shot deployment job. Do not run continuously. |
| API | `dotnet MemorySystem.Api.dll` | Serve authenticated memory, review, vault export, operations, and health endpoints. | Run at least two instances for pilot availability if the platform supports it. |
| Worker | `dotnet MemorySystem.Worker.dll` | Lease and process outbox jobs, write embeddings, record heartbeats, and run retention minimization. | Start with one instance; add more only after monitoring lease behavior, provider quota, and backlog age. |

The API and worker must use the same PostgreSQL database and embedding provider
configuration. The API must not apply migrations on startup. The worker must not
run until the migrator for the deployed version has completed successfully.

## PostgreSQL

Use managed PostgreSQL or an equivalent operator-owned PostgreSQL service with:

- `pgvector` extension support.
- Encrypted storage and encrypted backups.
- Point-in-time recovery when the provider supports it.
- A database credential dedicated to the memory system.
- A backup validation path that can restore into a separate database.

PostgreSQL is the recovery authority for events, memory facts, chunks,
embeddings, reviews, outbox jobs, idempotency records, retrieval feedback,
context packet observations, governance legal holds, vault export tracking, and
worker heartbeats. Vault files and generated benchmark outputs are projections,
not recovery sources.

## Secret Store Expectations

Production secrets must come from the deployment platform's secret store or an
equivalent managed secret broker. Environment variables are acceptable as the
delivery mechanism into the process, but not as the source of record.

Required secret-backed configuration:

- `Authentication:ApiKey:Keys:{keyId}:Key`
- `Authentication:ApiKey:Keys:{keyId}:PrincipalId`
- `Authentication:ApiKey:Keys:{keyId}:DisplayName`
- `MEMORYSYSTEM_POSTGRES_CONNECTION_STRING` or complete PostgreSQL connection parts
- `Embeddings:ApiKey` or `OPENAI_API_KEY`

The runtime guardrails in [Production Secret Handling](production-secrets.md)
remain mandatory for the pilot. Production-shaped environments must not use
local Docker Compose PostgreSQL credentials, deterministic embeddings, HTTP
traffic, placeholder API keys, or placeholder embedding credentials.

## Release Flow

1. Confirm the target database, image or artifact version, migration directory,
   API key configuration, PostgreSQL secret, embedding provider secret, and
   restore point.
2. Take or confirm a recent production backup before applying migrations.
3. Stop or pause worker instances if the release changes outbox payload shape,
   handler behavior, embedding model, or retention behavior.
4. Run the migrator as a one-shot job.
5. Confirm the migrator output: applied/skipped migrations and no checksum
   errors.
6. Deploy API instances with the new version.
7. Check `/health/live`, then `/health/ready`.
8. Deploy or resume worker instances.
9. Check `/api/operations/summary` with authentication.
10. Run one small authenticated read path and, for releases touching writes,
    one idempotent write path in the target environment.
11. Record the version, migration result, operator, health result, smoke result,
    and backup or restore point.

Schema changes should use expand-and-contract releases whenever old and new
code need to overlap. Destructive schema changes require a separate release
gate and a restore rehearsal.

## Executable Local Smoke

MR-10 adds a repeatable local smoke command for the deployment shape:

```bash
./scripts/production-pilot-deployment-smoke.sh
```

The smoke uses the local Docker Compose PostgreSQL service as a production-like
database target. It publishes the migrator, API, worker, and demo seeder; runs
the migrator as a one-shot role against an isolated database; seeds Scenario
0001 without precomputed embeddings; starts the API and worker as separate
processes; waits for the worker to complete indexing; verifies `/health/live`,
`/health/ready`, `/api/operations/summary`, an authenticated memory read, and
an authenticated write/read path; creates a custom-format backup; restores it
into a fresh database; runs the migrator against the restored database; compares
core table counts and `pgvector`; then restarts API and worker against the
restored database.

The smoke runs in the `Testing` environment with deterministic embeddings so it
can execute locally without external provider credentials or HTTPS ingress. It
does not replace a real pilot deployment rehearsal with managed PostgreSQL,
secret-store injection, TLS, and production embedding credentials. It does
prove the repo's operator steps and runtime role boundaries are executable.

## Rollback Procedure

Prefer rolling back application code, not the database.

For backward-compatible migrations:

1. Stop or pause worker instances.
2. Roll API instances back to the previous known-good artifact.
3. Roll worker instances back after the API is healthy.
4. Check `/health/ready`, `/api/operations/summary`, and the outbox dead-letter
   count.
5. Leave the already-applied migration in place and follow up with a forward
   fix if needed.

For incompatible or destructive migrations:

1. Stop API and worker processes.
2. Decide whether a forward migration can repair the issue safely.
3. If not, restore the pre-release backup into a new database.
4. Run the migrator for the target application version against the restored
   database.
5. Point API and worker secrets at the restored database.
6. Verify health and an authenticated read path before reopening traffic.

Do not edit `schema_migrations`, modify old migration files, or run ad hoc
manual schema changes as a normal rollback strategy. If an emergency manual fix
is unavoidable, capture it as a follow-up migration immediately after the
incident is stable.

## Restore Validation Path

The production restore path follows [Backup and Restore Runbook](backup-restore.md):

1. Restore into a fresh validation database.
2. Run the migrator against the restored database.
3. Verify the table-count manifest in `scripts/restore-validation-tables.txt`
   and `pg_extension` for `vector`.
4. Start API and worker processes against the restored database in an isolated
   validation environment when possible.
5. Check `/health/live`, `/health/ready`, and one authenticated memory read.
6. Document the backup id, restore timestamp, operator, row-count checks,
   application checks, and any known data-loss window.

Restoring over the active production database is break-glass only. The normal
pilot strategy is restore to a new database, update connection-string secrets,
and roll the application processes.

## Health and Operator Gates

Minimum production-pilot gates:

- `/health/live` is healthy before routing traffic.
- `/health/ready` is healthy after PostgreSQL, outbox backlog, worker heartbeat,
  and embedding provider checks pass.
- `/api/operations/summary` is reachable by an authenticated operator.
- `/api/operations/metrics` is reachable by an authenticated operator or metrics
  collector.
- Outbox dead-letter count is zero before and after release.
- Worker heartbeat is current after the worker rollout.
- Retrieval feedback metrics are present in the operator summary.
- Backup or point-in-time recovery status is known before migration.

The current implementation has health checks, structured operational logs, and
operator summary and metrics data. It also ships versioned pilot alert rules,
dashboard definitions, and trace coverage under `observability/`. It does not
yet ship runtime distributed tracing, platform exporters, or
infrastructure-as-code.

## Pilot Readiness Checklist

- Migrator, API, and worker run as separate deployment roles.
- API and worker share the same production PostgreSQL and embedding provider.
- API receives HTTPS directly or through trusted forwarded headers.
- PostgreSQL uses managed backups and restore validation.
- Secret values live outside the repository.
- A pre-release backup or restore point exists.
- Migration output is captured.
- Worker heartbeat and outbox backlog are checked after release.
- Operations metrics smoke passes against the target API.
- Restore to a new database has been rehearsed for the environment.
- Rollback ownership and escalation contacts are known before release.
