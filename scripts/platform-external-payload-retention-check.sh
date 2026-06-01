#!/usr/bin/env bash
set -euo pipefail

ENVIRONMENT="${MEMORYSYSTEM_ENVIRONMENT:-local}"
RUN_ID="$(date -u +%Y%m%dT%H%M%SZ)_$$"
MODE="${MEMORYSYSTEM_EXTERNAL_PAYLOAD_CHECK_MODE:-audit-only}"
POLICY_MODE="${MEMORYSYSTEM_EXTERNAL_PAYLOAD_POLICY_MODE:-disabled}"
ALLOWED_SCHEMES="${MEMORYSYSTEM_EXTERNAL_PAYLOAD_ALLOWED_SCHEMES:-}"
AS_OF_UTC="${MEMORYSYSTEM_EXTERNAL_PAYLOAD_CHECK_AS_OF_UTC:-$(date -u +"%Y-%m-%dT%H:%M:%SZ")}"
EPHEMERAL_MAX_AGE_DAYS="${MEMORYSYSTEM_EXTERNAL_PAYLOAD_EPHEMERAL_MAX_AGE_DAYS:-7}"
STANDARD_MAX_AGE_DAYS="${MEMORYSYSTEM_EXTERNAL_PAYLOAD_STANDARD_MAX_AGE_DAYS:-90}"
AUDIT_MAX_AGE_DAYS="${MEMORYSYSTEM_EXTERNAL_PAYLOAD_AUDIT_MAX_AGE_DAYS:-365}"
BATCH_SIZE="${MEMORYSYSTEM_EXTERNAL_PAYLOAD_CHECK_BATCH_SIZE:-500}"
EVIDENCE_DIR="${MEMORYSYSTEM_EXTERNAL_PAYLOAD_EVIDENCE_DIR:-/tmp/memorysystem-governance-evidence}"
METRICS_FILE="${MEMORYSYSTEM_EXTERNAL_PAYLOAD_METRICS_FILE:-$EVIDENCE_DIR/external-payload-retention-metrics.prom}"
EVIDENCE_FILE="${MEMORYSYSTEM_EXTERNAL_PAYLOAD_EVIDENCE_FILE:-$EVIDENCE_DIR/external-payload-retention-evidence.json}"
STARTED_AT_UTC="$(date -u +"%Y-%m-%dT%H:%M:%SZ")"
COMPLETED_AT_UTC=""
COMPLETED_AT_SECONDS="$(date -u +%s)"
STATUS="failed"
ERROR_MESSAGE=""
SUCCESS_VALUE=0
TARGETS_FILE=""
TARGET_EVENTS=0
EXPECTED_PRESENT=0
EXPECTED_ABSENT=0
VERIFIED_PRESENT=0
VERIFIED_ABSENT=0
UNVERIFIED_TARGETS=0
POLICY_VIOLATIONS=0
UNSUPPORTED_SCHEME=0
STATE_MISMATCHES=0
PROBE_FAILURES=0
FAILURES=0
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

json_string_array_from_csv() {
  local raw="$1"
  local first=true
  local value

  printf '['
  IFS=',' read -r -a values <<<"$raw"
  for value in "${values[@]}"; do
    value="${value#"${value%%[![:space:]]*}"}"
    value="${value%"${value##*[![:space:]]}"}"
    if [[ -z "$value" ]]; then
      continue
    fi

    if [[ "$first" == true ]]; then
      first=false
    else
      printf ', '
    fi

    printf '"%s"' "$(json_escape "${value,,}")"
  done
  printf ']'
}

sha256_stream() {
  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum | awk '{print $1}'
    return
  fi

  shasum -a 256 | awk '{print $1}'
}

is_scheme_allowed() {
  local scheme="${1,,}"
  local allowed

  IFS=',' read -r -a values <<<"$ALLOWED_SCHEMES"
  for allowed in "${values[@]}"; do
    allowed="${allowed#"${allowed%%[![:space:]]*}"}"
    allowed="${allowed%"${allowed##*[![:space:]]}"}"
    if [[ "${allowed,,}" == "$scheme" ]]; then
      return 0
    fi
  done

  return 1
}

probe_file_uri() {
  local uri="$1"
  local path="${uri#file://}"

  if [[ "$path" == "$uri" || -z "$path" ]]; then
    printf 'unsupported'
    return
  fi

  if [[ -e "$path" ]]; then
    printf 'present'
  else
    printf 'absent'
  fi
}

probe_s3_uri() {
  local uri="$1"
  local rest="${uri#s3://}"
  local bucket
  local key
  local error_file

  if [[ "$rest" == "$uri" ]]; then
    printf 'unsupported'
    return
  fi

  bucket="${rest%%/*}"
  key="${rest#*/}"
  if [[ -z "$bucket" || -z "$key" || "$bucket" == "$rest" ]]; then
    printf 'unsupported'
    return
  fi

  if ! command -v aws >/dev/null 2>&1; then
    printf 'unsupported'
    return
  fi

  error_file="$(mktemp "${TMPDIR:-/tmp}/memorysystem-s3-head.XXXXXX")"
  if aws s3api head-object --bucket "$bucket" --key "$key" >/dev/null 2>"$error_file"; then
    rm -f "$error_file"
    printf 'present'
    return
  fi

  if grep -Eiq 'NoSuchKey|Not[[:space:]]*Found|404' "$error_file"; then
    rm -f "$error_file"
    printf 'absent'
    return
  fi

  rm -f "$error_file"
  printf 'unknown'
}

probe_external_payload() {
  local scheme="${1,,}"
  local uri="$2"

  case "$scheme" in
    file)
      probe_file_uri "$uri"
      ;;
    s3)
      probe_s3_uri "$uri"
      ;;
    *)
      printf 'unsupported'
      ;;
  esac
}

write_metrics() {
  local escaped_environment
  local escaped_mode
  local escaped_policy_mode
  escaped_environment="$(metric_label_escape "$ENVIRONMENT")"
  escaped_mode="$(metric_label_escape "$MODE")"
  escaped_policy_mode="$(metric_label_escape "$POLICY_MODE")"

  mkdir -p "$(dirname "$METRICS_FILE")"
  cat >"$METRICS_FILE" <<EOF
# HELP memorysystem_external_payload_retention_check_success Whether the latest external payload retention check completed successfully.
# TYPE memorysystem_external_payload_retention_check_success gauge
memorysystem_external_payload_retention_check_success{environment="$escaped_environment",mode="$escaped_mode",policy_mode="$escaped_policy_mode"} $SUCCESS_VALUE
# HELP memorysystem_external_payload_retention_check_targets Source events with external payload pointers inspected by the latest check.
# TYPE memorysystem_external_payload_retention_check_targets gauge
memorysystem_external_payload_retention_check_targets{environment="$escaped_environment",mode="$escaped_mode",policy_mode="$escaped_policy_mode"} $TARGET_EVENTS
# HELP memorysystem_external_payload_retention_expected_present External payload objects expected to still exist.
# TYPE memorysystem_external_payload_retention_expected_present gauge
memorysystem_external_payload_retention_expected_present{environment="$escaped_environment",mode="$escaped_mode",policy_mode="$escaped_policy_mode"} $EXPECTED_PRESENT
# HELP memorysystem_external_payload_retention_expected_absent External payload objects expected to be absent because retention, minimization, or erasure state requires it.
# TYPE memorysystem_external_payload_retention_expected_absent gauge
memorysystem_external_payload_retention_expected_absent{environment="$escaped_environment",mode="$escaped_mode",policy_mode="$escaped_policy_mode"} $EXPECTED_ABSENT
# HELP memorysystem_external_payload_retention_verified_present Provider probes that confirmed expected-present objects exist.
# TYPE memorysystem_external_payload_retention_verified_present gauge
memorysystem_external_payload_retention_verified_present{environment="$escaped_environment",mode="$escaped_mode",policy_mode="$escaped_policy_mode"} $VERIFIED_PRESENT
# HELP memorysystem_external_payload_retention_verified_absent Provider probes that confirmed expected-absent objects are gone.
# TYPE memorysystem_external_payload_retention_verified_absent gauge
memorysystem_external_payload_retention_verified_absent{environment="$escaped_environment",mode="$escaped_mode",policy_mode="$escaped_policy_mode"} $VERIFIED_ABSENT
# HELP memorysystem_external_payload_retention_unverified_targets External payload pointers not provider-probed by the latest run.
# TYPE memorysystem_external_payload_retention_unverified_targets gauge
memorysystem_external_payload_retention_unverified_targets{environment="$escaped_environment",mode="$escaped_mode",policy_mode="$escaped_policy_mode"} $UNVERIFIED_TARGETS
# HELP memorysystem_external_payload_retention_policy_violations External payload pointers rejected by policy mode or allowed-scheme configuration.
# TYPE memorysystem_external_payload_retention_policy_violations gauge
memorysystem_external_payload_retention_policy_violations{environment="$escaped_environment",mode="$escaped_mode",policy_mode="$escaped_policy_mode"} $POLICY_VIOLATIONS
# HELP memorysystem_external_payload_retention_unsupported_scheme External payload pointers using a scheme that this check cannot probe.
# TYPE memorysystem_external_payload_retention_unsupported_scheme gauge
memorysystem_external_payload_retention_unsupported_scheme{environment="$escaped_environment",mode="$escaped_mode",policy_mode="$escaped_policy_mode"} $UNSUPPORTED_SCHEME
# HELP memorysystem_external_payload_retention_mismatches Provider probes where actual object state did not match expected retention state.
# TYPE memorysystem_external_payload_retention_mismatches gauge
memorysystem_external_payload_retention_mismatches{environment="$escaped_environment",mode="$escaped_mode",policy_mode="$escaped_policy_mode"} $STATE_MISMATCHES
# HELP memorysystem_external_payload_retention_probe_failures Provider probes that could not determine object state.
# TYPE memorysystem_external_payload_retention_probe_failures gauge
memorysystem_external_payload_retention_probe_failures{environment="$escaped_environment",mode="$escaped_mode",policy_mode="$escaped_policy_mode"} $PROBE_FAILURES
# HELP memorysystem_external_payload_retention_failures Total policy, provider, probe, and state-mismatch failures in the latest run.
# TYPE memorysystem_external_payload_retention_failures gauge
memorysystem_external_payload_retention_failures{environment="$escaped_environment",mode="$escaped_mode",policy_mode="$escaped_policy_mode"} $FAILURES
# HELP memorysystem_external_payload_retention_timestamp_seconds Unix timestamp for the latest external payload retention check attempt.
# TYPE memorysystem_external_payload_retention_timestamp_seconds gauge
memorysystem_external_payload_retention_timestamp_seconds{environment="$escaped_environment",mode="$escaped_mode",policy_mode="$escaped_policy_mode"} $COMPLETED_AT_SECONDS
EOF
}

write_evidence() {
  local allowed_schemes_json
  allowed_schemes_json="$(json_string_array_from_csv "$ALLOWED_SCHEMES")"

  mkdir -p "$(dirname "$EVIDENCE_FILE")"
  cat >"$EVIDENCE_FILE" <<EOF
{
  "schemaVersion": 1,
  "kind": "memorysystem.external_payload_retention_check",
  "runId": "$(json_escape "$RUN_ID")",
  "environment": "$(json_escape "$ENVIRONMENT")",
  "mode": "$(json_escape "$MODE")",
  "policyMode": "$(json_escape "$POLICY_MODE")",
  "allowedSchemes": $allowed_schemes_json,
  "payloadSafe": true,
  "asOfUtc": "$(json_escape "$AS_OF_UTC")",
  "ephemeralMaxAgeDays": $EPHEMERAL_MAX_AGE_DAYS,
  "standardMaxAgeDays": $STANDARD_MAX_AGE_DAYS,
  "auditMaxAgeDays": $AUDIT_MAX_AGE_DAYS,
  "batchSize": $BATCH_SIZE,
  "targetEvents": $TARGET_EVENTS,
  "expectedPresent": $EXPECTED_PRESENT,
  "expectedAbsent": $EXPECTED_ABSENT,
  "verifiedPresent": $VERIFIED_PRESENT,
  "verifiedAbsent": $VERIFIED_ABSENT,
  "unverifiedTargets": $UNVERIFIED_TARGETS,
  "policyViolations": $POLICY_VIOLATIONS,
  "unsupportedScheme": $UNSUPPORTED_SCHEME,
  "stateMismatches": $STATE_MISMATCHES,
  "probeFailures": $PROBE_FAILURES,
  "failureCount": $FAILURES,
  "targetSetHash": "$(json_escape "$TARGET_SET_HASH")",
  "providerChecks": ["file", "s3"],
  "checkSemantics": [
    "active legal holds expect external payload objects to remain present",
    "live unexpired source events expect external payload objects to remain present",
    "erased, redacted, minimized, or retention-expired source events expect external payload objects to be absent",
    "disabled policy mode rejects every external payload pointer"
  ],
  "omittedFields": ["events.external_payload_uri", "events.content", "provider_credentials", "payload_bytes"],
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
    ERROR_MESSAGE="external payload retention check failed"
  fi

  write_metrics || true
  write_evidence || true

  if [[ -n "$TARGETS_FILE" ]]; then
    rm -f "$TARGETS_FILE"
  fi

  if [[ "$STATUS" == "succeeded" ]]; then
    printf 'External payload retention evidence: %s\n' "$EVIDENCE_FILE"
    printf 'External payload retention metrics: %s\n' "$METRICS_FILE"
  else
    printf 'External payload retention check failed; evidence: %s\n' "$EVIDENCE_FILE" >&2
  fi

  exit "$exit_code"
}

trap 'ERROR_MESSAGE="external payload retention check failed near line $LINENO"' ERR
trap finish EXIT

case "$MODE" in
  audit-only | verify)
    ;;
  *)
    echo "MEMORYSYSTEM_EXTERNAL_PAYLOAD_CHECK_MODE must be audit-only or verify." >&2
    exit 1
    ;;
esac

case "$POLICY_MODE" in
  disabled | audit_only | allowed)
    ;;
  *)
    echo "MEMORYSYSTEM_EXTERNAL_PAYLOAD_POLICY_MODE must be disabled, audit_only, or allowed." >&2
    exit 1
    ;;
esac

require_positive_int MEMORYSYSTEM_EXTERNAL_PAYLOAD_EPHEMERAL_MAX_AGE_DAYS "$EPHEMERAL_MAX_AGE_DAYS"
require_positive_int MEMORYSYSTEM_EXTERNAL_PAYLOAD_STANDARD_MAX_AGE_DAYS "$STANDARD_MAX_AGE_DAYS"
require_positive_int MEMORYSYSTEM_EXTERNAL_PAYLOAD_AUDIT_MAX_AGE_DAYS "$AUDIT_MAX_AGE_DAYS"
require_positive_int MEMORYSYSTEM_EXTERNAL_PAYLOAD_CHECK_BATCH_SIZE "$BATCH_SIZE"
require_command psql

postgres_args=()
if [[ -n "${MEMORYSYSTEM_POSTGRES_URL:-}" ]]; then
  postgres_args=("$MEMORYSYSTEM_POSTGRES_URL")
fi

mkdir -p "$EVIDENCE_DIR"
TARGETS_FILE="$(mktemp "${TMPDIR:-/tmp}/memorysystem-external-payload-targets.XXXXXX")"

printf 'Starting external payload retention check for %s in %s mode\n' "$ENVIRONMENT" "$MODE"

psql "${postgres_args[@]}" -qAt -F $'\t' -v ON_ERROR_STOP=1 \
  -v as_of_utc="$AS_OF_UTC" \
  -v ephemeral_max_age_days="$EPHEMERAL_MAX_AGE_DAYS" \
  -v standard_max_age_days="$STANDARD_MAX_AGE_DAYS" \
  -v audit_max_age_days="$AUDIT_MAX_AGE_DAYS" \
  -v batch_size="$BATCH_SIZE" <<'SQL' >"$TARGETS_FILE"
WITH active_legal_holds AS (
    SELECT DISTINCT link.event_id
    FROM governance_legal_hold_events AS link
    INNER JOIN governance_legal_holds AS hold
        ON hold.id = link.legal_hold_id
        AND hold.status = 'active'
    WHERE link.released_at IS NULL
),
target_events AS (
    SELECT
        event.id,
        event.retention_class,
        event.redaction_status,
        event.created_at,
        event.external_payload_uri,
        COALESCE(event.content->>'minimized', 'false') = 'true' AS content_minimized,
        active_legal_holds.event_id IS NOT NULL AS active_legal_hold
    FROM events AS event
    LEFT JOIN active_legal_holds
        ON active_legal_holds.event_id = event.id
    WHERE event.external_payload_uri IS NOT NULL
    ORDER BY event.created_at, event.id
    LIMIT (:'batch_size')::int
)
SELECT
    target_events.id::text,
    target_events.retention_class,
    target_events.redaction_status,
    CASE
        WHEN target_events.active_legal_hold THEN 'present'
        WHEN target_events.redaction_status <> 'none' THEN 'absent'
        WHEN target_events.content_minimized THEN 'absent'
        WHEN target_events.retention_class = 'erasure_requested' THEN 'absent'
        WHEN target_events.retention_class = 'ephemeral'
            AND target_events.created_at <= :'as_of_utc'::timestamptz - ((:'ephemeral_max_age_days')::int * interval '1 day')
            THEN 'absent'
        WHEN target_events.retention_class = 'standard'
            AND target_events.created_at <= :'as_of_utc'::timestamptz - ((:'standard_max_age_days')::int * interval '1 day')
            THEN 'absent'
        WHEN target_events.retention_class = 'audit'
            AND target_events.created_at <= :'as_of_utc'::timestamptz - ((:'audit_max_age_days')::int * interval '1 day')
            THEN 'absent'
        ELSE 'present'
    END AS expected_external_state,
    lower(split_part(target_events.external_payload_uri, ':', 1)) AS scheme,
    target_events.external_payload_uri
FROM target_events;
SQL

TARGET_EVENTS="$(awk -F $'\t' 'END { print NR + 0 }' "$TARGETS_FILE")"
EXPECTED_PRESENT="$(awk -F $'\t' '$4 == "present" { count++ } END { print count + 0 }' "$TARGETS_FILE")"
EXPECTED_ABSENT="$(awk -F $'\t' '$4 == "absent" { count++ } END { print count + 0 }' "$TARGETS_FILE")"

if [[ "$TARGET_EVENTS" -gt 0 ]]; then
  TARGET_SET_HASH="sha256:$(awk -F $'\t' '{ print $1 }' "$TARGETS_FILE" | sort | sha256_stream)"
fi

while IFS=$'\t' read -r event_id retention_class redaction_status expected_state scheme external_payload_uri; do
  if [[ -z "$event_id" ]]; then
    continue
  fi

  if [[ "$POLICY_MODE" == "disabled" ]]; then
    POLICY_VIOLATIONS=$((POLICY_VIOLATIONS + 1))
    FAILURES=$((FAILURES + 1))
    UNVERIFIED_TARGETS=$((UNVERIFIED_TARGETS + 1))
    continue
  fi

  if ! is_scheme_allowed "$scheme"; then
    POLICY_VIOLATIONS=$((POLICY_VIOLATIONS + 1))
    FAILURES=$((FAILURES + 1))
    UNVERIFIED_TARGETS=$((UNVERIFIED_TARGETS + 1))
    continue
  fi

  if [[ "$MODE" == "audit-only" ]]; then
    UNVERIFIED_TARGETS=$((UNVERIFIED_TARGETS + 1))
    continue
  fi

  actual_state="$(probe_external_payload "$scheme" "$external_payload_uri")"
  case "$actual_state" in
    present)
      if [[ "$expected_state" == "present" ]]; then
        VERIFIED_PRESENT=$((VERIFIED_PRESENT + 1))
      else
        STATE_MISMATCHES=$((STATE_MISMATCHES + 1))
        FAILURES=$((FAILURES + 1))
      fi
      ;;
    absent)
      if [[ "$expected_state" == "absent" ]]; then
        VERIFIED_ABSENT=$((VERIFIED_ABSENT + 1))
      else
        STATE_MISMATCHES=$((STATE_MISMATCHES + 1))
        FAILURES=$((FAILURES + 1))
      fi
      ;;
    unsupported)
      UNSUPPORTED_SCHEME=$((UNSUPPORTED_SCHEME + 1))
      FAILURES=$((FAILURES + 1))
      UNVERIFIED_TARGETS=$((UNVERIFIED_TARGETS + 1))
      ;;
    *)
      PROBE_FAILURES=$((PROBE_FAILURES + 1))
      FAILURES=$((FAILURES + 1))
      UNVERIFIED_TARGETS=$((UNVERIFIED_TARGETS + 1))
      ;;
  esac
done <"$TARGETS_FILE"

if [[ "$FAILURES" -ne 0 ]]; then
  ERROR_MESSAGE="external payload retention check found $FAILURES failures"
  echo "$ERROR_MESSAGE" >&2
  exit 1
fi

STATUS="succeeded"
SUCCESS_VALUE=1
ERROR_MESSAGE=""

printf 'External payload retention check complete: targets=%s expectedAbsent=%s failures=%s\n' \
  "$TARGET_EVENTS" \
  "$EXPECTED_ABSENT" \
  "$FAILURES"
