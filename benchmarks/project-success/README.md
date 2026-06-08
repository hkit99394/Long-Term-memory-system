# Project Success Benchmark

This benchmark suite measures whether the long-term memory system improves real
project work during a pilot. It complements the technical release gates under
`benchmarks/release-gate`; it does not replace them.

## Scope

Project success is measured across six areas:

- adoption
- memory quality
- trust and safety
- operator burden
- project delivery impact
- human confidence

The canonical product contract is
[`docs/project-success-benchmark-ps01.md`](../../docs/project-success-benchmark-ps01.md).
The first selected pilot and baseline are recorded in
[`docs/project-success-pilot-baseline-ps02.md`](../../docs/project-success-pilot-baseline-ps02.md).
The two-week observation runbook is recorded in
[`docs/project-success-observation-ps03.md`](../../docs/project-success-observation-ps03.md).

## First Pilot Window

| Phase | Dates |
| --- | --- |
| Planning | 2026-06-08 to 2026-06-14 |
| Pilot observation | 2026-06-15 to 2026-06-28 |
| Closeout | 2026-06-29 to 2026-07-05 |

## Scorecard Template

Use [`scorecard-template.json`](scorecard-template.json) for the first pilot
scorecard. Keep generated filled scorecards under `benchmarks/outputs/` unless a
curated summary is intentionally attached to release or pilot evidence.

The scorecard artifact kind is `memorysystem.project_success_scorecard`.

Use [`weekly-cycle-template.json`](weekly-cycle-template.json) for each PS-03
weekly observation cycle. Filled cycle records stay under
`benchmarks/outputs/project-success/` unless a curated payload-safe summary is
attached to pilot evidence.

The weekly cycle artifact kind is
`memorysystem.project_success_observation_cycle`.

The scorecard is payload-safe. It stores ids, counts, percentages, ratings,
dates, decisions, and links to evidence; it must not include raw source payloads,
memory bodies, review notes, raw queries, provider payloads, API keys, database
connection strings, or secret values.

## First-Pilot Hard Gates

The first pilot cannot be marked `GO` if any of these fail:

- scoped safety leaks are above zero
- stale-memory benchmark usage is above zero
- source-link coverage is below 100 percent for active memory-derived claims
- committed scorecard evidence contains raw payloads
- critical review items remain unresolved after 2 business days
- weekly access-boundary review is missed

## Relationship To Existing Benchmarks

| Existing benchmark | Role in project-success scorecard |
| --- | --- |
| `llm-outcome-v0` | Provides Memory Lift and task-output quality evidence. |
| `agent-contract-usefulness-v1` | Provides Contract Lift and agent-facing tool usefulness evidence. |
| `context-product-v1` | Provides context explainability, safe exclusion, and feedback hygiene evidence. |
| `release-gate` | Provides safety counters and release-blocking benchmark evidence. |
