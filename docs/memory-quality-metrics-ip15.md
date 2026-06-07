# Memory Quality Metrics IP-15

IP-15 adds a payload-safe memory-quality slice to the existing operator
surfaces:

- `GET /api/operations/summary` now includes `memoryQuality`.
- `GET /api/operations/metrics` exports Prometheus-compatible
  `memorysystem_memory_quality_*` gauges and counters.
- The `/admin/` Operations view includes a Memory quality row and detail panel.

## Metrics

The quality window uses the same 24-hour feedback window as retrieval feedback.

| Signal | Summary field | Prometheus metric |
| --- | --- | --- |
| Source-link coverage | `sourceLinkCoverage` | `memorysystem_memory_quality_source_link_coverage` |
| Active source-linked items | `sourceLinkedActiveMemoryItems` | `memorysystem_memory_quality_source_linked_active_items` |
| Stale-memory rate | `staleMemoryRate` | `memorysystem_memory_quality_stale_memory_rate` |
| Useful feedback rate | `usefulFeedbackRate` | `memorysystem_memory_quality_useful_feedback_rate` |
| Missing-memory reports | `missingMemoryReports` | `memorysystem_memory_quality_missing_memory_reports_total` |
| Role-boundary misses | `roleBoundaryMisses` | `memorysystem_memory_quality_role_boundary_misses_total` |
| Duplicate ratio | `duplicateRatio` | `memorysystem_memory_quality_duplicate_ratio` |

`sourceLinkCoverage` is the share of active memory facts and role lenses that
carry source-event evidence. `staleMemoryRate` is the share of active memory
items with recent stale feedback. `usefulFeedbackRate` is the useful share of
recent retrieval feedback. `duplicateRatio` uses the same stable identity shape
as the weekly admin review workflow: memory type, namespace, subject,
predicate, and object for facts; role, scope, base fact, and interpretation for
role lenses.

Role-boundary misses are counted from context-product `role_mismatch` exclusion
summaries. The high-level miss count is safe even when item counts are withheld;
the disclosed item count is exported separately when the caller may see it.

## Verification

Run these after changing the quality summary, metrics renderer, or admin
Operations view:

```bash
dotnet test tests/MemorySystem.IntegrationTests/MemorySystem.IntegrationTests.csproj --filter FullyQualifiedName~OperationalSummaryEndpointTests
npm run build --prefix tools/ui
npm run check --prefix tools/ui
```

When a local API is running, the existing metrics smoke also validates the new
metric names through `observability/alert-inputs/api-metrics.txt`:

```bash
MEMORYSYSTEM_API_BASE_URL=http://127.0.0.1:5099 ./scripts/operations-metrics-smoke.sh
```
