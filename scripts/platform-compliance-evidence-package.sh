#!/usr/bin/env bash
set -euo pipefail

ENVIRONMENT="${MEMORYSYSTEM_ENVIRONMENT:-local}"
RUN_ID="$(date -u +%Y%m%dT%H%M%SZ)_$$"
PACKAGE_ID="${MEMORYSYSTEM_COMPLIANCE_PACKAGE_ID:-memorysystem-$ENVIRONMENT-$RUN_ID}"
MODE="${MEMORYSYSTEM_COMPLIANCE_EVIDENCE_PACKAGE_MODE:-draft}"
OPERATOR_ID="${MEMORYSYSTEM_COMPLIANCE_OPERATOR_ID:-unknown}"
RELEASE_ID="${MEMORYSYSTEM_COMPLIANCE_RELEASE_ID:-}"
GOVERNANCE_EVIDENCE_DIR="${MEMORYSYSTEM_COMPLIANCE_GOVERNANCE_EVIDENCE_DIR:-/tmp/memorysystem-governance-evidence}"
BACKUP_EVIDENCE_DIR="${MEMORYSYSTEM_COMPLIANCE_BACKUP_EVIDENCE_DIR:-/tmp/memorysystem-backup-evidence}"
RELEASE_EVIDENCE_DIR="${MEMORYSYSTEM_COMPLIANCE_RELEASE_EVIDENCE_DIR:-/tmp/memorysystem-release-evidence}"
PACKAGE_DIR="${MEMORYSYSTEM_COMPLIANCE_EVIDENCE_DIR:-/tmp/memorysystem-compliance-evidence}"
MANIFEST_FILE="${MEMORYSYSTEM_COMPLIANCE_PACKAGE_FILE:-$PACKAGE_DIR/$PACKAGE_ID.json}"
ARTIFACT_INDEX_FILE="${MEMORYSYSTEM_COMPLIANCE_ARTIFACT_INDEX_FILE:-$PACKAGE_DIR/$PACKAGE_ID-artifacts.ndjson}"
MANIFEST_HASH_FILE="${MEMORYSYSTEM_COMPLIANCE_PACKAGE_HASH_FILE:-$MANIFEST_FILE.sha256}"
METRICS_FILE="${MEMORYSYSTEM_COMPLIANCE_METRICS_FILE:-$PACKAGE_DIR/compliance-evidence-package-metrics.prom}"
STARTED_AT_UTC="$(date -u +"%Y-%m-%dT%H:%M:%SZ")"
COMPLETED_AT_UTC=""
COMPLETED_AT_SECONDS="$(date -u +%s)"
STATUS="failed"
ERROR_MESSAGE=""
SUCCESS_VALUE=0
ARTIFACT_COUNT=0
PRESENT_ARTIFACTS=0
MISSING_ARTIFACTS=0
REQUIRED_ARTIFACTS=0
MISSING_REQUIRED_ARTIFACTS=0
MANIFEST_BYTES=0
MANIFEST_SHA256=""
ARTIFACT_SET_HASH=""

AUDIT_EXPORT_FILE="${MEMORYSYSTEM_COMPLIANCE_AUDIT_EXPORT_FILE:-$GOVERNANCE_EVIDENCE_DIR/audit-export.ndjson}"
RETENTION_REPORT_FILE="${MEMORYSYSTEM_COMPLIANCE_RETENTION_REPORT_FILE:-$GOVERNANCE_EVIDENCE_DIR/retention-report.json}"
LEGAL_HOLD_SUMMARY_FILE="${MEMORYSYSTEM_COMPLIANCE_LEGAL_HOLD_SUMMARY_FILE:-$GOVERNANCE_EVIDENCE_DIR/legal-hold-summary.json}"
PERMISSION_DRIFT_FILE="${MEMORYSYSTEM_COMPLIANCE_PERMISSION_DRIFT_FILE:-$GOVERNANCE_EVIDENCE_DIR/permission-drift-report.json}"
RETENTION_MINIMIZATION_FILE="${MEMORYSYSTEM_COMPLIANCE_RETENTION_MINIMIZATION_FILE:-$GOVERNANCE_EVIDENCE_DIR/retention-minimization-evidence.json}"
EXTERNAL_PAYLOAD_RETENTION_FILE="${MEMORYSYSTEM_COMPLIANCE_EXTERNAL_PAYLOAD_RETENTION_FILE:-$GOVERNANCE_EVIDENCE_DIR/external-payload-retention-evidence.json}"
ERASURE_REPLAY_FILE="${MEMORYSYSTEM_COMPLIANCE_ERASURE_REPLAY_FILE:-$BACKUP_EVIDENCE_DIR/erasure-replay-ledger-evidence.json}"
BACKUP_EXPORT_FILE="${MEMORYSYSTEM_COMPLIANCE_BACKUP_EXPORT_FILE:-$BACKUP_EVIDENCE_DIR/backup-export-evidence.json}"
RESTORE_VALIDATION_FILE="${MEMORYSYSTEM_COMPLIANCE_RESTORE_VALIDATION_FILE:-$BACKUP_EVIDENCE_DIR/restore-validation-evidence.json}"
RELEASE_CHECKLIST_FILE="${MEMORYSYSTEM_COMPLIANCE_RELEASE_CHECKLIST_FILE:-$RELEASE_EVIDENCE_DIR/release-checklist-evidence.json}"
BENCHMARK_GATE_FILE="${MEMORYSYSTEM_COMPLIANCE_BENCHMARK_GATE_FILE:-$RELEASE_EVIDENCE_DIR/benchmark-release-gate.json}"
ALERT_ROUTE_FILE="${MEMORYSYSTEM_COMPLIANCE_ALERT_ROUTE_FILE:-$RELEASE_EVIDENCE_DIR/alert-route-smoke.json}"

ARTIFACT_IDS=(
  "audit_export"
  "retention_report"
  "legal_hold_summary"
  "permission_drift_report"
  "retention_minimization"
  "external_payload_retention"
  "erasure_replay"
  "backup_export"
  "restore_validation"
  "release_checklist"
  "benchmark_release_gate"
  "alert_route_smoke"
)

ARTIFACT_TYPES=(
  "audit_export"
  "retention_report"
  "legal_hold_summary"
  "permission_drift_report"
  "retention_minimization_evidence"
  "external_payload_retention_evidence"
  "erasure_replay_evidence"
  "backup_export_evidence"
  "restore_validation_evidence"
  "release_checklist_evidence"
  "benchmark_release_gate"
  "alert_route_smoke"
)

ARTIFACT_PATHS=(
  "$AUDIT_EXPORT_FILE"
  "$RETENTION_REPORT_FILE"
  "$LEGAL_HOLD_SUMMARY_FILE"
  "$PERMISSION_DRIFT_FILE"
  "$RETENTION_MINIMIZATION_FILE"
  "$EXTERNAL_PAYLOAD_RETENTION_FILE"
  "$ERASURE_REPLAY_FILE"
  "$BACKUP_EXPORT_FILE"
  "$RESTORE_VALIDATION_FILE"
  "$RELEASE_CHECKLIST_FILE"
  "$BENCHMARK_GATE_FILE"
  "$ALERT_ROUTE_FILE"
)

ARTIFACT_REQUIRED=(
  "true"
  "true"
  "true"
  "true"
  "true"
  "true"
  "true"
  "true"
  "true"
  "true"
  "true"
  "true"
)

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

sha256_file() {
  local file="$1"

  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum "$file" | awk '{print $1}'
    return
  fi

  shasum -a 256 "$file" | awk '{print $1}'
}

sha256_stream() {
  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum | awk '{print $1}'
    return
  fi

  shasum -a 256 | awk '{print $1}'
}

write_metrics() {
  local escaped_environment
  local escaped_mode
  escaped_environment="$(metric_label_escape "$ENVIRONMENT")"
  escaped_mode="$(metric_label_escape "$MODE")"

  mkdir -p "$(dirname "$METRICS_FILE")"
  cat >"$METRICS_FILE" <<EOF
# HELP memorysystem_compliance_evidence_package_success Whether the latest compliance evidence package command completed successfully.
# TYPE memorysystem_compliance_evidence_package_success gauge
memorysystem_compliance_evidence_package_success{environment="$escaped_environment",mode="$escaped_mode"} $SUCCESS_VALUE
# HELP memorysystem_compliance_evidence_package_artifacts Evidence artifacts listed in the latest package manifest.
# TYPE memorysystem_compliance_evidence_package_artifacts gauge
memorysystem_compliance_evidence_package_artifacts{environment="$escaped_environment",mode="$escaped_mode"} $ARTIFACT_COUNT
# HELP memorysystem_compliance_evidence_package_present_artifacts Evidence artifacts present when the latest package manifest was generated.
# TYPE memorysystem_compliance_evidence_package_present_artifacts gauge
memorysystem_compliance_evidence_package_present_artifacts{environment="$escaped_environment",mode="$escaped_mode"} $PRESENT_ARTIFACTS
# HELP memorysystem_compliance_evidence_package_missing_artifacts Evidence artifacts missing when the latest package manifest was generated.
# TYPE memorysystem_compliance_evidence_package_missing_artifacts gauge
memorysystem_compliance_evidence_package_missing_artifacts{environment="$escaped_environment",mode="$escaped_mode"} $MISSING_ARTIFACTS
# HELP memorysystem_compliance_evidence_package_missing_required_artifacts Required evidence artifacts missing from the latest package manifest.
# TYPE memorysystem_compliance_evidence_package_missing_required_artifacts gauge
memorysystem_compliance_evidence_package_missing_required_artifacts{environment="$escaped_environment",mode="$escaped_mode"} $MISSING_REQUIRED_ARTIFACTS
# HELP memorysystem_compliance_evidence_package_manifest_bytes Size of the latest compliance evidence manifest in bytes.
# TYPE memorysystem_compliance_evidence_package_manifest_bytes gauge
memorysystem_compliance_evidence_package_manifest_bytes{environment="$escaped_environment",mode="$escaped_mode"} $MANIFEST_BYTES
# HELP memorysystem_compliance_evidence_package_timestamp_seconds Unix timestamp for the latest compliance evidence package attempt.
# TYPE memorysystem_compliance_evidence_package_timestamp_seconds gauge
memorysystem_compliance_evidence_package_timestamp_seconds{environment="$escaped_environment",mode="$escaped_mode"} $COMPLETED_AT_SECONDS
EOF
}

artifact_json() {
  local index="$1"
  local artifact_id="${ARTIFACT_IDS[$index]}"
  local artifact_type="${ARTIFACT_TYPES[$index]}"
  local artifact_path="${ARTIFACT_PATHS[$index]}"
  local required="${ARTIFACT_REQUIRED[$index]}"
  local presence="missing"
  local bytes=0
  local sha256=""

  if [[ -f "$artifact_path" ]]; then
    presence="present"
    bytes="$(wc -c <"$artifact_path" | tr -d ' ')"
    sha256="sha256:$(sha256_file "$artifact_path")"
  fi

  printf '{"id":"%s","type":"%s","required":%s,"status":"%s","path":"%s","bytes":%s,"sha256":"%s"}' \
    "$(json_escape "$artifact_id")" \
    "$(json_escape "$artifact_type")" \
    "$required" \
    "$presence" \
    "$(json_escape "$artifact_path")" \
    "$bytes" \
    "$(json_escape "$sha256")"
}

collect_counts() {
  local index
  local artifact_path
  local required

  ARTIFACT_COUNT="${#ARTIFACT_IDS[@]}"
  PRESENT_ARTIFACTS=0
  MISSING_ARTIFACTS=0
  REQUIRED_ARTIFACTS=0
  MISSING_REQUIRED_ARTIFACTS=0

  for index in "${!ARTIFACT_IDS[@]}"; do
    artifact_path="${ARTIFACT_PATHS[$index]}"
    required="${ARTIFACT_REQUIRED[$index]}"

    if [[ "$required" == "true" ]]; then
      REQUIRED_ARTIFACTS=$((REQUIRED_ARTIFACTS + 1))
    fi

    if [[ -f "$artifact_path" ]]; then
      PRESENT_ARTIFACTS=$((PRESENT_ARTIFACTS + 1))
    else
      MISSING_ARTIFACTS=$((MISSING_ARTIFACTS + 1))
      if [[ "$required" == "true" ]]; then
        MISSING_REQUIRED_ARTIFACTS=$((MISSING_REQUIRED_ARTIFACTS + 1))
      fi
    fi
  done

  ARTIFACT_SET_HASH="sha256:$(printf '%s\n' "${ARTIFACT_IDS[@]}" | sort | sha256_stream)"
}

write_package_files() {
  local first=true
  local index

  mkdir -p "$(dirname "$MANIFEST_FILE")"
  mkdir -p "$(dirname "$ARTIFACT_INDEX_FILE")"

  : >"$ARTIFACT_INDEX_FILE"
  for index in "${!ARTIFACT_IDS[@]}"; do
    artifact_json "$index" >>"$ARTIFACT_INDEX_FILE"
    printf '\n' >>"$ARTIFACT_INDEX_FILE"
  done

  {
    printf '{\n'
    printf '  "schemaVersion": 1,\n'
    printf '  "kind": "memorysystem.compliance_evidence_package",\n'
    printf '  "runId": "%s",\n' "$(json_escape "$RUN_ID")"
    printf '  "packageId": "%s",\n' "$(json_escape "$PACKAGE_ID")"
    printf '  "environment": "%s",\n' "$(json_escape "$ENVIRONMENT")"
    printf '  "mode": "%s",\n' "$(json_escape "$MODE")"
    printf '  "operatorId": "%s",\n' "$(json_escape "$OPERATOR_ID")"
    printf '  "releaseId": "%s",\n' "$(json_escape "$RELEASE_ID")"
    printf '  "payloadSafe": true,\n'
    printf '  "artifactCount": %s,\n' "$ARTIFACT_COUNT"
    printf '  "requiredArtifactCount": %s,\n' "$REQUIRED_ARTIFACTS"
    printf '  "presentArtifactCount": %s,\n' "$PRESENT_ARTIFACTS"
    printf '  "missingArtifactCount": %s,\n' "$MISSING_ARTIFACTS"
    printf '  "missingRequiredArtifactCount": %s,\n' "$MISSING_REQUIRED_ARTIFACTS"
    printf '  "artifactSetHash": "%s",\n' "$(json_escape "$ARTIFACT_SET_HASH")"
    printf '  "artifactIndexFile": "%s",\n' "$(json_escape "$ARTIFACT_INDEX_FILE")"
    printf '  "manifestHashFile": "%s",\n' "$(json_escape "$MANIFEST_HASH_FILE")"
    printf '  "artifacts": [\n'
    for index in "${!ARTIFACT_IDS[@]}"; do
      if [[ "$first" == true ]]; then
        first=false
      else
        printf ',\n'
      fi
      printf '    '
      artifact_json "$index"
    done
    printf '\n  ],\n'
    printf '  "omittedFields": ["event.content", "memory_facts.object", "memory_chunks.content", "memory_reviews.notes", "raw_query", "embedding.input", "provider_credentials", "payload_bytes"],\n'
    printf '  "startedAtUtc": "%s",\n' "$STARTED_AT_UTC"
    printf '  "completedAtUtc": "%s",\n' "$COMPLETED_AT_UTC"
    printf '  "status": "%s",\n' "$STATUS"
    printf '  "error": "%s"\n' "$(json_escape "$ERROR_MESSAGE")"
    printf '}\n'
  } >"$MANIFEST_FILE"

  MANIFEST_BYTES="$(wc -c <"$MANIFEST_FILE" | tr -d ' ')"
  MANIFEST_SHA256="$(sha256_file "$MANIFEST_FILE")"
  printf '%s  %s\n' "$MANIFEST_SHA256" "$MANIFEST_FILE" >"$MANIFEST_HASH_FILE"
}

finish() {
  local exit_code=$?

  if [[ "$exit_code" -ne 0 && -z "$ERROR_MESSAGE" ]]; then
    ERROR_MESSAGE="compliance evidence package failed"
  fi

  write_metrics || true

  if [[ "$STATUS" == "succeeded" ]]; then
    printf 'Compliance evidence package: %s\n' "$MANIFEST_FILE"
    printf 'Compliance evidence artifact index: %s\n' "$ARTIFACT_INDEX_FILE"
    printf 'Compliance evidence manifest hash: %s\n' "$MANIFEST_HASH_FILE"
    printf 'Compliance evidence metrics: %s\n' "$METRICS_FILE"
  else
    printf 'Compliance evidence package failed; manifest: %s\n' "$MANIFEST_FILE" >&2
  fi

  exit "$exit_code"
}

trap 'ERROR_MESSAGE="compliance evidence package failed near line $LINENO"' ERR
trap finish EXIT

case "$MODE" in
  draft | strict)
    ;;
  *)
    echo "MEMORYSYSTEM_COMPLIANCE_EVIDENCE_PACKAGE_MODE must be draft or strict." >&2
    exit 1
    ;;
esac

printf 'Building compliance evidence package for %s in %s mode\n' "$ENVIRONMENT" "$MODE"

collect_counts

if [[ "$MODE" == "strict" && "$MISSING_REQUIRED_ARTIFACTS" -ne 0 ]]; then
  COMPLETED_AT_UTC="$(date -u +"%Y-%m-%dT%H:%M:%SZ")"
  COMPLETED_AT_SECONDS="$(date -u +%s)"
  STATUS="failed"
  ERROR_MESSAGE="missing $MISSING_REQUIRED_ARTIFACTS required evidence artifacts"
  write_package_files
  echo "$ERROR_MESSAGE" >&2
  exit 1
fi

COMPLETED_AT_UTC="$(date -u +"%Y-%m-%dT%H:%M:%SZ")"
COMPLETED_AT_SECONDS="$(date -u +%s)"
STATUS="succeeded"
SUCCESS_VALUE=1
ERROR_MESSAGE=""
write_package_files

printf 'Compliance evidence package complete: artifacts=%s present=%s missingRequired=%s\n' \
  "$ARTIFACT_COUNT" \
  "$PRESENT_ARTIFACTS" \
  "$MISSING_REQUIRED_ARTIFACTS"
