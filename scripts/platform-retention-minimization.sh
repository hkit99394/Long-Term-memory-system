#!/usr/bin/env bash
set -euo pipefail

ENVIRONMENT="${MEMORYSYSTEM_ENVIRONMENT:-local}"
RUN_ID="$(date -u +%Y%m%dT%H%M%SZ)_$$"
MODE="${MEMORYSYSTEM_RETENTION_MINIMIZATION_MODE:-dry-run}"
AS_OF_UTC="${MEMORYSYSTEM_RETENTION_MINIMIZATION_AS_OF_UTC:-$(date -u +"%Y-%m-%dT%H:%M:%SZ")}"
STANDARD_MAX_AGE_DAYS="${MEMORYSYSTEM_RETENTION_STANDARD_MAX_AGE_DAYS:-90}"
AUDIT_MAX_AGE_DAYS="${MEMORYSYSTEM_RETENTION_AUDIT_MAX_AGE_DAYS:-365}"
BATCH_SIZE="${MEMORYSYSTEM_RETENTION_MINIMIZATION_BATCH_SIZE:-500}"
EVIDENCE_DIR="${MEMORYSYSTEM_RETENTION_MINIMIZATION_EVIDENCE_DIR:-/tmp/memorysystem-governance-evidence}"
METRICS_FILE="${MEMORYSYSTEM_RETENTION_MINIMIZATION_METRICS_FILE:-$EVIDENCE_DIR/retention-minimization-metrics.prom}"
EVIDENCE_FILE="${MEMORYSYSTEM_RETENTION_MINIMIZATION_EVIDENCE_FILE:-$EVIDENCE_DIR/retention-minimization-evidence.json}"
STARTED_AT_UTC="$(date -u +"%Y-%m-%dT%H:%M:%SZ")"
STARTED_AT_SECONDS="$(date -u +%s)"
COMPLETED_AT_UTC=""
COMPLETED_AT_SECONDS="$STARTED_AT_SECONDS"
STATUS="failed"
ERROR_MESSAGE=""
SUCCESS_VALUE=0
EXECUTE_MODE=false
CANDIDATE_EVENTS=0
STANDARD_CANDIDATE_EVENTS=0
AUDIT_CANDIDATE_EVENTS=0
MINIMIZED_EVENTS=0
REVIEW_NOTES_CLEARED=0
LEGAL_HOLD_SKIPPED_EVENTS=0
EXTERNAL_PAYLOAD_SKIPPED_EVENTS=0
VALIDATION_FAILURES=0
TARGET_SET_HASH=""

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

require_positive_int() {
  local name="$1"
  local value="$2"

  if [[ ! "$value" =~ ^[1-9][0-9]*$ ]]; then
    echo "$name must be a positive integer." >&2
    exit 1
  fi
}

write_metrics() {
  local escaped_environment
  local escaped_mode
  escaped_environment="$(metric_label_escape "$ENVIRONMENT")"
  escaped_mode="$(metric_label_escape "$MODE")"

  mkdir -p "$(dirname "$METRICS_FILE")"
  cat >"$METRICS_FILE" <<EOF
# HELP memorysystem_retention_minimization_success Whether the latest standard/audit retention minimization job completed successfully.
# TYPE memorysystem_retention_minimization_success gauge
memorysystem_retention_minimization_success{environment="$escaped_environment",mode="$escaped_mode"} $SUCCESS_VALUE
# HELP memorysystem_retention_minimization_candidates Candidate source events selected for standard/audit payload minimization.
# TYPE memorysystem_retention_minimization_candidates gauge
memorysystem_retention_minimization_candidates{environment="$escaped_environment",mode="$escaped_mode"} $CANDIDATE_EVENTS
# HELP memorysystem_retention_minimization_minimized_events Source event payloads minimized by the latest run.
# TYPE memorysystem_retention_minimization_minimized_events gauge
memorysystem_retention_minimization_minimized_events{environment="$escaped_environment",mode="$escaped_mode"} $MINIMIZED_EVENTS
# HELP memorysystem_retention_minimization_review_notes_cleared Review note payload copies cleared by the latest run.
# TYPE memorysystem_retention_minimization_review_notes_cleared gauge
memorysystem_retention_minimization_review_notes_cleared{environment="$escaped_environment",mode="$escaped_mode"} $REVIEW_NOTES_CLEARED
# HELP memorysystem_retention_minimization_legal_hold_skipped Source events skipped because an active legal hold applies.
# TYPE memorysystem_retention_minimization_legal_hold_skipped gauge
memorysystem_retention_minimization_legal_hold_skipped{environment="$escaped_environment",mode="$escaped_mode"} $LEGAL_HOLD_SKIPPED_EVENTS
# HELP memorysystem_retention_minimization_external_payload_skipped Source events skipped until external payload-store checks are available.
# TYPE memorysystem_retention_minimization_external_payload_skipped gauge
memorysystem_retention_minimization_external_payload_skipped{environment="$escaped_environment",mode="$escaped_mode"} $EXTERNAL_PAYLOAD_SKIPPED_EVENTS
# HELP memorysystem_retention_minimization_failures Rows that still expose target payload copies after execute-mode validation.
# TYPE memorysystem_retention_minimization_failures gauge
memorysystem_retention_minimization_failures{environment="$escaped_environment",mode="$escaped_mode"} $VALIDATION_FAILURES
# HELP memorysystem_retention_minimization_timestamp_seconds Unix timestamp for the latest retention minimization attempt.
# TYPE memorysystem_retention_minimization_timestamp_seconds gauge
memorysystem_retention_minimization_timestamp_seconds{environment="$escaped_environment",mode="$escaped_mode"} $COMPLETED_AT_SECONDS
EOF
}

write_evidence() {
  mkdir -p "$(dirname "$EVIDENCE_FILE")"
  cat >"$EVIDENCE_FILE" <<EOF
{
  "schemaVersion": 1,
  "kind": "memorysystem.retention_minimization",
  "runId": "$(json_escape "$RUN_ID")",
  "environment": "$(json_escape "$ENVIRONMENT")",
  "mode": "$(json_escape "$MODE")",
  "payloadSafe": true,
  "asOfUtc": "$(json_escape "$AS_OF_UTC")",
  "standardMaxAgeDays": $STANDARD_MAX_AGE_DAYS,
  "auditMaxAgeDays": $AUDIT_MAX_AGE_DAYS,
  "batchSize": $BATCH_SIZE,
  "candidateEvents": $CANDIDATE_EVENTS,
  "standardCandidateEvents": $STANDARD_CANDIDATE_EVENTS,
  "auditCandidateEvents": $AUDIT_CANDIDATE_EVENTS,
  "minimizedEvents": $MINIMIZED_EVENTS,
  "reviewNotesCleared": $REVIEW_NOTES_CLEARED,
  "legalHoldSkippedEvents": $LEGAL_HOLD_SKIPPED_EVENTS,
  "externalPayloadSkippedEvents": $EXTERNAL_PAYLOAD_SKIPPED_EVENTS,
  "validationFailureCount": $VALIDATION_FAILURES,
  "targetSetHash": "$(json_escape "$TARGET_SET_HASH")",
  "preservedDerivedCopies": ["memory_facts", "role_memory_lenses", "memory_chunks", "memory_embeddings", "vault_exports"],
  "clearedDerivedCopies": ["memory_reviews.notes"],
  "omittedFields": ["events.content", "memory_facts.object", "memory_chunks.content", "memory_reviews.notes", "vault_exports.body"],
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
    ERROR_MESSAGE="retention minimization failed"
  fi

  write_metrics || true
  write_evidence || true

  if [[ "$STATUS" == "succeeded" ]]; then
    printf 'Retention minimization evidence: %s\n' "$EVIDENCE_FILE"
    printf 'Retention minimization metrics: %s\n' "$METRICS_FILE"
  else
    printf 'Retention minimization failed; evidence: %s\n' "$EVIDENCE_FILE" >&2
  fi

  exit "$exit_code"
}

trap 'ERROR_MESSAGE="retention minimization failed near line $LINENO"' ERR
trap finish EXIT

case "$MODE" in
  dry-run)
    EXECUTE_MODE=false
    ;;
  execute)
    EXECUTE_MODE=true
    ;;
  *)
    echo "MEMORYSYSTEM_RETENTION_MINIMIZATION_MODE must be dry-run or execute." >&2
    exit 1
    ;;
esac

require_positive_int MEMORYSYSTEM_RETENTION_STANDARD_MAX_AGE_DAYS "$STANDARD_MAX_AGE_DAYS"
require_positive_int MEMORYSYSTEM_RETENTION_AUDIT_MAX_AGE_DAYS "$AUDIT_MAX_AGE_DAYS"
require_positive_int MEMORYSYSTEM_RETENTION_MINIMIZATION_BATCH_SIZE "$BATCH_SIZE"
require_command psql

postgres_args=()
if [[ -n "${MEMORYSYSTEM_POSTGRES_URL:-}" ]]; then
  postgres_args=("$MEMORYSYSTEM_POSTGRES_URL")
fi

printf 'Starting retention minimization for %s in %s mode\n' "$ENVIRONMENT" "$MODE"

result="$(
  psql "${postgres_args[@]}" -qAt -F $'\t' -v ON_ERROR_STOP=1 \
    -v execute_mode="$EXECUTE_MODE" \
    -v as_of_utc="$AS_OF_UTC" \
    -v standard_max_age_days="$STANDARD_MAX_AGE_DAYS" \
    -v audit_max_age_days="$AUDIT_MAX_AGE_DAYS" \
    -v batch_size="$BATCH_SIZE" <<'SQL' | tail -n 1
BEGIN;

CREATE TEMP TABLE retention_minimization_scope AS
SELECT
    event.id,
    event.retention_class,
    event.created_at,
    event.event_type,
    event.sensitivity,
    event.external_payload_uri
FROM events AS event
WHERE event.retention_class IN ('standard', 'audit')
    AND event.redaction_status = 'none'
    AND COALESCE(event.content->>'minimized', 'false') <> 'true'
    AND (
        (
            event.retention_class = 'standard'
            AND event.created_at <= :'as_of_utc'::timestamptz - (:standard_max_age_days::int * interval '1 day')
        )
        OR (
            event.retention_class = 'audit'
            AND event.created_at <= :'as_of_utc'::timestamptz - (:audit_max_age_days::int * interval '1 day')
        )
    );

CREATE TEMP TABLE retention_minimization_held_scope AS
SELECT DISTINCT
    event.id,
    link.original_retention_class AS retention_class,
    event.created_at,
    event.event_type,
    event.sensitivity,
    event.external_payload_uri
FROM governance_legal_hold_events AS link
INNER JOIN governance_legal_holds AS hold
    ON hold.id = link.legal_hold_id
    AND hold.status = 'active'
INNER JOIN events AS event
    ON event.id = link.event_id
WHERE link.released_at IS NULL
    AND link.original_retention_class IN ('standard', 'audit')
    AND COALESCE(event.content->>'minimized', 'false') <> 'true'
    AND (
        (
            link.original_retention_class = 'standard'
            AND event.created_at <= :'as_of_utc'::timestamptz - (:standard_max_age_days::int * interval '1 day')
        )
        OR (
            link.original_retention_class = 'audit'
            AND event.created_at <= :'as_of_utc'::timestamptz - (:audit_max_age_days::int * interval '1 day')
        )
    );

CREATE TEMP TABLE retention_minimization_external_payload_skips AS
SELECT scope.id
FROM retention_minimization_scope AS scope
WHERE scope.external_payload_uri IS NOT NULL
    AND NOT EXISTS (
        SELECT 1
        FROM governance_legal_hold_events AS link
        INNER JOIN governance_legal_holds AS hold
            ON hold.id = link.legal_hold_id
            AND hold.status = 'active'
        WHERE link.event_id = scope.id
            AND link.released_at IS NULL
    );

CREATE TEMP TABLE retention_minimization_candidates AS
SELECT scope.*
FROM retention_minimization_scope AS scope
WHERE scope.external_payload_uri IS NULL
    AND NOT EXISTS (
        SELECT 1
        FROM governance_legal_hold_events AS link
        INNER JOIN governance_legal_holds AS hold
            ON hold.id = link.legal_hold_id
            AND hold.status = 'active'
        WHERE link.event_id = scope.id
            AND link.released_at IS NULL
    )
ORDER BY scope.created_at, scope.id
LIMIT :batch_size;

CREATE TEMP TABLE retention_minimization_review_notes AS
SELECT review.id
FROM memory_reviews AS review
INNER JOIN retention_minimization_candidates AS candidate
    ON candidate.id = review.source_event_id
WHERE review.notes IS NOT NULL;

CREATE TEMP TABLE retention_minimization_update_counts (
    minimized_events integer NOT NULL DEFAULT 0,
    review_notes_cleared integer NOT NULL DEFAULT 0
);

INSERT INTO retention_minimization_update_counts DEFAULT VALUES;

\if :execute_mode
WITH cleared_reviews AS (
    UPDATE memory_reviews AS review
    SET notes = NULL
    FROM retention_minimization_review_notes AS target
    WHERE review.id = target.id
    RETURNING review.id
)
UPDATE retention_minimization_update_counts
SET review_notes_cleared = (SELECT count(*) FROM cleared_reviews);

WITH minimized_events AS (
    UPDATE events AS event
    SET content = jsonb_build_object(
            'minimized', true,
            'reason', CASE candidate.retention_class
                WHEN 'audit' THEN 'audit_retention_expired'
                ELSE 'standard_retention_expired'
            END,
            'message', 'Raw source event payload minimized after the retention window.',
            'retentionClass', candidate.retention_class,
            'eventType', candidate.event_type,
            'sensitivity', candidate.sensitivity,
            'minimizedAtUtc', :'as_of_utc'
        ),
        external_payload_uri = NULL
    FROM retention_minimization_candidates AS candidate
    WHERE event.id = candidate.id
    RETURNING event.id
)
UPDATE retention_minimization_update_counts
SET minimized_events = (SELECT count(*) FROM minimized_events);

CREATE TEMP TABLE retention_minimization_failures AS
SELECT 'event' AS target_type, event.id AS target_id
FROM retention_minimization_candidates AS candidate
INNER JOIN events AS event
    ON event.id = candidate.id
WHERE event.content->>'minimized' <> 'true'
    OR event.content ? 'payload'
    OR event.external_payload_uri IS NOT NULL

UNION ALL

SELECT 'memory_review', review.id
FROM memory_reviews AS review
WHERE review.id IN (SELECT id FROM retention_minimization_review_notes)
    AND review.notes IS NOT NULL;
\else
CREATE TEMP TABLE retention_minimization_failures (
    target_type text,
    target_id uuid
);
\endif

SELECT concat_ws(
    E'\t',
    (SELECT count(*) FROM retention_minimization_candidates)::text,
    (SELECT count(*) FROM retention_minimization_candidates WHERE retention_class = 'standard')::text,
    (SELECT count(*) FROM retention_minimization_candidates WHERE retention_class = 'audit')::text,
    (SELECT minimized_events FROM retention_minimization_update_counts)::text,
    (SELECT review_notes_cleared FROM retention_minimization_update_counts)::text,
    (SELECT count(*) FROM retention_minimization_held_scope)::text,
    (SELECT count(*) FROM retention_minimization_external_payload_skips)::text,
    (SELECT count(*) FROM retention_minimization_failures)::text,
    COALESCE(
        'md5:' || (
            SELECT md5(string_agg(candidate.id::text, ',' ORDER BY candidate.id::text))
            FROM retention_minimization_candidates AS candidate
        ),
        ''
    )
);

COMMIT;
SQL
)"

IFS=$'\t' read -r \
  CANDIDATE_EVENTS \
  STANDARD_CANDIDATE_EVENTS \
  AUDIT_CANDIDATE_EVENTS \
  MINIMIZED_EVENTS \
  REVIEW_NOTES_CLEARED \
  LEGAL_HOLD_SKIPPED_EVENTS \
  EXTERNAL_PAYLOAD_SKIPPED_EVENTS \
  VALIDATION_FAILURES \
  TARGET_SET_HASH <<<"$result"

if [[ "$VALIDATION_FAILURES" != "0" ]]; then
  ERROR_MESSAGE="retention minimization validation failed"
  echo "Retention minimization validation failed with $VALIDATION_FAILURES failed rows." >&2
  exit 1
fi

STATUS="succeeded"
SUCCESS_VALUE=1
ERROR_MESSAGE=""

printf 'Retention minimization complete: candidates=%s minimized=%s reviewNotesCleared=%s\n' \
  "$CANDIDATE_EVENTS" \
  "$MINIMIZED_EVENTS" \
  "$REVIEW_NOTES_CLEARED"
