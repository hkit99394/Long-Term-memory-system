# 0037 Backup and Restore Runbook

## Status

Accepted.

## Context

M8-04 needs a documented and locally tested database backup and restore process. PostgreSQL is the system of record, while vault files, chunks, embeddings, and worker state are either stored in PostgreSQL or generated projections that must recover from PostgreSQL truth.

The project uses SQL-first migrations and local Docker Compose PostgreSQL with pgvector, so the backup process should be understandable without introducing a cloud-specific backup service yet.

## Decision

Adopt [Backup and Restore Runbook](../backup-restore.md) as the MVP operator runbook.

Use PostgreSQL custom-format logical backups:

```text
pg_dump --format=custom --blobs --no-owner --no-privileges
```

Restore tests use a fresh validation database and:

```text
pg_restore --clean --if-exists --no-owner --no-privileges
```

After restore, run the migration runner against the restored database and verify health/read paths before declaring the restore complete.

Backups are sensitive because they can contain raw event payloads, memory text, idempotency responses, and payloads that were erased after the backup was taken. Backup retention and restore procedures must honor the M8 retention and erasure policy.

## Consequences

- Operators have a repeatable local backup and restore verification path.
- Production can later swap in managed PostgreSQL snapshot tooling while preserving the same validation expectations.
- Restore over an existing production database is explicitly break-glass; restoring to a new database and switching the connection string is preferred.
- Backup handling is tied to legal hold and erasure expectations instead of being treated as a neutral file-copy process.
