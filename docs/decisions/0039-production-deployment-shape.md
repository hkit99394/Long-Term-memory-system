# 0039 Production Deployment Shape

## Status

Accepted.

## Context

The private-alpha system now has separate deployable entrypoints for migrations,
HTTP traffic, and background work:

- `MemorySystem.Migrator`
- `MemorySystem.Api`
- `MemorySystem.Worker`

The production-pilot track needs a concrete deployment shape before adding
observability, admin workflows, or infrastructure automation. The shape must
preserve the existing SQL-first migration model, PostgreSQL recovery authority,
outbox worker behavior, secret handling, and restore runbook.

## Decision

Adopt [Production Deployment Shape](../production-deployment-shape.md) as the
production-pilot deployment plan.

The pilot deployment has three separate runtime roles:

- a single one-shot migrator job that runs before traffic moves to a new version
- one or more API instances for authenticated HTTP endpoints and health checks
- one or more worker instances for outbox processing, embeddings, heartbeat
  updates, and retention minimization

Use managed PostgreSQL or an equivalent operator-owned PostgreSQL service with
pgvector support as the system of record. Use a managed secret store or
equivalent secret broker for API keys, PostgreSQL credentials, and embedding
provider credentials. Use the backup/restore runbook for validation and prefer
restoring to a new database over overwriting the active production database.

Rollback normally rolls back API and worker artifacts while leaving already
applied forward migrations in place. Incompatible or destructive migrations
require a restore-to-new-database path or a forward repair migration.

## Consequences

- Deployment ownership is clear: migrator applies schema, API serves traffic,
  and worker processes asynchronous work.
- The API and worker no longer need to be treated as one process for production
  planning.
- Production rollout can be validated with existing health checks, operations
  summary data, and backup/restore checks before infrastructure-as-code exists.
- Migrations should remain backward-compatible by default. Destructive changes
  need explicit release gates and restore rehearsal.
- Future work can add container images, infrastructure modules, dashboards,
  alerts, and multi-region recovery without changing the current application
  role boundaries.

