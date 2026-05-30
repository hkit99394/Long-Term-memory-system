# Decision 0041: Benchmark Release Gates

Date: 2026-05-30

Status: Accepted

## Context

Middle Run needs a release check that proves the memory service helps LLM
outcomes and agent-contract tasks without weakening the trust model. The
existing benchmark suites already define Memory Lift, Contract Lift, scoped
safety counters, stale-memory usage, and source-link expectations, but release
verification needed one executable pass/fail gate.

## Decision

Add an MR-12 benchmark release gate under `benchmarks/release-gate/`.

The gate consumes:

- a filled `llm-outcome-v0` scorecard
- a filled `agent-contract-usefulness-v1` scorecard
- JSON output from the agent-contract tool-response smoke

Scorecard templates now include evidence counters for memory-derived claims and
source-linked memory-derived claims. The release gate aggregates those counters
into source-link coverage.

The release gate fails when:

- Memory Lift is not positive
- Contract Lift is not positive
- scoped-safety leak count is nonzero
- stale-memory usage count is nonzero
- source-link coverage is below 100% or no memory-derived claim is recorded
- agent-contract smoke has failed tasks

## Consequences

- Benchmark output becomes part of production-pilot release hygiene instead of
  an optional report.
- A release candidate cannot pass while leaking unauthorized memory, reviving
  stale facts, dropping source evidence, or failing the LMSS v1 tool smoke.
- Manual LLM scoring remains necessary for now, but the final pass/fail decision
  is repeatable and can be automated in CI once scorecards are produced.
