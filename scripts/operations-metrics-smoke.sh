#!/usr/bin/env bash
set -euo pipefail

API_BASE_URL="${MEMORYSYSTEM_API_BASE_URL:-http://127.0.0.1:5099}"
API_KEY="${MEMORYSYSTEM_API_KEY:-private-alpha-local-key}"
OUTPUT_FILE="${MEMORYSYSTEM_OPERATIONS_METRICS_SMOKE_FILE:-}"

tmp_file="$(mktemp)"

cleanup() {
  rm -f "$tmp_file"
}

trap cleanup EXIT

echo "Priming API request metrics..."
curl -fsS \
  -H "X-Api-Key: $API_KEY" \
  "$API_BASE_URL/api/operations/summary" \
  >/dev/null

echo "Fetching operations metrics..."
curl -fsS \
  -H "X-Api-Key: $API_KEY" \
  "$API_BASE_URL/api/operations/metrics" \
  -o "$tmp_file"

required_metrics=(
  memorysystem_api_requests_total
  memorysystem_health_ready
  memorysystem_health_check_status
  memorysystem_outbox_ready_pending
  memorysystem_outbox_dead_letter
  memorysystem_outbox_oldest_ready_pending_age_seconds
  memorysystem_worker_heartbeat_observed
  memorysystem_worker_heartbeat_stale
  memorysystem_retrieval_feedback_total
  memorysystem_retrieval_feedback_type_total
  memorysystem_embedding_provider_ready
  memorysystem_embedding_outbox_retrying_failed
  memorysystem_embedding_outbox_dead_letter
)

for metric_name in "${required_metrics[@]}"; do
  if ! grep -q "^$metric_name" "$tmp_file"; then
    echo "Missing expected metric: $metric_name" >&2
    echo "First 120 metric lines:" >&2
    sed -n '1,120p' "$tmp_file" >&2
    exit 1
  fi

  printf '  %-56s present\n' "$metric_name"
done

if [[ -n "$OUTPUT_FILE" ]]; then
  mkdir -p "$(dirname "$OUTPUT_FILE")"
  cp "$tmp_file" "$OUTPUT_FILE"
  echo "Saved metrics snapshot to $OUTPUT_FILE"
fi

echo "Operations metrics smoke passed."
