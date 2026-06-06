#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LOCAL_DEMO_API_KEY="private-alpha-local-key"
API_BASE_URL="${MEMORYSYSTEM_API_BASE_URL:-http://127.0.0.1:5099}"
API_KEY="${MEMORYSYSTEM_API_KEY:-}"
OUTPUT_FILE="${MEMORYSYSTEM_OPERATIONS_METRICS_SMOKE_FILE:-}"
ALERT_INPUTS_FILE="${MEMORYSYSTEM_OBSERVABILITY_ALERT_INPUTS_FILE:-$ROOT_DIR/observability/alert-inputs/api-metrics.txt}"

tmp_file="$(mktemp)"

cleanup() {
  rm -f "$tmp_file"
}

trap cleanup EXIT

is_loopback_url() {
  [[ "$1" =~ ^https?://(127\.0\.0\.1|localhost)([:/]|$) || "$1" =~ ^https?://\[::1\]([:/]|$) ]]
}

if [[ -z "$API_KEY" ]]; then
  if is_loopback_url "$API_BASE_URL"; then
    API_KEY="$LOCAL_DEMO_API_KEY"
    echo "Using local demo API key default for loopback operations metrics smoke." >&2
  else
    echo "MEMORYSYSTEM_API_KEY is required when running operations metrics smoke against a non-loopback API." >&2
    exit 64
  fi
fi

if [[ "$API_KEY" == "$LOCAL_DEMO_API_KEY" ]] && ! is_loopback_url "$API_BASE_URL"; then
  echo "Refusing to use the public local demo API key against a non-loopback API." >&2
  exit 64
fi

required_metrics=()
while IFS= read -r metric_name; do
  metric_name="${metric_name#"${metric_name%%[![:space:]]*}"}"
  metric_name="${metric_name%"${metric_name##*[![:space:]]}"}"

  if [[ -z "$metric_name" || "${metric_name:0:1}" == "#" ]]; then
    continue
  fi

  required_metrics+=("$metric_name")
done < "$ALERT_INPUTS_FILE"

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

for metric_name in "${required_metrics[@]}"; do
  if ! grep -Eq "^$metric_name(\\{| |$)" "$tmp_file"; then
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
