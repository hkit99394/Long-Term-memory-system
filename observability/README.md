# Observability Artifacts

This folder contains the first executable production-pilot observability assets
for the Long-Term Memory System.

## Contents

- `alert-inputs/api-metrics.txt`: metrics exported by
  `GET /api/operations/metrics` and required by the pilot dashboard or alert
  rules.
- `alert-inputs/external-pilot-metrics.txt`: metrics expected from the hosting
  platform, PostgreSQL provider, backup/restore jobs, or release-gate runner.
- `prometheus/memorysystem-pilot-alerts.yml`: Prometheus-compatible pilot alert
  rules for API, worker, PostgreSQL, retrieval, review, vault export, backup,
  and governance signals.
- `grafana/memorysystem-pilot-dashboard.json`: Grafana-compatible dashboard
  definition covering the pilot dashboard minimum.
- `tracing/memorysystem-pilot-trace-coverage.json`: versioned trace coverage
  manifest with required areas, required span attributes, and forbidden
  payload-bearing attributes.

## Validation

Run the artifact smoke after changing observability assets:

```bash
./scripts/observability-artifacts-smoke.sh
```

Run the live metrics smoke against a running API when validating a local or
pilot environment:

```bash
MEMORYSYSTEM_API_BASE_URL=http://127.0.0.1:5099 ./scripts/operations-metrics-smoke.sh
```

Or ask the artifact smoke to run the live metrics check too:

```bash
MEMORYSYSTEM_OBSERVABILITY_VALIDATE_LIVE_METRICS=true \
MEMORYSYSTEM_API_BASE_URL=http://127.0.0.1:5099 \
  ./scripts/observability-artifacts-smoke.sh
```
