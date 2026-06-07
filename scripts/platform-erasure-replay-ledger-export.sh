#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "$ROOT_DIR/scripts/lib/checksums.sh"

ENVIRONMENT="${MEMORYSYSTEM_ENVIRONMENT:-local}"
RUN_ID="$(date -u +%Y%m%dT%H%M%SZ)_$$"
BACKUP_ID="${MEMORYSYSTEM_BACKUP_ID:-}"
EVIDENCE_DIR="${MEMORYSYSTEM_ERASURE_REPLAY_EVIDENCE_DIR:-/tmp/memorysystem-backup-evidence}"
LEDGER_FILE="${MEMORYSYSTEM_ERASURE_REPLAY_LEDGER_FILE:-$EVIDENCE_DIR/erasure-replay-ledger-$RUN_ID.csv}"
METRICS_FILE="${MEMORYSYSTEM_ERASURE_REPLAY_METRICS_FILE:-$EVIDENCE_DIR/erasure-replay-ledger-metrics.prom}"
EVIDENCE_FILE="${MEMORYSYSTEM_ERASURE_REPLAY_EVIDENCE_FILE:-$EVIDENCE_DIR/erasure-replay-ledger-evidence.json}"
STARTED_AT_UTC="$(date -u +"%Y-%m-%dT%H:%M:%SZ")"
STARTED_AT_SECONDS="$(date -u +%s)"
COMPLETED_AT_UTC=""
COMPLETED_AT_SECONDS="$STARTED_AT_SECONDS"
STATUS="failed"
ERROR_MESSAGE=""
LEDGER_BYTES=0
LEDGER_SHA256=""
LEDGER_RECORDS=0

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

write_metrics() {
  local success_value="0"
  local escaped_environment
  escaped_environment="$(metric_label_escape "$ENVIRONMENT")"

  if [[ "$STATUS" == "succeeded" ]]; then
    success_value="1"
  fi

  mkdir -p "$(dirname "$METRICS_FILE")"
  cat >"$METRICS_FILE" <<EOF
# HELP memorysystem_erasure_replay_ledger_export_success Whether the latest erasure replay ledger export completed successfully.
# TYPE memorysystem_erasure_replay_ledger_export_success gauge
memorysystem_erasure_replay_ledger_export_success{environment="$escaped_environment"} $success_value
# HELP memorysystem_erasure_replay_ledger_records Payload-safe redaction records exported for restore-time replay.
# TYPE memorysystem_erasure_replay_ledger_records gauge
memorysystem_erasure_replay_ledger_records{environment="$escaped_environment"} $LEDGER_RECORDS
# HELP memorysystem_erasure_replay_ledger_bytes Size of the latest erasure replay ledger CSV in bytes.
# TYPE memorysystem_erasure_replay_ledger_bytes gauge
memorysystem_erasure_replay_ledger_bytes{environment="$escaped_environment"} $LEDGER_BYTES
# HELP memorysystem_erasure_replay_ledger_timestamp_seconds Unix timestamp for the latest erasure replay ledger export attempt.
# TYPE memorysystem_erasure_replay_ledger_timestamp_seconds gauge
memorysystem_erasure_replay_ledger_timestamp_seconds{environment="$escaped_environment"} $COMPLETED_AT_SECONDS
EOF
}

write_evidence() {
  mkdir -p "$(dirname "$EVIDENCE_FILE")"
  cat >"$EVIDENCE_FILE" <<EOF
{
  "schemaVersion": 1,
  "kind": "memorysystem.erasure_replay_ledger_export",
  "environment": "$(json_escape "$ENVIRONMENT")",
  "backupId": "$(json_escape "$BACKUP_ID")",
  "ledgerFile": "$(json_escape "$LEDGER_FILE")",
  "ledgerBytes": $LEDGER_BYTES,
  "ledgerSha256": "$(json_escape "$LEDGER_SHA256")",
  "recordCount": $LEDGER_RECORDS,
  "payloadSafe": true,
  "omittedFields": ["reason", "event.content", "memory_facts.object", "memory_chunks.content", "memory_reviews.notes"],
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

  if [[ "$exit_code" -ne 0 && -z "$ERROR_MESSAGE" ]]; then
    ERROR_MESSAGE="erasure replay ledger export failed"
  fi

  write_metrics || true
  write_evidence || true

  if [[ "$STATUS" == "succeeded" ]]; then
    printf 'Erasure replay ledger: %s\n' "$LEDGER_FILE"
    printf 'Erasure replay ledger evidence: %s\n' "$EVIDENCE_FILE"
    printf 'Erasure replay ledger metrics: %s\n' "$METRICS_FILE"
  else
    printf 'Erasure replay ledger export failed; evidence: %s\n' "$EVIDENCE_FILE" >&2
  fi

  exit "$exit_code"
}

trap 'ERROR_MESSAGE="erasure replay ledger export failed near line $LINENO"' ERR
trap finish EXIT

postgres_args=()
if [[ -n "${MEMORYSYSTEM_POSTGRES_URL:-}" ]]; then
  postgres_args=("$MEMORYSYSTEM_POSTGRES_URL")
fi

require_command psql

mkdir -p "$(dirname "$LEDGER_FILE")"

printf 'Exporting payload-safe erasure replay ledger for %s\n' "$ENVIRONMENT"
if [[ "${#postgres_args[@]}" -gt 0 ]]; then
  psql "${postgres_args[@]}" -v ON_ERROR_STOP=1 -q -c "
COPY (
  SELECT
    to_char(redaction.created_at AT TIME ZONE 'UTC', 'YYYY-MM-DD\"T\"HH24:MI:SS\"Z\"') AS redaction_created_at_utc,
    redaction.target_type,
    redaction.target_id::text,
    redaction.source_event_id::text AS redaction_event_id,
    COALESCE(redaction.requested_by_principal_id::text, '') AS requested_by_principal_id,
    redaction.redaction_type
  FROM memory_redactions AS redaction
  WHERE redaction.target_type IN ('event', 'memory_fact')
  ORDER BY redaction.created_at, redaction.target_type, redaction.target_id
) TO STDOUT WITH (FORMAT csv, HEADER true);
" >"$LEDGER_FILE"
else
  psql -v ON_ERROR_STOP=1 -q -c "
COPY (
  SELECT
    to_char(redaction.created_at AT TIME ZONE 'UTC', 'YYYY-MM-DD\"T\"HH24:MI:SS\"Z\"') AS redaction_created_at_utc,
    redaction.target_type,
    redaction.target_id::text,
    redaction.source_event_id::text AS redaction_event_id,
    COALESCE(redaction.requested_by_principal_id::text, '') AS requested_by_principal_id,
    redaction.redaction_type
  FROM memory_redactions AS redaction
  WHERE redaction.target_type IN ('event', 'memory_fact')
  ORDER BY redaction.created_at, redaction.target_type, redaction.target_id
) TO STDOUT WITH (FORMAT csv, HEADER true);
" >"$LEDGER_FILE"
fi

LEDGER_BYTES="$(wc -c <"$LEDGER_FILE" | tr -d ' ')"
LEDGER_SHA256="$(sha256_file "$LEDGER_FILE")"
LEDGER_RECORDS="$(awk 'NR > 1 { count++ } END { print count + 0 }' "$LEDGER_FILE")"
STATUS="succeeded"
ERROR_MESSAGE=""

printf 'Erasure replay ledger export complete: %s records\n' "$LEDGER_RECORDS"
