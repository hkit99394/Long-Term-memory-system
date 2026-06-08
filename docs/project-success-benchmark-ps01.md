# Project Success Benchmark PS-01

Date started: 2026-06-08

Status: implemented as the pilot scorecard contract. The first live pilot run is
follow-on execution work.

Owners: Product Owner, Tester/QA, Knowledge Steward, and pilot operator.

## Purpose

PS-01 defines the benchmark that answers the next product question:

```text
Does governed long-term memory improve real project work?
```

The existing release gates prove that the memory service is technically safe and
useful in controlled benchmark tasks. PS-01 adds the product-success layer:
adoption, memory quality, trust and safety, operator burden, project delivery
impact, and human confidence during a real pilot project.

## Timeframe

| Window | Dates | Target |
| --- | --- | --- |
| Planning | 2026-06-08 to 2026-06-14 | Select the pilot project, baseline current workflow pain, and approve the scorecard. |
| Pilot observation | 2026-06-15 to 2026-06-28 | Run two weekly measurement cycles with real memory-informed work. |
| Closeout | 2026-06-29 to 2026-07-05 | Produce the first project-success scorecard and promotion recommendation. |

## Benchmark Areas

Canonical areas: adoption, memory quality, trust and safety, operator burden,
project delivery impact, and human confidence.

| Area | Question | Primary Signals |
| --- | --- | --- |
| Adoption | Is the pilot actually using memory? | Onboarding time, active users or agents, memory-informed work items, feedback coverage. |
| Memory quality | Is remembered context useful and current? | Source-link coverage, useful feedback rate, stale rate, missing reports, duplicate ratio. |
| Trust and safety | Did memory stay scoped, auditable, and removable? | Scoped leaks, stale-memory benchmark usage, payload leaks, correction SLA, access review completion. |
| Operator burden | Can the team operate the system without bespoke maintainer help? | Review queue age, critical review resolution time, manual evidence steps, maintainer interventions. |
| Project delivery impact | Did project work improve? | Repeated context questions, missed decisions, first-pass task completion, contributor onboarding time. |
| Human confidence | Do pilot users and operators trust the system? | User confidence rating, operator confidence rating, explainability pass rate. |

## First-Pilot Targets

The first pilot should not optimize for broad adoption. It should prove one
project can use memory repeatedly without safety regressions.

| Metric | Target | Notes |
| --- | --- | --- |
| Source-link coverage | 100 percent | Applies to active memory-derived project claims. |
| Scoped safety leaks | 0 | Release-blocking. |
| Stale-memory benchmark usage | 0 | Release-blocking. |
| Raw payload leakage in scorecard evidence | 0 | Scorecard must remain payload-safe. |
| Critical review resolution | 2 business days or less | Applies to stale, wrong, sensitive, or over-broad critical items. |
| Feedback coverage | 70 percent or more | Share of memory-informed work items with context feedback. |
| Weekly access-boundary review | 100 percent complete | Required for pilot continuation. |
| Weekly memory review | 100 percent complete | Required for pilot continuation. |
| Pilot user confidence | 4 out of 5 or better | Human rating after each weekly cycle. |
| Operator confidence | 4 out of 5 or better | Operator rating after each weekly cycle. |

## Scorecard Contract

The scorecard artifact kind is:

```text
memorysystem.project_success_scorecard
```

The scorecard must be payload-safe:

- `payloadSafe` is `true`
- `rawSourcePayloadsIncluded` is `false`
- raw source event payloads, memory bodies, review notes, queries, API keys,
  provider payloads, database connection strings, and secret values are excluded
- project claims link to source documents, release evidence, benchmark reports,
  operations summaries, or admin review records instead of copying raw payloads

The template lives at
[`benchmarks/project-success/scorecard-template.json`](../benchmarks/project-success/scorecard-template.json).

## Scorecard Inputs

PS-01 reuses existing product and operations surfaces instead of inventing a
parallel measurement system:

| Input | Source |
| --- | --- |
| Memory quality metrics | `/api/operations/summary`, `/api/operations/metrics`, and [Memory Quality Metrics IP-15](memory-quality-metrics-ip15.md). |
| Context feedback | `memory.recordContextFeedback` observations and weekly admin review output. |
| Access safety | [Access Boundary Review IP-11](access-boundary-review-ip11.md) and permission-drift reports. |
| Onboarding | [Project Onboarding Runbook IP-16](project-onboarding-runbook-ip16.md). |
| Release safety | [Benchmark Release Gate LR-03](benchmark-release-gate-lr03.md) and target evidence records. |
| Operator evidence | Weekly review queue, compliance status, backup/restore status, alert acknowledgement, and release evidence bundles. |
| Human confidence | Weekly Product Owner and operator ratings recorded as payload-safe numeric scores and notes. |

## Pass, Watch, And No-Go Rules

The first pilot scorecard should produce one of three recommendations:

| Recommendation | Meaning |
| --- | --- |
| `GO` | The pilot met hard safety gates and enough adoption/confidence targets to continue or promote. |
| `WATCH` | No hard safety gate failed, but adoption, confidence, delivery impact, or operator burden needs another cycle. |
| `NO-GO` | A hard safety gate failed, or the operator cannot explain, correct, remove, or recover memory with confidence. |

Hard no-go conditions:

- scoped safety leaks above zero
- stale-memory benchmark usage above zero
- source-link coverage below 100 percent for active memory-derived claims
- raw payload leakage in committed scorecard evidence
- unresolved critical stale, wrong, sensitive, or over-broad memory after 2
  business days
- weekly access-boundary review missed during the pilot window

## Implementation Backlog

| ID | Priority | Status | Item | Acceptance Criteria |
| --- | --- | --- | --- | --- |
| PS-01 | P0 | Done | Define pilot project success scorecard. | This document, the benchmark folder, and the JSON scorecard template define target dates, metrics, hard gates, payload-safety rules, and first-pilot targets. |
| PS-02 | P0 | Todo | Select first pilot project and baseline. | Pilot owner, project scope, active roles, current repeated-context pain, and baseline workflow metrics are recorded in payload-safe Markdown. |
| PS-03 | P0 | Todo | Run two-week pilot observation. | Two weekly scorecard cycles are captured from real memory-informed work between 2026-06-15 and 2026-06-28. |
| PS-04 | P0 | Todo | Produce closeout and promotion recommendation. | By 2026-07-05, Product Owner records GO/WATCH/NO-GO, evidence links, residual risks, and next action. |
