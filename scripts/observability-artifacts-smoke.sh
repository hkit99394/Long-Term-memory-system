#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

node "$ROOT_DIR/scripts/observability-artifacts-smoke.mjs"

if [[ "${MEMORYSYSTEM_OBSERVABILITY_VALIDATE_LIVE_METRICS:-false}" == "true" ]]; then
  "$ROOT_DIR/scripts/operations-metrics-smoke.sh"
else
  echo "Set MEMORYSYSTEM_OBSERVABILITY_VALIDATE_LIVE_METRICS=true to also verify a running API's alert inputs."
fi
