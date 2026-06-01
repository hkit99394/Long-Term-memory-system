# GC-03 Backup Erasure Replay Validation

Date: 2026-06-01

Status: Implemented

## Purpose

GC-03 closes the restore-safety gap for backups that predate a governance
erasure. A restored database must not be promoted only because the dump is
readable and migrations pass. It must also prove that erasure and redaction
actions recorded after the selected backup are replayed or already verified.

The implementation is intentionally payload-safe:

- no source event payloads
- no memory fact bodies
- no chunk content
- no review notes
- no erasure reason text

## Operator Flow

1. Export a normal PostgreSQL backup with `scripts/platform-backup-export.sh`.
2. Export the payload-safe erasure replay ledger from the live database:

   ```bash
   MEMORYSYSTEM_BACKUP_ID="$BACKUP_ID" \
   MEMORYSYSTEM_ERASURE_REPLAY_LEDGER_FILE=/secure/evidence/erasure-replay-ledger.csv \
   ./scripts/platform-erasure-replay-ledger-export.sh
   ```

3. Restore the selected backup into a validation database and require replay
   validation:

   ```bash
   MEMORYSYSTEM_BACKUP_FILE=/secure/backups/memorysystem.dump \
   MEMORYSYSTEM_BACKUP_ID="$BACKUP_ID" \
   MEMORYSYSTEM_BACKUP_CREATED_AT_UTC=2026-06-01T12:00:00Z \
   MEMORYSYSTEM_ERASURE_REPLAY_LEDGER_FILE=/secure/evidence/erasure-replay-ledger.csv \
   MEMORYSYSTEM_RESTORE_VALIDATION_REQUIRE_ERASURE_REPLAY=true \
   MEMORYSYSTEM_RESTORE_CONNECTION_STRING="$RESTORE_CONNECTION_STRING" \
   ./scripts/platform-restore-validation.sh
   ```

4. Promote only after restore validation succeeds and its evidence contains
   `"erasureReplay": { "status": "succeeded" }`.

## Replay Ledger

`platform-erasure-replay-ledger-export.sh` writes a CSV file with these fields:

| Field | Purpose |
| --- | --- |
| `redaction_created_at_utc` | When the erasure or redaction ledger record was created. |
| `target_type` | `event` or `memory_fact`. |
| `target_id` | The source event or memory fact id to replay. |
| `redaction_event_id` | The original audit event id from `memory_redactions.source_event_id`. |
| `requested_by_principal_id` | The operator principal id when present. |
| `redaction_type` | The redaction operation, currently `redact`. |

The ledger evidence records the CSV byte size, SHA-256 hash, row count, backup
id, environment, and omitted payload fields.

## Restore-Time Validation

`platform-restore-validation.sh` imports the ledger into a temporary table,
filters actions newer than `MEMORYSYSTEM_BACKUP_CREATED_AT_UTC`, and then
replays those actions against the restored database.

Replay updates:

- source events become `retention_class = 'erasure_requested'`,
  `redaction_status = 'erased'`, `external_payload_uri = NULL`, and a
  sanitized erased JSON marker
- memory facts become `status = 'redacted'` with the governance erased marker
- role lenses become `status = 'redacted'`
- chunks receive the governance erased marker, erased content hash, and
  `redacted_at`
- embeddings for replayed chunks are deleted
- review notes are cleared
- vault exports are marked `stale` with `source_erased`

Active legal holds still win. A restored event that is under an active legal
hold is counted in `heldEvents` and skipped by replay until the hold is
resolved by the normal governance workflow.

## Evidence And Metrics

Restore validation evidence now includes:

- `backupId`
- `restoreId`
- `backupCreatedAtUtc`
- `erasureReplay.status`
- `erasureReplay.required`
- `erasureReplay.payloadSafe`
- `erasureReplay.ledgerSha256`
- `erasureReplay.actionsNewerThanBackup`
- `erasureReplay.replayedEvents`
- `erasureReplay.heldEvents`
- `erasureReplay.redactedFacts`
- `erasureReplay.redactedChunks`
- `erasureReplay.deletedEmbeddings`
- `erasureReplay.validationFailureCount`
- `erasureReplay.targetSetHash`

Restore validation metrics now include:

- `memorysystem_restore_erasure_replay_validation_success`
- `memorysystem_restore_erasure_replay_configured`
- `memorysystem_restore_erasure_replay_required`
- `memorysystem_restore_erasure_replay_actions`
- `memorysystem_restore_erasure_replay_failures`

Ledger export metrics include:

- `memorysystem_erasure_replay_ledger_export_success`
- `memorysystem_erasure_replay_ledger_records`
- `memorysystem_erasure_replay_ledger_bytes`
- `memorysystem_erasure_replay_ledger_timestamp_seconds`

## Promotion Rule

For pilot and production restores, set
`MEMORYSYSTEM_RESTORE_VALIDATION_REQUIRE_ERASURE_REPLAY=true`. A restore
validation without a backup timestamp or ledger is acceptable only for local
schema smoke checks, never for promoting a database that may contain user or
project memory.

## Follow-On Work

GC-04 now builds on this by adding standard and audit retention minimization.
GC-05 now adds external payload-store retention checks. GC-06 can link erasure
replay and external payload evidence into a compliance evidence package without
changing the payload-safety boundary.
