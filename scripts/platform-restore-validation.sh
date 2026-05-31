#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
. "$ROOT_DIR/scripts/restore-validation-tables.sh"

ENVIRONMENT="${MEMORYSYSTEM_ENVIRONMENT:-local}"
RUN_ID="$(date -u +%Y%m%dT%H%M%SZ)_$$"
BACKUP_FILE="${MEMORYSYSTEM_BACKUP_FILE:-}"
RESTORE_DB="${MEMORYSYSTEM_RESTORE_DATABASE:-memorysystem_restore_validation_$RUN_ID}"
RESTORE_CONNECTION_STRING="${MEMORYSYSTEM_RESTORE_CONNECTION_STRING:-}"
MIGRATIONS_DIR="${MEMORYSYSTEM_MIGRATIONS_DIR:-$ROOT_DIR/migrations}"
MIGRATOR_DLL="${MEMORYSYSTEM_MIGRATOR_DLL:-$ROOT_DIR/migrator/MemorySystem.Migrator.dll}"
EVIDENCE_DIR="${MEMORYSYSTEM_RESTORE_VALIDATION_EVIDENCE_DIR:-/tmp/memorysystem-backup-evidence}"
METRICS_FILE="${MEMORYSYSTEM_RESTORE_VALIDATION_METRICS_FILE:-$EVIDENCE_DIR/restore-validation-metrics.prom}"
EVIDENCE_FILE="${MEMORYSYSTEM_RESTORE_VALIDATION_EVIDENCE_FILE:-$EVIDENCE_DIR/restore-validation-evidence.json}"
TABLES_FILE="${MEMORYSYSTEM_RESTORE_VALIDATION_TABLES_FILE:-$ROOT_DIR/scripts/restore-validation-tables.txt}"
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

write_metrics() {
  local success_value="0"
  local restore_age_seconds="0"
  local escaped_environment
  escaped_environment="$(metric_label_escape "$ENVIRONMENT")"

  if [[ "$STATUS" == "succeeded" ]]; then
    success_value="1"
    restore_age_seconds="0"
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
EOF
}

write_evidence() {
  mkdir -p "$(dirname "$EVIDENCE_FILE")"
  cat >"$EVIDENCE_FILE" <<EOF
{
  "schemaVersion": 1,
  "kind": "memorysystem.restore_validation",
  "environment": "$(json_escape "$ENVIRONMENT")",
  "backupFile": "$(json_escape "$BACKUP_FILE")",
  "restoreDatabase": "$(json_escape "$RESTORE_DB")",
  "restoreDatabaseDropped": $RESTORE_DATABASE_DROPPED,
  "tablesFile": "$(json_escape "$TABLES_FILE")",
  "tableCounts": $TABLE_COUNTS_JSON,
  "vectorExtensionCount": $VECTOR_EXTENSION_COUNT,
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

build_table_counts
VECTOR_EXTENSION_COUNT="$(restore_scalar "SELECT count(*) FROM pg_extension WHERE extname = 'vector';")"

if [[ "$VECTOR_EXTENSION_COUNT" != "1" ]]; then
  echo "Restored database is missing pgvector extension." >&2
  exit 1
fi

STATUS="succeeded"
ERROR_MESSAGE=""

printf 'Restore validation complete for database: %s\n' "$RESTORE_DB"
