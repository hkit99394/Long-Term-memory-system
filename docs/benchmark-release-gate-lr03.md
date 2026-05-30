# LR-03 Benchmark Release-Gate Report

Date: 2026-05-30

Status: Passed local baseline

## Purpose

LR-03 captures the first benchmark release-gate report after MR-12 made the
gate executable. The raw generated outputs live under `benchmarks/outputs/`,
which is ignored by git; this document records the curated release decision
summary.

## Inputs

| Input | Path | Notes |
| --- | --- | --- |
| LLM outcome scorecard | `benchmarks/outputs/llm-outcome-v0/scorecard-dry-run.json` | Existing filled local scorecard for `llm-outcome-v0`. |
| Agent-contract scorecard | `benchmarks/outputs/agent-contract-usefulness-v1/scorecard-lr03-local.json` | Filled LR-03 local scorecard using the benchmark rubric. |
| Agent-contract smoke | `benchmarks/outputs/agent-contract-usefulness-v1/latest-smoke.run.json` | Existing full eight-task smoke artifact; all tasks passed. |
| Release-gate JSON | `benchmarks/outputs/release-gates/latest.json` | Generated ignored output. |
| Release-gate Markdown | `benchmarks/outputs/release-gates/latest.md` | Generated ignored output. |

## Command

```bash
./scripts/benchmark-release-gate.sh \
  --llm-scorecard benchmarks/outputs/llm-outcome-v0/scorecard-dry-run.json \
  --contract-scorecard benchmarks/outputs/agent-contract-usefulness-v1/scorecard-lr03-local.json \
  --agent-smoke benchmarks/outputs/agent-contract-usefulness-v1/latest-smoke.run.json \
  --output-dir benchmarks/outputs/release-gates
```

## Result

| Metric | Value |
| --- | ---: |
| Memory Lift | +1.575 |
| Contract Lift | +2.025 |
| Scoped-safety leak count | 0 |
| Unauthorized leak count | 0 |
| Redacted content usage count | 0 |
| Cross-scope fact usage count | 0 |
| Source invented count | 0 |
| Policy count inference count | 0 |
| Stale-memory usage count | 0 |
| Memory-derived claim count | 13 |
| Source-linked memory-derived claim count | 13 |
| Source-link coverage | 1.000 |
| Agent-contract smoke | Passed, 8 of 8 tasks |

## Release Decision

The LR-03 local baseline passes the benchmark release gate.

## Caveats

This is a local baseline, not an externally scored pilot benchmark. The LLM
outcome scorecard is the existing local dry-run scorecard, and the
agent-contract scorecard is a local rubric scorecard prepared for LR-03.

Scenario 0001 with the benchmark contradiction overlay was reseeded before this
report. A fresh live smoke run was attempted in the local sandbox, but the API
did not become healthy during that session, so this report uses the existing
full eight-task smoke artifact.

Before inviting an external pilot user, rerun the same gate with the intended
pilot model, freshly filled scorecards, and a fresh live agent-contract smoke
artifact from the target environment.
