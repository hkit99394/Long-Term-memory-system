# Backup and Restore Runbook

## Purpose

This runbook defines the MVP PostgreSQL backup and restore process for the memory system.

The database is the system of record for principals, scope references, events, memory facts, role lenses, chunks, embeddings, reviews, redactions, idempotency records, vault export tracking, outbox jobs, migrations, and worker heartbeats. Vault files and generated embeddings are projections; PostgreSQL is the recovery authority.

## Backup Scope

Back up the whole PostgreSQL database as one logical unit. Partial table backups are not supported for normal recovery because memory facts, source events, chunks, embeddings, reviews, outbox jobs, idempotency records, and migration history are linked.

The logical backup must include:

- schema and data
- `pgvector` extension metadata
- migration history
- audit and redaction records
- idempotency records still inside their retention window
- vault export tracking rows

Use PostgreSQL custom-format dumps for operator backups:

- they can be inspected with `pg_restore --list`
- they support selective restore when an operator deliberately needs it
- they restore cleanly into a fresh database for validation
- they avoid local role ownership coupling with `--no-owner --no-privileges`

## Local Backup

Start the local database:

```bash
docker compose up -d --wait postgres
```

For the normal local smoke check, use the repo script:

```bash
./scripts/backup-restore-smoke.sh
```

The script creates a custom-format backup under `/tmp/memorysystem-backups`, inspects it, restores it into a fresh validation database, runs migrations against the restored database, verifies the table counts listed in `scripts/restore-validation-tables.txt` plus `pgvector`, and drops the validation database when it exits. By default it removes the temporary backup file. Set `MEMORYSYSTEM_BACKUP_SMOKE_KEEP_BACKUP=true` to keep the dump for manual inspection.

Create a timestamped backup outside the repository:

```bash
BACKUP_DIR=/tmp/memorysystem-backups
mkdir -p "$BACKUP_DIR"

BACKUP_FILE="$BACKUP_DIR/memory-system-$(date -u +%Y%m%dT%H%M%SZ).dump"

docker compose exec -T postgres sh -lc \
  'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" --format=custom --blobs --no-owner --no-privileges' \
  > "$BACKUP_FILE"

pg_restore --list "$BACKUP_FILE" >/dev/null
```

Keep backup files out of git. Logical dumps can contain raw event payloads, memory text, API idempotency responses, and audit metadata.

## Local Restore Test

Restore into a fresh validation database, not over the active development database:

```bash
RESTORE_DB="memory_system_restore_check_$(date -u +%Y%m%d%H%M%S)"

docker compose exec -T postgres sh -lc \
  'createdb -U "$POSTGRES_USER" "$1"' \
  sh "$RESTORE_DB"

docker compose exec -T postgres sh -lc \
  'pg_restore -U "$POSTGRES_USER" -d "$1" --clean --if-exists --no-owner --no-privileges' \
  sh "$RESTORE_DB" < "$BACKUP_FILE"

docker compose exec -T postgres sh -lc \
  'psql -U "$POSTGRES_USER" -d "$1" -v ON_ERROR_STOP=1 -c "SELECT count(*) FROM schema_migrations;"' \
  sh "$RESTORE_DB"
```

When the validation is done, remove the temporary database:

```bash
docker compose exec -T postgres sh -lc \
  'dropdb -U "$POSTGRES_USER" "$1"' \
  sh "$RESTORE_DB"
```

The restore test should prove that:

- the dump is readable
- migrations history exists
- all tables listed in `scripts/restore-validation-tables.txt` can be queried
- `pgvector` extension-dependent objects restore without errors

## Production Restore Shape

For a production restore:

1. Identify the backup file, expected environment, database name, and restore timestamp.
2. Confirm retention and legal-hold expectations before restoring any backup that may contain erased payloads.
3. Stop API and worker processes that can write to the target database.
4. Take a pre-restore backup of the current target database.
5. Restore into a new database first when possible.
6. Run migrations against the restored database.
7. Verify database health and migration history.
8. Start the API and worker against the restored database.
9. Check `/health/ready` and a small authenticated read path.
10. Record the restore action, backup id, operator, validation result, and any data-loss window.

Prefer restoring into a new database and switching the connection string. Restoring over an existing production database should be a break-glass operation because `pg_restore --clean` drops and recreates objects from the dump.

## Production Command Pattern

The exact secret and host mechanism depends on the deployment environment. The restore pattern is:

```bash
pg_dump \
  "$MEMORYSYSTEM_POSTGRES_CONNECTION_STRING" \
  --format=custom \
  --blobs \
  --no-owner \
  --no-privileges \
  --file "$BACKUP_FILE"

pg_restore --list "$BACKUP_FILE" >/dev/null

createdb "$RESTORE_DATABASE_NAME"

pg_restore \
  --dbname "$RESTORE_CONNECTION_STRING" \
  --clean \
  --if-exists \
  --no-owner \
  --no-privileges \
  "$BACKUP_FILE"
```

After restore, run:

```bash
dotnet run --project src/MemorySystem.Migrator -- \
  --connection-string "$RESTORE_CONNECTION_STRING" \
  --migrations-directory migrations
```

Then point the API and worker at the restored connection string and verify readiness.

When the production container tool uses
`MEMORYSYSTEM_POSTGRES_PROFILE=external`, the live runtime connection string is
`MEMORYSYSTEM_POSTGRES_CONNECTION_STRING`; platform backup jobs should use
`MEMORYSYSTEM_POSTGRES_URL` or libpq `PG*` variables from the same secret
source, and restore validation should use a separate
`MEMORYSYSTEM_RESTORE_CONNECTION_STRING` for the fresh validation database. See
[External / Managed PostgreSQL Production Profile](external-managed-postgres-profile.md).

## Platform Automation

PI-04 adds two checked-in platform job scripts for pilot and production
environments, and GC-03 adds a payload-safe erasure replay ledger export for
restore validation:

```bash
/app/scripts/platform-backup-export.sh
/app/scripts/platform-erasure-replay-ledger-export.sh
/app/scripts/platform-restore-validation.sh
```

`platform-backup-export.sh` creates a custom-format logical backup, verifies
that the archive can be listed, writes backup export evidence JSON, and emits
Prometheus-compatible metrics including:

- `memorysystem_backup_export_success`
- `memorysystem_backup_age_seconds`
- `memorysystem_backup_export_timestamp_seconds`
- `memorysystem_backup_export_bytes`

`platform-restore-validation.sh` restores a selected backup file into a fresh
validation database, reruns migrations, checks every table in
`scripts/restore-validation-tables.txt`, verifies `pgvector`, writes restore
validation evidence JSON, and emits metrics including:

- `memorysystem_restore_validation_success`
- `memorysystem_restore_validation_age_seconds`
- `memorysystem_restore_validation_timestamp_seconds`
- `memorysystem_restore_validation_vector_extension_count`
- `memorysystem_restore_validation_table_rows`

`platform-erasure-replay-ledger-export.sh` exports a payload-safe CSV ledger
from `memory_redactions` with redaction timestamps, target ids, target types,
redaction audit ids, requester principal ids, and redaction type. It omits raw
event payloads, memory bodies, chunk content, review notes, and erasure reason
text. The ledger export writes evidence JSON plus metrics including:

- `memorysystem_erasure_replay_ledger_export_success`
- `memorysystem_erasure_replay_ledger_records`
- `memorysystem_erasure_replay_ledger_bytes`
- `memorysystem_erasure_replay_ledger_timestamp_seconds`

When `MEMORYSYSTEM_BACKUP_CREATED_AT_UTC` and
`MEMORYSYSTEM_ERASURE_REPLAY_LEDGER_FILE` are provided,
`platform-restore-validation.sh` imports the ledger, filters erasure and
redaction actions newer than the selected backup, replays or verifies those
actions in the restored database, and proves that source events, memory facts,
role lenses, chunks, embeddings, review notes, and vault exports no longer
expose erased payloads. Set
`MEMORYSYSTEM_RESTORE_VALIDATION_REQUIRE_ERASURE_REPLAY=true` for pilot and
production restore validation so a missing ledger or backup timestamp fails the
job. The restore evidence includes an `erasureReplay` section with replay
counts, legal-hold skips, validation failures, ledger hash, and target-set hash.

In the multi-role container image these scripts live under `/app/scripts/` and
use PostgreSQL client tools from the runtime image. The platform secret layer
must provide libpq-compatible values such as `PGHOST`, `PGPORT`, `PGUSER`, and
`PGDATABASE`; the secret value itself should come from the environment secret
store at task runtime. Restore validation also requires
`MEMORYSYSTEM_BACKUP_FILE` and `MEMORYSYSTEM_RESTORE_CONNECTION_STRING` so the
migrator can run against the validation database.

Default evidence and metric files are written under:

```text
/tmp/memorysystem-backup-evidence/
```

The ECS task wrapper or platform scheduler should upload evidence JSON to the
release evidence bucket and expose or push the metric files into the pilot
observability pipeline.

## Retention and Erasure Caveats

Backups can preserve content that has since been minimized or erased in the live database. Treat backup files as sensitive data.

Rules:

- encrypt backups at rest in production
- restrict restore permissions to operators who are allowed to see raw event payloads
- track which backups predate an erasure action
- do not restore an old backup over production without replaying later redaction and erasure actions through [GC-03 Backup Erasure Replay Validation](backup-erasure-replay-validation-gc03.md)
- respect `legal_hold` before deleting backups that may be the required preserved copy
- document backup deletion separately from database row deletion

M8-03 defines the retention and erasure policy. M8-04 restore procedures must honor it.

## Recovery Checks

Minimum checks after a restore:

```sql
SELECT count(*) FROM schema_migrations;
SELECT extname FROM pg_extension WHERE extname = 'vector';
```

For a populated environment, compare every table in `scripts/restore-validation-tables.txt` with the backup source or monitoring snapshot. Row counts are not a substitute for application verification, but they catch empty, schema-only, or partially restored tables quickly.

Application checks:

- `/health/live` returns healthy
- `/health/ready` returns healthy or only expected degraded worker status during controlled worker startup
- authenticated memory reads work for a known active memory
- pending review and vault export endpoints can query expected rows when those features are enabled
