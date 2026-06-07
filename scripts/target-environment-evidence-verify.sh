#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "$ROOT_DIR/scripts/lib/checksums.sh"

usage() {
  cat <<'EOF'
Usage: scripts/target-environment-evidence-verify.sh <manifest.json>

Validates a payload-safe target-environment evidence manifest and verifies every
listed artifact exists with the expected SHA-256 hash. Artifact paths must be
local files, absolute or relative to the manifest directory.
EOF
}

if [[ $# -ne 1 ]]; then
  usage >&2
  exit 2
fi

MANIFEST_FILE="$1"

if [[ ! -f "$MANIFEST_FILE" ]]; then
  printf 'Target environment evidence verification failed: manifest not found: %s\n' "$MANIFEST_FILE" >&2
  exit 1
fi

if ! command -v jq >/dev/null 2>&1; then
  printf 'Target environment evidence verification failed: jq is required.\n' >&2
  exit 1
fi

MANIFEST_DIR="$(cd "$(dirname "$MANIFEST_FILE")" && pwd)"
FAILURES=()

add_failure() {
  FAILURES+=("$1")
}

require_string() {
  local filter="$1"
  local label="$2"

  if ! jq -er "$filter | select(type == \"string\" and length > 0)" "$MANIFEST_FILE" >/dev/null; then
    add_failure "missing required field: $label"
  fi
}

require_gate_string() {
  local gate_id="$1"
  local filter="$2"
  local label="$3"

  if ! jq -er --arg id "$gate_id" ".gates[]? | select(.id == \$id) | $filter | select(type == \"string\" and length > 0)" "$MANIFEST_FILE" >/dev/null; then
    add_failure "gate '$gate_id' missing required field: $label"
  fi
}

if ! jq empty "$MANIFEST_FILE" >/dev/null 2>&1; then
  printf 'Target environment evidence verification failed: manifest is not valid JSON: %s\n' "$MANIFEST_FILE" >&2
  exit 1
fi

if ! jq -e '.schemaVersion == 1' "$MANIFEST_FILE" >/dev/null; then
  add_failure "schemaVersion must be 1"
fi

if ! jq -e '.kind == "memorysystem.target_environment_evidence"' "$MANIFEST_FILE" >/dev/null; then
  add_failure "kind must be memorysystem.target_environment_evidence"
fi

if ! jq -e '.payloadSafe == true' "$MANIFEST_FILE" >/dev/null; then
  add_failure "payloadSafe must be true"
fi

require_string '.releaseId' 'releaseId'
require_string '.environment' 'environment'
require_string '.imageDigest' 'imageDigest'
require_string '.evidencePrefix' 'evidencePrefix'
require_string '.generatedAtUtc' 'generatedAtUtc'
require_string '.targetEnvironment.name' 'targetEnvironment.name'
require_string '.targetEnvironment.region' 'targetEnvironment.region'
require_string '.databaseTarget.endpointRef' 'databaseTarget.endpointRef'
require_string '.databaseTarget.databaseName' 'databaseTarget.databaseName'

if ! jq -e '.databaseTarget.pgvectorVerified == true' "$MANIFEST_FILE" >/dev/null; then
  add_failure "databaseTarget.pgvectorVerified must be true"
fi

for owner in releaseOwner rollbackOwner alertRouteOwner evidenceOwner benchmarkScorer governanceReviewer; do
  require_string ".owners.$owner" "owners.$owner"
done

while IFS= read -r duplicate_gate_id; do
  if [[ -n "$duplicate_gate_id" ]]; then
    add_failure "duplicate gate id: $duplicate_gate_id"
  fi
done < <(jq -r '.gates[]?.id // empty' "$MANIFEST_FILE" | sort | uniq -d)

REQUIRED_GATES=(
  "environment_preflight"
  "terraform_validation"
  "deployment_smoke"
  "metrics_and_tracing"
  "benchmark_scorecards"
  "governance_smoke"
  "backup_restore"
  "alert_receiver_acknowledgement"
  "evidence_upload"
  "rollback_notes"
  "go_no_go"
)

for gate_id in "${REQUIRED_GATES[@]}"; do
  gate_count="$(jq -r --arg id "$gate_id" '[.gates[]? | select(.id == $id)] | length' "$MANIFEST_FILE")"
  if [[ "$gate_count" -ne 1 ]]; then
    add_failure "required gate must appear exactly once: $gate_id"
    continue
  fi

  gate_status="$(jq -r --arg id "$gate_id" '.gates[]? | select(.id == $id) | .status // ""' "$MANIFEST_FILE")"
  if [[ "$gate_status" != "passed" ]]; then
    add_failure "gate '$gate_id' status must be passed"
  fi

  artifact_count="$(jq -r --arg id "$gate_id" '[.gates[]? | select(.id == $id) | .artifacts[]?] | length' "$MANIFEST_FILE")"
  if [[ "$artifact_count" -lt 1 ]]; then
    add_failure "gate '$gate_id' must list at least one artifact"
  fi
done

require_gate_string "alert_receiver_acknowledgement" ".acknowledgement.receiver" "acknowledgement.receiver"
require_gate_string "alert_receiver_acknowledgement" ".acknowledgement.acknowledgedBy" "acknowledgement.acknowledgedBy"
require_gate_string "alert_receiver_acknowledgement" ".acknowledgement.acknowledgedAtUtc" "acknowledgement.acknowledgedAtUtc"
require_gate_string "alert_receiver_acknowledgement" ".acknowledgement.runbook" "acknowledgement.runbook"

require_gate_string "rollback_notes" ".rollback.previousImageDigest" "rollback.previousImageDigest"
require_gate_string "rollback_notes" ".rollback.rollbackBoundary" "rollback.rollbackBoundary"
require_gate_string "rollback_notes" ".rollback.communicationRoute" "rollback.communicationRoute"
require_gate_string "rollback_notes" ".signatures.rollbackOwner" "signatures.rollbackOwner"

go_no_go_decision="$(jq -r '.gates[]? | select(.id == "go_no_go") | .decision.decision // ""' "$MANIFEST_FILE")"
if [[ "$go_no_go_decision" != "GO" && "$go_no_go_decision" != "NO-GO" ]]; then
  add_failure "gate 'go_no_go' decision.decision must be GO or NO-GO"
fi
require_gate_string "go_no_go" ".decision.releaseOwnerSignature" "decision.releaseOwnerSignature"
require_gate_string "go_no_go" ".decision.rollbackOwnerSignature" "decision.rollbackOwnerSignature"

while IFS=$'\t' read -r gate_id artifact_index artifact_path expected_hash payload_safe description; do
  if [[ -z "$artifact_path" ]]; then
    add_failure "gate '$gate_id' artifact #$artifact_index missing path"
    continue
  fi

  if [[ -z "$expected_hash" ]]; then
    add_failure "gate '$gate_id' artifact '$artifact_path' missing sha256"
    continue
  fi

  if [[ "$payload_safe" != "true" ]]; then
    add_failure "gate '$gate_id' artifact '$artifact_path' must set payloadSafe true"
  fi

  if [[ -z "$description" ]]; then
    add_failure "gate '$gate_id' artifact '$artifact_path' missing description"
  fi

  if [[ "$artifact_path" =~ ^[A-Za-z][A-Za-z0-9+.-]*:// ]]; then
    add_failure "gate '$gate_id' artifact '$artifact_path' must be a local file path for hash verification"
    continue
  fi

  if [[ "$artifact_path" = /* ]]; then
    artifact_file="$artifact_path"
  else
    artifact_file="$MANIFEST_DIR/$artifact_path"
  fi

  normalized_hash="$expected_hash"
  if [[ "$normalized_hash" != sha256:* ]]; then
    normalized_hash="sha256:$normalized_hash"
  fi
  normalized_hash="$(printf '%s' "$normalized_hash" | tr '[:upper:]' '[:lower:]')"

  if ! [[ "$normalized_hash" =~ ^sha256:[0-9a-f]{64}$ ]]; then
    add_failure "gate '$gate_id' artifact '$artifact_path' sha256 must be sha256:<64 hex chars>"
    continue
  fi

  if [[ ! -f "$artifact_file" ]]; then
    add_failure "gate '$gate_id' artifact file not found: $artifact_path"
    continue
  fi

  actual_hash="sha256:$(sha256_file "$artifact_file")"
  if [[ "$actual_hash" != "$normalized_hash" ]]; then
    add_failure "gate '$gate_id' artifact '$artifact_path' sha256 mismatch: expected $normalized_hash, got $actual_hash"
  fi
done < <(jq -r '
  .gates[]? as $gate
  | ($gate.artifacts // [])
  | to_entries[]
  | [
      ($gate.id // ""),
      (.key | tostring),
      (.value.path // ""),
      (.value.sha256 // ""),
      (.value.payloadSafe // false | tostring),
      (.value.description // "")
    ]
  | @tsv
' "$MANIFEST_FILE")

if [[ "${#FAILURES[@]}" -gt 0 ]]; then
  printf 'Target environment evidence verification failed for %s:\n' "$MANIFEST_FILE" >&2
  for failure in "${FAILURES[@]}"; do
    printf -- '- %s\n' "$failure" >&2
  done
  exit 1
fi

release_id="$(jq -r '.releaseId' "$MANIFEST_FILE")"
environment="$(jq -r '.environment' "$MANIFEST_FILE")"
artifact_total="$(jq -r '[.gates[]?.artifacts[]?] | length' "$MANIFEST_FILE")"
printf 'Target environment evidence verification passed: releaseId=%s environment=%s artifacts=%s\n' "$release_id" "$environment" "$artifact_total"
