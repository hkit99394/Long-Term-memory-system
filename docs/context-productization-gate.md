# Context Productization Gate

Date: 2026-05-31

Status: Scoped

## Purpose

LR-02 scopes the context productization gate. The goal is to turn context
packets from a useful backend response into an explainable, reviewable product
surface that agents and humans can trust.

The key invariant is:

```text
Context packets explain included memory and summarize excluded memory without
revealing unauthorized, redacted, deleted, or sensitive payloads.
```

This gate does not replace authorized hybrid retrieval. It productizes the
contract around it: why items were included, what was safely excluded, which
review actions are available, and how those actions improve ranking in ways the
benchmark release gate can see.

## Current Baseline

The production-pilot baseline already has:

- authorized hybrid retrieval with rank components for relevance, confidence,
  recency, authority, and scope match
- a context packet builder that groups memory into user preferences, project
  memory, role memory, and relevant decisions
- source event links for packet items
- `memory.queryFacts` responses with source links, contradictions, lifecycle
  metadata, and safe exclusion summaries
- context-packet feedback for `useful`, `stale`, `missing`, and `noisy`
- operator retrieval-feedback metrics over the recent window
- benchmark release gates for Memory Lift, Contract Lift, scoped-safety leaks,
  stale-memory usage, source-link coverage, and agent-contract smoke

The missing product layer is a stable contract that tells callers why context
was selected, what was not shown, and what a reviewer can do when context is
wrong, stale, sensitive, missing, or too broad.

## Scope

LR-02 covers the implementation plan for:

- packet-level inclusion explanations
- packet-level safe exclusion summaries
- reviewer feedback actions for useful, stale, wrong, sensitive, over-broad, and
  missing context
- feedback-to-ranking and feedback-to-broker policy loops
- admin console review workflows for context packet observations
- benchmark-visible ranking improvement criteria
- payload-safe telemetry and reports for context product health

LR-02 does not implement the new context response shape yet. It defines the next
build slices and acceptance gates.

## Product Contract

### Packet Shape

The productized context packet should keep the current grouped item lists, then
add packet-level metadata:

| Field | Purpose |
| --- | --- |
| `packetId` | Stable id for feedback, review actions, and trace correlation. |
| `generatedAt` | Server time used for recency and lifecycle explanations. |
| `policy` | Safe summary of the target scope, role, and namespace filters applied. |
| `included` | Existing grouped packet items with richer inclusion explanations. |
| `excluded` | Safe exclusion summaries for omitted candidates. |
| `reviewActions` | Actions the caller may take on this packet or individual items. |
| `feedbackPolicy` | Tells callers which feedback fields are required and which raw fields are not stored. |
| `evaluationHints` | Payload-safe ids and counters used by benchmarks and operator dashboards. |

The response should remain compact enough for direct LLM use. Full audit and
operator details belong behind admin or source-evidence links, not inside the
agent prompt payload.

### Inclusion Explanations

Each included item should carry a structured explanation, not only a generic
sentence.

Minimum explanation fields:

| Field | Meaning |
| --- | --- |
| `primaryReason` | Human-readable reason such as `query_match`, `role_match`, `recent_decision`, or `high_confidence_preference`. |
| `matchedSignals` | Bounded list of signals that contributed to inclusion, such as query relevance, scope, role, confidence, authority, recency, source linkage, or active lifecycle state. |
| `components` | Existing relevance, confidence, recency, authority, and scope-match rank components. |
| `policyFit` | Safe statement that the item matched the authenticated principal, target scope, role, and namespace grant. |
| `lifecycleFit` | Whether the item is active and evidence-current. |
| `sourceEvidence` | Source event ids and links already present today. |
| `reviewSuggestedActions` | Item-level actions such as mark useful, stale, wrong, sensitive, or over-broad. |

The explanation must not reveal hidden candidates, unauthorized namespaces, raw
query text beyond the caller-supplied query, or source payloads the caller has
not explicitly read through the authorized source endpoint.

### Safe Exclusion Summaries

Packet-level exclusion summaries explain omissions without leaking hidden
memory.

Allowed exclusion reasons:

| Reason | Count Disclosure | Notes |
| --- | --- | --- |
| `inactive` | Count allowed for authorized scope. | Includes superseded, contradicted, expired, deleted, or redacted records only when count disclosure is safe. |
| `not_authorized` | Withheld by default. | May disclose only `countDisclosure = withheld`, not a count or content. |
| `scope_mismatch` | Count allowed when derived from caller-provided target scope. | Do not reveal other project names or namespaces. |
| `role_mismatch` | Count allowed only for roles the caller is assigned. | Withhold otherwise. |
| `below_rank_cutoff` | Count allowed. | Indicates candidates lost to limit or rank threshold. |
| `source_unavailable` | Count allowed. | Indicates source evidence cannot currently be linked or read. |
| `sensitive` | Withheld or bounded. | Should not identify content; may route to reviewer workflow. |

Exclusion summaries must follow the same safety posture as `memory.queryFacts`:
unauthorized content can be acknowledged only as a withheld summary when that is
safe for the caller.

### Reviewer Actions

Reviewer actions should become first-class product operations. The initial
action set should be:

| Action | Applies To | Effect |
| --- | --- | --- |
| `useful` | Item or packet | Positive signal for ranking and benchmark review. |
| `stale` | Item | Opens stale-memory review and down-ranks or suppresses the item until resolved. |
| `wrong` | Item | Opens correction or contradiction review for the underlying memory. |
| `sensitive` | Item | Opens sensitivity or redaction review; does not echo the sensitive content in logs. |
| `over_broad` | Item or packet | Indicates the item is authorized but not helpful for this target scope, role, or task. |
| `missing` | Packet | Records that needed memory was absent without storing raw query text. |

The existing `noisy` feedback type should be preserved for compatibility during
migration, but product UI and docs should prefer `over_broad` once implemented.

### Feedback To Ranking

Feedback should influence ranking through transparent, bounded signals:

- useful feedback can increase an item's authority or usefulness prior within
  the same scope, namespace, and role
- stale feedback can suppress or down-rank the item and open review
- wrong feedback can open correction or contradiction review before changing
  durable truth
- sensitive feedback can route to redaction review and exclude the item from
  normal retrieval while pending where policy requires it
- over-broad feedback can reduce scope-match weight for similar packets without
  deleting valid memory
- missing feedback can create benchmark-visible observations for recall gaps

Ranking changes must be reversible, auditable, and benchmarked. Feedback should
not rewrite memory facts directly without broker or review workflow decisions.

### Benchmark Visibility

The productization gate should improve measured outcomes, not only add UI.

Benchmark-visible success criteria:

- Memory Lift remains positive
- Contract Lift remains positive
- scoped-safety leak count remains zero
- stale-memory usage remains zero
- source-link coverage remains 100 percent for memory-derived claims
- context-packet explanations are present for included items
- safe exclusion summaries never leak unauthorized content
- feedback-driven ranking changes improve or preserve relevant task scores over
  the LR-03 baseline
- reviewer actions can produce traceable observations without raw query storage

## Implementation Backlog

| ID | Priority | Status | Item | Acceptance Criteria |
| --- | --- | --- | --- | --- |
| CP-01 | P0 | Done | Define productized context packet schema. | [Context Packet Product v1 Contract](api/context-packet-product-v1.md) defines a versioned response contract and JSON Schema with packet id, generated time, policy summary, included item explanations, exclusion summaries, review actions, feedback policy, and evaluation hints while preserving current grouped items. |
| CP-02 | P0 | Done | Add packet and item identifiers for feedback. | Context packet responses include stable packet ids and item ids; context feedback can reference `packetId` and `itemId` so item feedback no longer requires resending raw query text. |
| CP-03 | P0 | Done | Implement structured inclusion explanations. | Context packet items now return primary reason, matched signals, rank components, policy fit, lifecycle fit, source evidence, and suggested review actions; tests prove unauthorized candidate metadata stays out of the packet. |
| CP-04 | P0 | Done | Add safe exclusion summaries to context packets. | Context packets now return payload-safe excluded summaries for inactive, not-authorized, scope-mismatch, role-mismatch, rank-cutoff, source-unavailable, and sensitive omissions using disclosed counts only for safe post-policy filters and withheld disclosure for side-channel-sensitive reasons. |
| CP-05 | P0 | Todo | Expand context feedback actions. | Feedback supports useful, stale, wrong, sensitive, over-broad, and missing actions while preserving the legacy noisy path; validation requires source ids for item-level actions and stores only payload-safe metadata. |
| CP-06 | P0 | Todo | Add reviewer workflow for context observations. | Admin operators can inspect context feedback observations, open stale/wrong/sensitive reviews, and see source-linked evidence without raw query storage. |
| CP-07 | P0 | Todo | Feed reviewer actions into ranking signals. | Ranking consumes bounded usefulness, stale, wrong, sensitive, over-broad, and missing signals by scope, namespace, role, and source id; feedback can down-rank or suppress but cannot rewrite facts without review or broker decisions. |
| CP-08 | P0 | Todo | Add context-product benchmark checks. | Benchmark tasks assert inclusion explanations, safe exclusions, reviewer-action hygiene, source-link coverage, stale-memory avoidance, and before/after ranking behavior against the LR-03 baseline. |
| CP-09 | P1 | Todo | Add context product health dashboard metrics. | Operations metrics expose explanation coverage, exclusion counts by safe reason, feedback action shares, review-open counts, ranking-signal application counts, and benchmark deltas. |
| CP-10 | P1 | Todo | Update caller docs and examples. | API docs and client examples show how agents should read explanations, handle safe exclusions, submit reviewer actions, and avoid storing raw query text. |

## Migration Plan

| Phase | Description | Exit Criteria |
| --- | --- | --- |
| 0. Baseline | Current context packet and feedback endpoints remain unchanged. | Existing API, feedback, and benchmark release-gate tests pass. |
| 1. Schema contract | Add productized response schema and docs behind a version flag or additive fields. | Existing clients can ignore new fields. |
| 2. Explanation builder | Add structured inclusion explanations from existing rank components and policy metadata. | Unit and API tests prove explanations are present and payload-safe. |
| 3. Exclusion summaries | Add safe packet-level exclusions using authorized counts and withheld disclosures. | Cross-project and role-boundary tests prove no hidden content leaks. |
| 4. Feedback actions | Expand feedback actions and persist item or packet references. | Validation rejects unsafe feedback and does not store raw query text. |
| 5. Reviewer workflow | Connect feedback observations to admin review paths. | Operators can route stale, wrong, sensitive, and over-broad context safely. |
| 6. Ranking loop | Apply bounded ranking signals from reviewed feedback. | Benchmark deltas improve or preserve LR-03 gate metrics. |

Rollback should be additive: disabling productized context features should return
the API to the current context packet behavior without deleting feedback history.

## Risks

| Risk | Severity | Mitigation |
| --- | --- | --- |
| Exclusion summaries leak hidden memory. | High | Withhold unauthorized counts and never include content, namespaces, project names, or source ids the caller cannot read. |
| Feedback poisoning changes ranking unfairly. | High | Apply feedback only through bounded, auditable signals and prefer reviewed actions for suppressive effects. |
| Sensitive feedback repeats sensitive text. | High | Store source ids, action type, and hashes only; never require raw sensitive notes in the first implementation. |
| Explanations become too verbose for LLM prompts. | Medium | Keep packet explanations compact and move audit detail behind admin/source links. |
| Ranking improvements are invisible. | Medium | Require benchmark tasks and before/after deltas for each ranking-signal release. |
| Legacy `noisy` feedback conflicts with `over_broad`. | Low | Preserve `noisy` as a compatibility alias or legacy type while product docs prefer `over_broad`. |

## Success Criteria

LR-02 is successful when the team can start implementation with a clear answer
to these questions:

- Why was each memory included in a context packet?
- What was excluded, and what can be safely disclosed about that exclusion?
- Which feedback or review action should a caller use when context is useful,
  stale, wrong, sensitive, missing, or too broad?
- How do those actions affect ranking without rewriting truth unsafely?
- Which benchmark checks prove the productized context is better than the LR-03
  baseline?
