# Decision 0043: Context Productization Gate

Date: 2026-05-31

Status: Accepted

## Context

LR-02 starts the Long Run context productization track. The project already has
authorized hybrid retrieval, context packets, source links, query-facts safe
exclusions, context feedback, retrieval feedback metrics, and benchmark release
gates. The remaining product gap is that context packets are not yet a complete
agent and reviewer contract: inclusion explanations are generic, packet-level
exclusions are not surfaced, and feedback actions are not rich enough to drive
review and ranking improvement loops.

## Decision

Adopt [Context Productization Gate](../context-productization-gate.md) as the
LR-02 implementation scope.

The context productization architecture will:

- preserve the authorized hybrid retrieval and context packet grouping model
- add stable packet and item identifiers for feedback and trace correlation
- add structured inclusion explanations for each included memory
- add packet-level safe exclusion summaries using withheld disclosures for
  unauthorized or sensitive omissions
- expand reviewer actions to useful, stale, wrong, sensitive, over-broad, and
  missing context while preserving the legacy noisy feedback path
- route stale, wrong, sensitive, and over-broad observations into review
  workflows instead of rewriting memory facts directly
- feed reviewed and bounded feedback signals into ranking by scope, namespace,
  role, and source id
- make explanation coverage, safe exclusions, feedback actions, and ranking
  deltas visible in benchmarks and operations metrics

Context productization must not disclose unauthorized candidates, hidden counts,
raw source payloads, or sensitive text through explanations, exclusions,
feedback, logs, metrics, or benchmark artifacts.

## Consequences

- Context packets become a product contract for agents and reviewers, not only a
  compact retrieval response.
- Reviewers get action-specific paths for stale, wrong, sensitive, missing, and
  over-broad context.
- Ranking can improve from feedback while keeping durable memory changes behind
  broker and review workflows.
- Benchmark release gates can measure explanation coverage, safe exclusions, and
  feedback-driven ranking deltas against the LR-03 baseline.
- Implementation work is split into the `CP-*` backlog items before changing the
  runtime context packet shape.
