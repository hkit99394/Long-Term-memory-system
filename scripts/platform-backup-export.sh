#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "$ROOT_DIR/scripts/lib/checksums.sh"

ENVIRONMENT="${MEMORYSYSTEM_ENVIRONMENT:-local}"
RUN_ID="$(date -u +%Y%m%dT%H%M%SZ)_$$"
BACKUP_ID="${MEMORYSYSTEM_BACKUP_ID:-memorysystem-$ENVIRONMENT-$RUN_ID}"
EVIDENCE_DIR="${MEMORYSYSTEM_BACKUP_EVIDENCE_DIR:-/tmp/memorysystem-backup-evidence}"
BACKUP_FILE="${MEMORYSYSTEM_BACKUP_FILE:-$EVIDENCE_DIR/$BACKUP_ID.dump}"
METRICS_FILE="${MEMORYSYSTEM_BACKUP_METRICS_FILE:-$EVIDENCE_DIR/backup-metrics.prom}"
EVIDENCE_FILE="${MEMORYSYSTEM_BACKUP_EVIDENCE_FILE:-$EVIDENCE_DIR/backup-export-evidence.json}"
RELEASE_EVIDENCE_BUCKET="${MEMORYSYSTEM_RELEASE_EVIDENCE_BUCKET:-}"
STARTED_AT_UTC="$(date -u +"%Y-%m-%dT%H:%M:%SZ")"
STARTED_AT_SECONDS="$(date -u +%s)"
COMPLETED_AT_UTC=""
COMPLETED_AT_SECONDS="$STARTED_AT_SECONDS"
STATUS="failed"
ERROR_MESSAGE=""
BACKUP_BYTES=0
BACKUP_SHA256=""

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

write_metrics() {
  local success_value="0"
  local backup_age_seconds="0"
  local escaped_environment
  escaped_environment="$(metric_label_escape "$ENVIRONMENT")"

  if [[ "$STATUS" == "succeeded" ]]; then
    success_value="1"
    backup_age_seconds="0"
  fi

  mkdir -p "$(dirname "$METRICS_FILE")"
  cat >"$METRICS_FILE" <<EOF
# HELP memorysystem_backup_export_success Whether the latest platform backup export completed successfully.
# TYPE memorysystem_backup_export_success gauge
memorysystem_backup_export_success{environment="$escaped_environment"} $success_value
# HELP memorysystem_backup_age_seconds Age of the latest successful platform backup export in seconds.
# TYPE memorysystem_backup_age_seconds gauge
memorysystem_backup_age_seconds{environment="$escaped_environment"} $backup_age_seconds
# HELP memorysystem_backup_export_timestamp_seconds Unix timestamp for the latest platform backup export attempt.
# TYPE memorysystem_backup_export_timestamp_seconds gauge
memorysystem_backup_export_timestamp_seconds{environment="$escaped_environment"} $COMPLETED_AT_SECONDS
# HELP memorysystem_backup_export_bytes Size of the latest platform backup export in bytes.
# TYPE memorysystem_backup_export_bytes gauge
memorysystem_backup_export_bytes{environment="$escaped_environment"} $BACKUP_BYTES
EOF
}

write_evidence() {
  mkdir -p "$(dirname "$EVIDENCE_FILE")"
  cat >"$EVIDENCE_FILE" <<EOF
{
  "schemaVersion": 1,
  "kind": "memorysystem.backup_export",
  "environment": "$(json_escape "$ENVIRONMENT")",
  "backupId": "$(json_escape "$BACKUP_ID")",
  "backupFile": "$(json_escape "$BACKUP_FILE")",
  "backupBytes": $BACKUP_BYTES,
  "backupSha256": "$(json_escape "$BACKUP_SHA256")",
  "releaseEvidenceBucket": "$(json_escape "$RELEASE_EVIDENCE_BUCKET")",
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
    ERROR_MESSAGE="backup export failed"
  fi

  write_metrics || true
  write_evidence || true

  if [[ "$STATUS" == "succeeded" ]]; then
    printf 'Backup export evidence: %s\n' "$EVIDENCE_FILE"
    printf 'Backup export metrics: %s\n' "$METRICS_FILE"
  else
    printf 'Backup export failed; evidence: %s\n' "$EVIDENCE_FILE" >&2
  fi

  exit "$exit_code"
}

trap 'ERROR_MESSAGE="backup export failed near line $LINENO"' ERR
trap finish EXIT

require_command() {
  local name="$1"

  if ! command -v "$name" >/dev/null 2>&1; then
    echo "Required command not found: $name" >&2
    exit 1
  fi
}

postgres_args=()
if [[ -n "${MEMORYSYSTEM_POSTGRES_URL:-}" ]]; then
  postgres_args=("$MEMORYSYSTEM_POSTGRES_URL")
fi

require_command pg_dump
require_command pg_restore

mkdir -p "$(dirname "$BACKUP_FILE")"

printf 'Starting backup export for %s\n' "$ENVIRONMENT"
if [[ "${#postgres_args[@]}" -gt 0 ]]; then
  pg_dump "${postgres_args[@]}" \
    --format=custom \
    --blobs \
    --no-owner \
    --no-privileges \
    --file "$BACKUP_FILE"
else
  pg_dump \
    --format=custom \
    --blobs \
    --no-owner \
    --no-privileges \
    --file "$BACKUP_FILE"
fi

pg_restore --list "$BACKUP_FILE" >/dev/null

BACKUP_BYTES="$(wc -c <"$BACKUP_FILE" | tr -d ' ')"
BACKUP_SHA256="$(sha256_file "$BACKUP_FILE")"
STATUS="succeeded"
ERROR_MESSAGE=""

printf 'Backup export complete: %s bytes\n' "$BACKUP_BYTES"
