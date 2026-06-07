#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "$ROOT_DIR/scripts/lib/checksums.sh"
. "$ROOT_DIR/scripts/restore-validation-tables.sh"

ENVIRONMENT="${MEMORYSYSTEM_ENVIRONMENT:-local}"
RUN_ID="$(date -u +%Y%m%dT%H%M%SZ)_$$"
BACKUP_ID="${MEMORYSYSTEM_BACKUP_ID:-}"
RESTORE_ID="${MEMORYSYSTEM_RESTORE_ID:-restore-validation-$RUN_ID}"
BACKUP_FILE="${MEMORYSYSTEM_BACKUP_FILE:-}"
BACKUP_CREATED_AT_UTC="${MEMORYSYSTEM_BACKUP_CREATED_AT_UTC:-}"
RESTORE_DB="${MEMORYSYSTEM_RESTORE_DATABASE:-memorysystem_restore_validation_$RUN_ID}"
RESTORE_CONNECTION_STRING="${MEMORYSYSTEM_RESTORE_CONNECTION_STRING:-}"
MIGRATIONS_DIR="${MEMORYSYSTEM_MIGRATIONS_DIR:-$ROOT_DIR/migrations}"
MIGRATOR_DLL="${MEMORYSYSTEM_MIGRATOR_DLL:-$ROOT_DIR/migrator/MemorySystem.Migrator.dll}"
EVIDENCE_DIR="${MEMORYSYSTEM_RESTORE_VALIDATION_EVIDENCE_DIR:-/tmp/memorysystem-backup-evidence}"
METRICS_FILE="${MEMORYSYSTEM_RESTORE_VALIDATION_METRICS_FILE:-$EVIDENCE_DIR/restore-validation-metrics.prom}"
EVIDENCE_FILE="${MEMORYSYSTEM_RESTORE_VALIDATION_EVIDENCE_FILE:-$EVIDENCE_DIR/restore-validation-evidence.json}"
TABLES_FILE="${MEMORYSYSTEM_RESTORE_VALIDATION_TABLES_FILE:-$ROOT_DIR/scripts/restore-validation-tables.txt}"
ERASURE_REPLAY_LEDGER_FILE="${MEMORYSYSTEM_ERASURE_REPLAY_LEDGER_FILE:-}"
ERASURE_REPLAY_REQUIRED="${MEMORYSYSTEM_RESTORE_VALIDATION_REQUIRE_ERASURE_REPLAY:-false}"
KEEP_DATABASE="${MEMORYSYSTEM_RESTORE_VALIDATION_KEEP_DATABASE:-false}"
STARTED_AT_UTC="$(date -u +"%Y-%m-%dT%H:%M:%SZ")"
STARTED_AT_SECONDS="$(date -u +%s)"
COMPLETED_AT_UTC=""
COMPLETED_AT_SECONDS="$STARTED_AT_SECONDS"
STATUS="failed"
ERROR_MESSAGE=""
RESTORE_DATABASE_DROPPED=false
VECTOR_EXTENSION_COUNT=0
TABLE_COUNTS_JSON="{}"
TABLE_COUNT_METRICS=""
ERASURE_REPLAY_STATUS="not_configured"
ERASURE_REPLAY_LEDGER_BYTES=0
ERASURE_REPLAY_LEDGER_SHA256=""
ERASURE_REPLAY_LEDGER_RECORDS=0
ERASURE_REPLAY_ACTIONS_NEWER_THAN_BACKUP=0
ERASURE_REPLAY_TARGET_EVENTS=0
ERASURE_REPLAY_REPLAYED_EVENTS=0
ERASURE_REPLAY_HELD_EVENTS=0
ERASURE_REPLAY_REDACTED_FACTS=0
ERASURE_REPLAY_REDACTED_ROLE_LENSES=0
ERASURE_REPLAY_REDACTED_CHUNKS=0
ERASURE_REPLAY_STALE_VAULT_EXPORTS=0
ERASURE_REPLAY_CLEARED_REVIEW_NOTES=0
ERASURE_REPLAY_DELETED_EMBEDDINGS=0
ERASURE_REPLAY_FAILURES=0
ERASURE_REPLAY_TARGET_SET_HASH=""
created_restore_db=false

json_escape() {
  local value="$1"
  value="${value//\\/\\\\}"
  value="${value//\"/\\\"}"
  value="${value//$'\n'/\\n}"
  value="${value//$'\r'/\\r}"
  printf '%s' "$value"
}

metric_label_escape() {
  local value="$1"
  value="${value//\\/\\\\}"
  value="${value//\"/\\\"}"
  value="${value//$'\n'/\\n}"
  printf '%s' "$value"
}

require_command() {
  local name="$1"

  if ! command -v "$name" >/dev/null 2>&1; then
    echo "Required command not found: $name" >&2
    exit 1
  fi
}

require_value() {
  local name="$1"
  local value="$2"

  if [[ -z "$value" ]]; then
    echo "Required environment value is missing: $name" >&2
    exit 1
  fi
}

run_migrator() {
  if [[ -f "$MIGRATOR_DLL" ]]; then
    dotnet "$MIGRATOR_DLL" \
      --connection-string "$RESTORE_CONNECTION_STRING" \
      --migrations-directory "$MIGRATIONS_DIR" >/dev/null
    return
  fi

  if [[ -f "$ROOT_DIR/src/MemorySystem.Migrator/MemorySystem.Migrator.csproj" ]]; then
    dotnet run --project "$ROOT_DIR/src/MemorySystem.Migrator" --configuration Release -- \
      --connection-string "$RESTORE_CONNECTION_STRING" \
      --migrations-directory "$MIGRATIONS_DIR" >/dev/null
    return
  fi

  echo "Could not locate MemorySystem.Migrator. Set MEMORYSYSTEM_MIGRATOR_DLL." >&2
  exit 1
}

restore_scalar() {
  local sql="$1"

  psql -d "$RESTORE_DB" -At -v ON_ERROR_STOP=1 -c "$sql"
}

build_table_counts() {
  local first=true
  local json="{"
  local metrics=""
  local escaped_environment
  escaped_environment="$(metric_label_escape "$ENVIRONMENT")"

  load_restore_validation_tables "$TABLES_FILE"

  for table_name in "${RESTORE_VALIDATION_TABLES[@]}"; do
    local row_count
    local escaped_table
    row_count="$(restore_scalar "SELECT count(*) FROM $table_name;")"
    escaped_table="$(metric_label_escape "$table_name")"

    if [[ "$first" == "true" ]]; then
      first=false
      json="$json"$'\n'
    else
      json="$json,"$'\n'
    fi

    json="$json    \"$(json_escape "$table_name")\": $row_count"
    metrics="${metrics}memorysystem_restore_validation_table_rows{environment=\"$escaped_environment\",table=\"$escaped_table\"} $row_count"$'\n'
    printf '  %-32s %s\n' "$table_name" "$row_count"
  done

  if [[ "$first" == "false" ]]; then
    json="$json"$'\n'"  }"
  else
    json="{}"
  fi

  TABLE_COUNTS_JSON="$json"
  TABLE_COUNT_METRICS="$metrics"
}

validate_erasure_replay() {
  if [[ -z "$BACKUP_CREATED_AT_UTC" || -z "$ERASURE_REPLAY_LEDGER_FILE" ]]; then
    ERASURE_REPLAY_STATUS="not_configured"

    if [[ "$ERASURE_REPLAY_REQUIRED" == "true" ]]; then
      echo "Erasure replay validation requires MEMORYSYSTEM_BACKUP_CREATED_AT_UTC and MEMORYSYSTEM_ERASURE_REPLAY_LEDGER_FILE." >&2
      exit 1
    fi

    return
  fi

  if [[ ! -f "$ERASURE_REPLAY_LEDGER_FILE" ]]; then
    ERASURE_REPLAY_STATUS="failed"
    echo "Erasure replay ledger not found: $ERASURE_REPLAY_LEDGER_FILE" >&2
    exit 1
  fi

  ERASURE_REPLAY_LEDGER_BYTES="$(wc -c <"$ERASURE_REPLAY_LEDGER_FILE" | tr -d ' ')"
  ERASURE_REPLAY_LEDGER_SHA256="$(sha256_file "$ERASURE_REPLAY_LEDGER_FILE")"
  ERASURE_REPLAY_LEDGER_RECORDS="$(awk 'NR > 1 { count++ } END { print count + 0 }' "$ERASURE_REPLAY_LEDGER_FILE")"
  ERASURE_REPLAY_STATUS="failed"

  local result
  result="$(
    psql -d "$RESTORE_DB" -qAt -F $'\t' -v ON_ERROR_STOP=1 \
      -v backup_created_at="$BACKUP_CREATED_AT_UTC" \
      -v ledger_file="$ERASURE_REPLAY_LEDGER_FILE" <<'SQL' | tail -n 1
CREATE TEMP TABLE erasure_replay_ledger_raw (
    redaction_created_at_utc TEXT NOT NULL,
    target_type TEXT NOT NULL,
    target_id TEXT NOT NULL,
    redaction_event_id TEXT NOT NULL,
    requested_by_principal_id TEXT,
    redaction_type TEXT NOT NULL
);

\copy erasure_replay_ledger_raw (redaction_created_at_utc, target_type, target_id, redaction_event_id, requested_by_principal_id, redaction_type) FROM :'ledger_file' WITH (FORMAT csv, HEADER true)

CREATE TEMP TABLE erasure_replay_ledger AS
SELECT DISTINCT
    redaction_created_at_utc::timestamptz AS redaction_created_at,
    lower(target_type) AS target_type,
    target_id::uuid AS target_id,
    NULLIF(redaction_event_id, '')::uuid AS redaction_event_id,
    NULLIF(requested_by_principal_id, '')::uuid AS requested_by_principal_id,
    lower(redaction_type) AS redaction_type
FROM erasure_replay_ledger_raw
WHERE lower(target_type) IN ('event', 'memory_fact')
    AND lower(redaction_type) = 'redact'
    AND target_id ~* '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
    AND redaction_event_id ~* '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
    AND (
        requested_by_principal_id IS NULL
        OR requested_by_principal_id = ''
        OR requested_by_principal_id ~* '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
    );

CREATE TEMP TABLE post_backup_erasure_ledger AS
SELECT *
FROM erasure_replay_ledger
WHERE redaction_created_at > :'backup_created_at'::timestamptz;

CREATE TEMP TABLE replay_target_events AS
SELECT DISTINCT ledger.target_id AS event_id
FROM post_backup_erasure_ledger AS ledger
WHERE ledger.target_type = 'event'

UNION

SELECT DISTINCT fact.source_event_id AS event_id
FROM post_backup_erasure_ledger AS ledger
INNER JOIN memory_facts AS fact
    ON fact.id = ledger.target_id
WHERE ledger.target_type = 'memory_fact';

CREATE TEMP TABLE replay_existing_events AS
SELECT DISTINCT event.id AS event_id
FROM replay_target_events AS target
INNER JOIN events AS event
    ON event.id = target.event_id;

CREATE TEMP TABLE replay_held_events AS
SELECT DISTINCT existing.event_id
FROM replay_existing_events AS existing
WHERE EXISTS (
    SELECT 1
    FROM governance_legal_hold_events AS link
    INNER JOIN governance_legal_holds AS hold
        ON hold.id = link.legal_hold_id
        AND hold.status = 'active'
    WHERE link.event_id = existing.event_id
        AND link.released_at IS NULL
);

CREATE TEMP TABLE replay_events AS
SELECT existing.event_id
FROM replay_existing_events AS existing
LEFT JOIN replay_held_events AS held
    ON held.event_id = existing.event_id
WHERE held.event_id IS NULL;

CREATE TEMP TABLE replay_target_facts AS
SELECT DISTINCT fact.id AS fact_id
FROM memory_facts AS fact
INNER JOIN replay_events AS replay
    ON replay.event_id = fact.source_event_id

UNION

SELECT DISTINCT fact.id AS fact_id
FROM post_backup_erasure_ledger AS ledger
INNER JOIN memory_facts AS fact
    ON fact.id = ledger.target_id
LEFT JOIN replay_held_events AS held
    ON held.event_id = fact.source_event_id
WHERE ledger.target_type = 'memory_fact'
    AND held.event_id IS NULL;

CREATE TEMP TABLE replay_target_role_lenses AS
SELECT DISTINCT lens.id AS lens_id
FROM role_memory_lenses AS lens
WHERE lens.source_event_id IN (SELECT event_id FROM replay_events)
    OR lens.base_memory_fact_id IN (SELECT fact_id FROM replay_target_facts);

CREATE TEMP TABLE replay_target_chunks AS
SELECT DISTINCT chunk.id AS chunk_id
FROM memory_chunks AS chunk
WHERE chunk.source_event_id IN (SELECT event_id FROM replay_events)
    OR (chunk.source_type = 'memory_fact' AND chunk.source_id IN (SELECT fact_id FROM replay_target_facts))
    OR (chunk.source_type = 'role_memory_lens' AND chunk.source_id IN (SELECT lens_id FROM replay_target_role_lenses));

CREATE TEMP TABLE replay_target_vault_exports AS
SELECT DISTINCT export.id AS vault_export_id
FROM vault_exports AS export
WHERE export.source_event_id IN (SELECT event_id FROM replay_events)
    OR export.memory_fact_id IN (SELECT fact_id FROM replay_target_facts);

CREATE TEMP TABLE replay_target_embeddings AS
SELECT embedding.chunk_id, embedding.embedding_model
FROM memory_embeddings AS embedding
WHERE embedding.chunk_id IN (SELECT chunk_id FROM replay_target_chunks);

CREATE TEMP TABLE replay_target_reviews_with_notes AS
SELECT review.id AS review_id
FROM memory_reviews AS review
WHERE review.notes IS NOT NULL
    AND (
        review.source_event_id IN (SELECT event_id FROM replay_events)
        OR review.memory_fact_id IN (SELECT fact_id FROM replay_target_facts)
    );

UPDATE role_memory_lenses
SET interpretation = '[erased by governance workflow]',
    status = 'redacted'
WHERE id IN (SELECT lens_id FROM replay_target_role_lenses);

UPDATE memory_facts
SET subject = '[erased by governance workflow]',
    predicate = 'erased',
    object = '[erased by governance workflow]',
    confidence = 0,
    status = 'redacted'
WHERE id IN (SELECT fact_id FROM replay_target_facts);

DELETE FROM memory_embeddings
WHERE chunk_id IN (SELECT chunk_id FROM replay_target_chunks);

UPDATE memory_chunks
SET title = NULL,
    content = '[erased by governance workflow]',
    content_hash = 'sha256:governance-erased',
    redacted_at = COALESCE(redacted_at, now())
WHERE id IN (SELECT chunk_id FROM replay_target_chunks);

UPDATE memory_reviews
SET notes = NULL
WHERE id IN (SELECT review_id FROM replay_target_reviews_with_notes);

UPDATE vault_exports
SET status = 'stale',
    stale_reason = 'source_erased',
    stale_at = COALESCE(stale_at, now())
WHERE id IN (SELECT vault_export_id FROM replay_target_vault_exports);

UPDATE events
SET content = jsonb_build_object(
        'erased', true,
        'reason', 'restore_validation_erasure_replay',
        'backupCreatedAtUtc', :'backup_created_at'
    ),
    external_payload_uri = NULL,
    retention_class = 'erasure_requested',
    redaction_status = 'erased',
    redacted_at = COALESCE(redacted_at, now()),
    redaction_event_id = NULL
WHERE id IN (SELECT event_id FROM replay_events);

CREATE TEMP TABLE erasure_replay_failures AS
SELECT 'event' AS target_type, event.id AS target_id
FROM replay_events AS replay
INNER JOIN events AS event
    ON event.id = replay.event_id
WHERE event.retention_class <> 'erasure_requested'
    OR event.redaction_status <> 'erased'
    OR event.external_payload_uri IS NOT NULL
    OR event.content->>'erased' <> 'true'

UNION ALL

SELECT 'memory_fact', fact.id
FROM replay_target_facts AS target
INNER JOIN memory_facts AS fact
    ON fact.id = target.fact_id
WHERE fact.status <> 'redacted'
    OR fact.subject <> '[erased by governance workflow]'
    OR fact.predicate <> 'erased'
    OR fact.object <> '[erased by governance workflow]'

UNION ALL

SELECT 'role_memory_lens', lens.id
FROM replay_target_role_lenses AS target
INNER JOIN role_memory_lenses AS lens
    ON lens.id = target.lens_id
WHERE lens.status <> 'redacted'
    OR lens.interpretation <> '[erased by governance workflow]'

UNION ALL

SELECT 'memory_chunk', chunk.id
FROM replay_target_chunks AS target
INNER JOIN memory_chunks AS chunk
    ON chunk.id = target.chunk_id
WHERE chunk.redacted_at IS NULL
    OR chunk.content <> '[erased by governance workflow]'
    OR chunk.content_hash <> 'sha256:governance-erased'

UNION ALL

SELECT 'memory_embedding', embedding.chunk_id
FROM memory_embeddings AS embedding
WHERE embedding.chunk_id IN (SELECT chunk_id FROM replay_target_chunks)

UNION ALL

SELECT 'memory_review', review.id
FROM memory_reviews AS review
WHERE review.notes IS NOT NULL
    AND (
        review.source_event_id IN (SELECT event_id FROM replay_events)
        OR review.memory_fact_id IN (SELECT fact_id FROM replay_target_facts)
    )

UNION ALL

SELECT 'vault_export', export.id
FROM replay_target_vault_exports AS target
INNER JOIN vault_exports AS export
    ON export.id = target.vault_export_id
WHERE export.status <> 'stale'
    OR export.stale_reason <> 'source_erased';

SELECT concat_ws(
    E'\t',
    (SELECT count(*) FROM post_backup_erasure_ledger)::text,
    (SELECT count(*) FROM replay_target_events)::text,
    (SELECT count(*) FROM replay_events)::text,
    (SELECT count(*) FROM replay_held_events)::text,
    (SELECT count(*) FROM replay_target_facts)::text,
    (SELECT count(*) FROM replay_target_role_lenses)::text,
    (SELECT count(*) FROM replay_target_chunks)::text,
    (SELECT count(*) FROM replay_target_vault_exports)::text,
    (SELECT count(*) FROM replay_target_reviews_with_notes)::text,
    (SELECT count(*) FROM replay_target_embeddings)::text,
    (SELECT count(*) FROM erasure_replay_failures)::text,
    COALESCE(
        'md5:' || (
            SELECT md5(string_agg(ledger.target_type || ':' || ledger.target_id::text, ',' ORDER BY ledger.target_type, ledger.target_id::text))
            FROM post_backup_erasure_ledger AS ledger
        ),
        ''
    )
);
SQL
  )"

  IFS=$'\t' read -r \
    ERASURE_REPLAY_ACTIONS_NEWER_THAN_BACKUP \
    ERASURE_REPLAY_TARGET_EVENTS \
    ERASURE_REPLAY_REPLAYED_EVENTS \
    ERASURE_REPLAY_HELD_EVENTS \
    ERASURE_REPLAY_REDACTED_FACTS \
    ERASURE_REPLAY_REDACTED_ROLE_LENSES \
    ERASURE_REPLAY_REDACTED_CHUNKS \
    ERASURE_REPLAY_STALE_VAULT_EXPORTS \
    ERASURE_REPLAY_CLEARED_REVIEW_NOTES \
    ERASURE_REPLAY_DELETED_EMBEDDINGS \
    ERASURE_REPLAY_FAILURES \
    ERASURE_REPLAY_TARGET_SET_HASH <<<"$result"

  if [[ "$ERASURE_REPLAY_FAILURES" != "0" ]]; then
    ERASURE_REPLAY_STATUS="failed"
    echo "Erasure replay validation failed with $ERASURE_REPLAY_FAILURES failed rows." >&2
    exit 1
  fi

  ERASURE_REPLAY_STATUS="succeeded"
  printf 'Erasure replay validation complete: %s post-backup redaction actions, %s replayed events\n' \
    "$ERASURE_REPLAY_ACTIONS_NEWER_THAN_BACKUP" \
    "$ERASURE_REPLAY_REPLAYED_EVENTS"
}

write_metrics() {
  local success_value="0"
  local restore_age_seconds="0"
  local erasure_replay_success_value="0"
  local erasure_replay_configured_value="0"
  local erasure_replay_required_value="0"
  local escaped_environment
  escaped_environment="$(metric_label_escape "$ENVIRONMENT")"

  if [[ "$STATUS" == "succeeded" ]]; then
    success_value="1"
    restore_age_seconds="0"
  fi

  if [[ "$ERASURE_REPLAY_STATUS" == "succeeded" ]]; then
    erasure_replay_success_value="1"
  fi

  if [[ "$ERASURE_REPLAY_STATUS" != "not_configured" ]]; then
    erasure_replay_configured_value="1"
  fi

  if [[ "$ERASURE_REPLAY_REQUIRED" == "true" ]]; then
    erasure_replay_required_value="1"
  fi

  mkdir -p "$(dirname "$METRICS_FILE")"
  cat >"$METRICS_FILE" <<EOF
# HELP memorysystem_restore_validation_success Whether the latest restore-validation job completed successfully.
# TYPE memorysystem_restore_validation_success gauge
memorysystem_restore_validation_success{environment="$escaped_environment"} $success_value
# HELP memorysystem_restore_validation_age_seconds Age of the latest successful restore validation in seconds.
# TYPE memorysystem_restore_validation_age_seconds gauge
memorysystem_restore_validation_age_seconds{environment="$escaped_environment"} $restore_age_seconds
# HELP memorysystem_restore_validation_timestamp_seconds Unix timestamp for the latest restore-validation attempt.
# TYPE memorysystem_restore_validation_timestamp_seconds gauge
memorysystem_restore_validation_timestamp_seconds{environment="$escaped_environment"} $COMPLETED_AT_SECONDS
# HELP memorysystem_restore_validation_vector_extension_count Count of pgvector extension rows in the restored database.
# TYPE memorysystem_restore_validation_vector_extension_count gauge
memorysystem_restore_validation_vector_extension_count{environment="$escaped_environment"} $VECTOR_EXTENSION_COUNT
# HELP memorysystem_restore_validation_table_rows Restored row count for a table in the validation manifest.
# TYPE memorysystem_restore_validation_table_rows gauge
${TABLE_COUNT_METRICS}
# HELP memorysystem_restore_erasure_replay_validation_success Whether the latest restore erasure replay validation completed successfully.
# TYPE memorysystem_restore_erasure_replay_validation_success gauge
memorysystem_restore_erasure_replay_validation_success{environment="$escaped_environment"} $erasure_replay_success_value
# HELP memorysystem_restore_erasure_replay_configured Whether restore erasure replay validation had a backup timestamp and replay ledger.
# TYPE memorysystem_restore_erasure_replay_configured gauge
memorysystem_restore_erasure_replay_configured{environment="$escaped_environment"} $erasure_replay_configured_value
# HELP memorysystem_restore_erasure_replay_required Whether restore erasure replay validation was required for this run.
# TYPE memorysystem_restore_erasure_replay_required gauge
memorysystem_restore_erasure_replay_required{environment="$escaped_environment"} $erasure_replay_required_value
# HELP memorysystem_restore_erasure_replay_actions Redaction ledger actions newer than the selected backup.
# TYPE memorysystem_restore_erasure_replay_actions gauge
memorysystem_restore_erasure_replay_actions{environment="$escaped_environment"} $ERASURE_REPLAY_ACTIONS_NEWER_THAN_BACKUP
# HELP memorysystem_restore_erasure_replay_failures Rows that still exposed or retained erased projections after replay validation.
# TYPE memorysystem_restore_erasure_replay_failures gauge
memorysystem_restore_erasure_replay_failures{environment="$escaped_environment"} $ERASURE_REPLAY_FAILURES
EOF
}

write_evidence() {
  mkdir -p "$(dirname "$EVIDENCE_FILE")"
  cat >"$EVIDENCE_FILE" <<EOF
{
  "schemaVersion": 1,
  "kind": "memorysystem.restore_validation",
  "environment": "$(json_escape "$ENVIRONMENT")",
  "backupId": "$(json_escape "$BACKUP_ID")",
  "restoreId": "$(json_escape "$RESTORE_ID")",
  "backupFile": "$(json_escape "$BACKUP_FILE")",
  "backupCreatedAtUtc": "$(json_escape "$BACKUP_CREATED_AT_UTC")",
  "restoreDatabase": "$(json_escape "$RESTORE_DB")",
  "restoreDatabaseDropped": $RESTORE_DATABASE_DROPPED,
  "tablesFile": "$(json_escape "$TABLES_FILE")",
  "tableCounts": $TABLE_COUNTS_JSON,
  "vectorExtensionCount": $VECTOR_EXTENSION_COUNT,
  "erasureReplay": {
    "status": "$(json_escape "$ERASURE_REPLAY_STATUS")",
    "required": $([[ "$ERASURE_REPLAY_REQUIRED" == "true" ]] && printf 'true' || printf 'false'),
    "payloadSafe": true,
    "ledgerFile": "$(json_escape "$ERASURE_REPLAY_LEDGER_FILE")",
    "ledgerBytes": $ERASURE_REPLAY_LEDGER_BYTES,
    "ledgerSha256": "$(json_escape "$ERASURE_REPLAY_LEDGER_SHA256")",
    "ledgerRecordCount": $ERASURE_REPLAY_LEDGER_RECORDS,
    "actionsNewerThanBackup": $ERASURE_REPLAY_ACTIONS_NEWER_THAN_BACKUP,
    "targetEvents": $ERASURE_REPLAY_TARGET_EVENTS,
    "replayedEvents": $ERASURE_REPLAY_REPLAYED_EVENTS,
    "heldEvents": $ERASURE_REPLAY_HELD_EVENTS,
    "redactedFacts": $ERASURE_REPLAY_REDACTED_FACTS,
    "redactedRoleLenses": $ERASURE_REPLAY_REDACTED_ROLE_LENSES,
    "redactedChunks": $ERASURE_REPLAY_REDACTED_CHUNKS,
    "staleVaultExports": $ERASURE_REPLAY_STALE_VAULT_EXPORTS,
    "clearedReviewNotes": $ERASURE_REPLAY_CLEARED_REVIEW_NOTES,
    "deletedEmbeddings": $ERASURE_REPLAY_DELETED_EMBEDDINGS,
    "validationFailureCount": $ERASURE_REPLAY_FAILURES,
    "targetSetHash": "$(json_escape "$ERASURE_REPLAY_TARGET_SET_HASH")",
    "omittedFields": ["event.content", "memory_facts.object", "memory_chunks.content", "memory_reviews.notes"]
  },
  "startedAtUtc": "$STARTED_AT_UTC",
  "completedAtUtc": "$COMPLETED_AT_UTC",
  "status": "$STATUS",
  "error": "$(json_escape "$ERROR_MESSAGE")"
}
EOF
}

finish() {
  local exit_code=$?

  COMPLETED_AT_UTC="$(date -u +"%Y-%m-%dT%H:%M:%SZ")"
  COMPLETED_AT_SECONDS="$(date -u +%s)"

  if [[ "$created_restore_db" == "true" && "$KEEP_DATABASE" != "true" ]]; then
    dropdb --if-exists "$RESTORE_DB" >/dev/null 2>&1 || true
    RESTORE_DATABASE_DROPPED=true
  fi

  if [[ "$exit_code" -ne 0 && -z "$ERROR_MESSAGE" ]]; then
    ERROR_MESSAGE="restore validation failed"
  fi

  write_metrics || true
  write_evidence || true

  if [[ "$STATUS" == "succeeded" ]]; then
    printf 'Restore validation evidence: %s\n' "$EVIDENCE_FILE"
    printf 'Restore validation metrics: %s\n' "$METRICS_FILE"
  else
    printf 'Restore validation failed; evidence: %s\n' "$EVIDENCE_FILE" >&2
  fi

  exit "$exit_code"
}

trap 'ERROR_MESSAGE="restore validation failed near line $LINENO"' ERR
trap finish EXIT

require_command createdb
require_command dropdb
require_command pg_restore
require_command psql
require_command dotnet
require_value MEMORYSYSTEM_BACKUP_FILE "$BACKUP_FILE"
require_value MEMORYSYSTEM_RESTORE_CONNECTION_STRING "$RESTORE_CONNECTION_STRING"

if [[ ! -f "$BACKUP_FILE" ]]; then
  echo "Backup file not found: $BACKUP_FILE" >&2
  exit 1
fi

printf 'Starting restore validation for %s\n' "$ENVIRONMENT"
pg_restore --list "$BACKUP_FILE" >/dev/null

dropdb --if-exists "$RESTORE_DB" >/dev/null
createdb "$RESTORE_DB"
created_restore_db=true

pg_restore \
  --dbname "$RESTORE_DB" \
  --clean \
  --if-exists \
  --no-owner \
  --no-privileges \
  "$BACKUP_FILE"

run_migrator

validate_erasure_replay
build_table_counts
VECTOR_EXTENSION_COUNT="$(restore_scalar "SELECT count(*) FROM pg_extension WHERE extname = 'vector';")"

if [[ "$VECTOR_EXTENSION_COUNT" != "1" ]]; then
  echo "Restored database is missing pgvector extension." >&2
  exit 1
fi

STATUS="succeeded"
ERROR_MESSAGE=""

printf 'Restore validation complete for database: %s\n' "$RESTORE_DB"
