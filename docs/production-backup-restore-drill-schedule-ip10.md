# Production Backup And Restore Drill Schedule IP-10

Status: implemented for improvement plan item IP-10.

Owner: IT/Ops.

## Purpose

IP-10 turns the existing backup/export and restore-validation jobs into a
recurring operations habit. The schedule defines when to prove recovery, what
evidence to attach, how to measure RPO/RTO, and which protected-volume and
export checks must pass before pilot or production recovery evidence is
accepted.

The schedule generator is payload-safe and does not run database commands:

```bash
scripts/backup-restore-drill-schedule.sh \
  --environment production \
  --drill-type monthly \
  --dry-run
```

Use the generated checklist with [Backup and Restore Runbook](backup-restore.md)
and the platform scripts that already perform the actual work:

```bash
/app/scripts/platform-backup-export.sh
/app/scripts/platform-erasure-replay-ledger-export.sh
MEMORYSYSTEM_RESTORE_VALIDATION_REQUIRE_ERASURE_REPLAY=true /app/scripts/platform-restore-validation.sh
```

## Cadence

| Drill | Cadence | Completion rule |
| --- | --- | --- |
| Weekly backup freshness | Every week before the release window. | Latest successful backup export or managed recovery point is inside RPO and evidence is attached. |
| Monthly restore validation | First approved maintenance window each month. | Restore-to-new-database validation passes with table counts, `pgvector`, metrics, and erasure replay evidence. |
| Quarterly recovery rehearsal | Once per quarter in a named recovery window. | Restore validation plus application readiness, rollback owner, communication route, and known data-loss window are recorded. |
| Post-erasure replay | Within 7 days of an erasure or redaction batch that may be newer than retained backups. | Restore validation proves later redaction and erasure actions are replayed or verified. |
| Release gate | Before pilot or production go/no-go. | Backup evidence predates migration, restore validation is recent and passing, and target evidence verification succeeds. |

## RPO/RTO Expectations

| Environment | RPO expectation | RTO expectation |
| --- | --- | --- |
| Local | Best-effort developer data; no product RPO claim. | Complete `scripts/backup-restore-smoke.sh` within 30 minutes once Docker PostgreSQL is available. |
| CI | Not applicable for ephemeral CI databases. | Complete a database-backed restore validation job within 30 minutes when CI PostgreSQL is provisioned. |
| Pilot | 24 hours or less from the latest successful backup export or managed recovery point. | 4 hours or less for restore-to-new-database validation and application readiness proof. |
| Production | 1 hour or less when managed PITR evidence is attached; 24 hours or less from backup export until PITR is proven. | 2 hours or less for restore-to-new-database validation; break-glass production cutover requires incident commander sign-off. |

Measure RPO from the backup timestamp or managed recovery point to drill start.
Measure RTO from drill start to passing restore validation and readiness
evidence. Record observed RPO/RTO in every monthly, quarterly, post-erasure, and
release-gate evidence bundle.

## Evidence Contract

Every pilot or production drill must attach payload-safe evidence with:

- environment, operator, drill type, backup id, backup timestamp, restore
  database name, validation timestamp, and evidence prefix
- backup export evidence JSON and metrics with
  `memorysystem_backup_export_success` and `memorysystem_backup_age_seconds`
- restore validation evidence JSON and metrics with
  `memorysystem_restore_validation_success`,
  `memorysystem_restore_validation_table_rows`, and the `pgvector` check
- erasure replay ledger evidence and
  `memorysystem_restore_erasure_replay_validation_success` for pilot and
  production
- table manifest reference to `scripts/restore-validation-tables.txt`
- observed RPO, observed RTO, known data-loss window, and legal-hold or
  retention notes
- target evidence manifest verifier output when the drill is part of IP-04 or a
  release gate

The evidence must not include raw event payloads, memory bodies, chunk content,
review notes, API keys, database passwords, or logical dump contents.

## Protected Volume And Export Checks

Local checks:

- Keep the default Docker PostgreSQL volume protected unless an operator
  explicitly chooses a reset.
- Keep local logical dumps under `/tmp/memorysystem-backups` and out of git.
- Use `scripts/backup-restore-smoke.sh` for local recovery behavior changes.

Pilot and production checks:

- Use the managed PostgreSQL profile; the local Docker volume is not pilot or
  production recovery evidence.
- Restore validation must target a fresh database, not the active database.
- Set `MEMORYSYSTEM_RESTORE_VALIDATION_REQUIRE_ERASURE_REPLAY=true`.
- Keep logical backup files encrypted at rest, access-restricted, and outside
  the application image.
- Upload evidence JSON, metrics, ledger evidence, and verifier output to the
  release evidence bucket or controlled audit store.
- Confirm retention and legal-hold status before pruning backups or validation
  artifacts.

## Failure Rules

Do not close a drill when:

- backup age exceeds RPO
- restore validation is missing, stale, or failed
- erasure replay evidence is required but absent
- observed RTO exceeds the environment expectation without an owner-approved
  exception
- backup/restore evidence contains raw payloads or secrets
- the target evidence manifest verifier fails
- table-count drift is unexplained by migration, retention, or erasure evidence

Failed monthly, quarterly, post-erasure, or release-gate drills block production
release approval until the IT/Ops owner attaches passing evidence or records an
explicit owner-approved exception.

## Completion Evidence

IP-10 is complete when:

- `scripts/backup-restore-drill-schedule.sh` emits payload-safe schedules for
  local, CI, pilot, and production.
- the schedule defines weekly, monthly, quarterly, post-erasure, and release
  gate drills.
- RPO/RTO expectations, evidence capture, protected-volume checks, and export
  checks are documented.
- [Backup and Restore Runbook](backup-restore.md), [Testing Commands](testing.md),
  and the documentation index point to the schedule.
- unit tests cover the documentation contract and parse the dry-run output.
