# External / Managed PostgreSQL Production Profile

Status: active production profile for improvement plan item IP-03.

## Purpose

The production container tool supports two PostgreSQL profiles:

| Profile | Use | Database owner |
| --- | --- | --- |
| `local` | Single-machine production-shaped deployments and host-local project memory. | `docker-compose.production.yml` starts `postgres` with the protected Docker volume `memorysystem-prod_memorysystem-postgres-data`. |
| `external` | Pilot or production deployments that use managed PostgreSQL or an operator-owned database. | The platform secret store provides `MEMORYSYSTEM_POSTGRES_CONNECTION_STRING`; the local `postgres` service is not started. |

The runtime contract is identical in both profiles: the one-shot migrator runs
first, API and worker use the same database, PostgreSQL remains the recovery
authority, and backup/restore validation must prove the selected target before
traffic is promoted.

## Local Profile

Local profile is the default:

```text
MEMORYSYSTEM_POSTGRES_PROFILE=local
MEMORYSYSTEM_POSTGRES_DB=memory_system
MEMORYSYSTEM_POSTGRES_USER=memory_system
MEMORYSYSTEM_POSTGRES_PASSWORD=<secret>
```

Use it when the deployment intentionally owns the Docker volume. Do not remove
the volume during normal rollback or restart. The protected repository-local
production memory volume is recorded in [Project Memory Boundary](project-memory-boundary.md).

## External Profile

External profile uses an already-provisioned PostgreSQL database with `pgvector`
available:

```text
MEMORYSYSTEM_POSTGRES_PROFILE=external
MEMORYSYSTEM_POSTGRES_CONNECTION_STRING="Host=<managed-host>;Port=5432;Database=memory_system;Username=<user>;Password=<secret>;SSL Mode=VerifyFull"
```

`SSL Mode=Require` is the minimum; prefer `VerifyFull` when the provider CA and
DNS name can be validated. Keep the value quoted in `.env.production` because
PostgreSQL connection-string keywords can contain spaces.

External profile preflight rejects:

- missing or placeholder connection strings
- local Compose targets such as `Host=postgres`, `localhost`, or `127.0.0.1`
- the local development password `memory_system_dev_password`
- connection strings without PostgreSQL TLS

The Docker Compose override
`docker-compose.production.external-postgres.yml` removes local `postgres`
dependencies from the migrator, API, and worker services. The base compose file
still keeps the local profile available, so switching back to local mode is a
configuration change rather than a file edit.

## Migration Flow

1. Confirm the managed database has `pgvector` support and a least-privilege
   memory-system credential.
2. Set `MEMORYSYSTEM_POSTGRES_PROFILE=external` and the managed connection
   string in the deployment secret source.
3. Run:

   ```bash
   scripts/production-container.sh preflight
   scripts/production-container.sh migrate
   ```

4. Capture the migrator output in the release evidence.
5. Start or roll the API and worker only after the migrator succeeds:

   ```bash
   scripts/production-container.sh up
   scripts/production-container.sh health
   ```

6. Verify `/api/operations/summary` and one authenticated memory read through
   the target ingress.

The API and worker must not apply migrations on startup. If a release requires
restore rollback, restore into a new database, run the migrator against that
database, update the connection-string secret, and then roll API/worker.

## Backup And Restore

For managed databases, provider snapshots or PITR are useful but not sufficient
as the application validation record. Keep the existing logical validation path:

```bash
/app/scripts/platform-backup-export.sh
/app/scripts/platform-erasure-replay-ledger-export.sh
/app/scripts/platform-restore-validation.sh
```

Platform jobs can use `MEMORYSYSTEM_POSTGRES_URL` or libpq variables such as
`PGHOST`, `PGPORT`, `PGDATABASE`, `PGUSER`, and `PGPASSWORD`. Restore
validation must set `MEMORYSYSTEM_RESTORE_CONNECTION_STRING` for the fresh
validation database so the migrator validates the restored schema.

For pilot and production, set:

```text
MEMORYSYSTEM_RESTORE_VALIDATION_REQUIRE_ERASURE_REPLAY=true
```

when validating a backup that may predate erasure or redaction actions. The
restore evidence must include table counts, the `vector` extension check,
erasure replay status when configured, and the target database name.

## Smoke

Render the external profile without touching a real database:

```bash
./scripts/external-postgres-profile-smoke.sh
```

The smoke creates a temporary env file with a fake managed connection string,
renders `scripts/production-container.sh config`, and fails if migrator, API,
or worker still depend on the local `postgres` service.

For a real target environment, the minimum smoke is:

```bash
scripts/production-container.sh preflight
scripts/production-container.sh migrate
scripts/production-container.sh up
scripts/production-container.sh health
MEMORYSYSTEM_API_BASE_URL=<target-url> ./scripts/operations-metrics-smoke.sh
```

Then run the platform backup export and restore validation jobs against the
same managed database profile before marking the target evidence complete.
