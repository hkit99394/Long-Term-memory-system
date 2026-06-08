# Project Success Observation PS-03

Date started: 2026-06-08

Status: in progress as pre-observation setup. The live observation window starts
on 2026-06-15 and ends on 2026-06-28.

Owners: Product Owner, Tester/QA, Knowledge Steward, Security Professional, and
pilot operator.

## Purpose

PS-03 runs the first project-success observation window for the Long-Term Memory
System internal dogfood pilot selected in
[Project Success Pilot Baseline PS-02](project-success-pilot-baseline-ps02.md).
It captures two weekly measurement cycles from real memory-informed project
work without treating planning-only activity as pilot success.

This record is payload-safe. It must not include raw source event payloads,
memory bodies, review notes, raw queries, provider payloads, API keys,
database connection strings, or secret values.

## Pilot Scope

| Field | Value |
| --- | --- |
| Organization | `Personal AI Systems` |
| Organization id | `9f8e7d6c-5b4a-4321-9123-abcdef123001` |
| Project | `Long-Term Memory System` |
| Project id / scope id | `9f8e7d6c-5b4a-4321-9123-abcdef123002` |
| Pilot type | Internal dogfood pilot |
| Observation window | 2026-06-15 to 2026-06-28 |
| Closeout window | 2026-06-29 to 2026-07-05 |

The observation uses the canonical project boundary, role ids, and hard no-go
gates from [Project Success Benchmark PS-01](project-success-benchmark-ps01.md).

## Cycle Calendar

| Cycle | Window | Evidence due | Required output |
| --- | --- | --- | --- |
| Cycle 1 | 2026-06-15 to 2026-06-21 | 2026-06-22 | Payload-safe weekly cycle record under `benchmarks/outputs/project-success/`. |
| Cycle 2 | 2026-06-22 to 2026-06-28 | 2026-06-29 | Payload-safe weekly cycle record under `benchmarks/outputs/project-success/`. |

Use
[`benchmarks/project-success/weekly-cycle-template.json`](../benchmarks/project-success/weekly-cycle-template.json)
as the per-cycle capture template. Generated filled cycle records stay under
`benchmarks/outputs/project-success/` unless a curated summary is intentionally
attached to pilot evidence.

## Start Gate

| Gate | Status on 2026-06-08 | Required before Cycle 1 |
| --- | --- | --- |
| PS-01 scorecard contract | Done | Keep the scorecard targets and hard no-go gates unchanged unless Product Owner accepts a new baseline. |
| PS-02 pilot baseline | Done | Keep the Long-Term Memory System dogfood scope active. |
| Observation dates | Ready | Do not count pilot success metrics before 2026-06-15. |
| Active roles | Ready | Product Owner, Tester/QA, Knowledge Steward, Security Professional, and pilot operator must remain available. |
| Memory prework evidence | Pending | Confirm `memory.getContext`, `memory.queryFacts`, and feedback recording work for the pilot scope before the first memory-informed PS-03 work item. |
| Operations readiness | Pending | Capture a fresh `/health/ready` and `/api/operations/summary` snapshot at Cycle 1 start. |

If memory prework or operations readiness cannot be confirmed by 2026-06-15,
the pilot remains in setup and the missing readiness evidence must be called out
instead of inferred.

## Measurement Workflow

For each memory-informed work item during the observation window:

1. Record a payload-safe work-item id, date, owner role, and one-line task
   summary.
2. Run the repo memory prework loop for the project scope:
   `memory.getContext`, `memory.queryFacts`, then retain only packet, item,
   source, and artifact references needed for feedback.
3. Complete the work item and record context feedback for each used or missing
   memory signal.
4. Count whether repeated context questions were avoided, repeated, or still
   missing.
5. Keep raw memory payloads, raw queries, review notes, and secret values out of
   committed evidence.

At the end of each weekly cycle:

1. Capture memory-quality metrics from `/api/operations/summary`.
2. Run the weekly admin review workflow and record payload-safe output links.
3. Run the access-boundary review and record payload-safe output links.
4. Run source-backed memory hygiene and backlog/roadmap memory sync checks.
5. Record feedback coverage, review SLA status, confidence ratings, manual
   evidence steps, maintainer interventions, and missed decisions.
6. Fill one weekly cycle JSON record from the PS-03 template.

## Metrics To Capture

| Area | Metrics | Source |
| --- | --- | --- |
| Adoption | active memory users or agents, memory-informed work items, feedback coverage | Work-item ledger and context feedback records. |
| Memory quality | source-link coverage, useful feedback rate, stale-memory rate, missing-memory reports, duplicate ratio | `/api/operations/summary` and weekly admin review. |
| Trust and safety | scoped safety leaks, stale-memory benchmark usage, raw payload leakage, access-boundary review completion | Access review, benchmark release gate, and evidence audit. |
| Operator burden | critical review resolution age, oldest critical review age, manual evidence steps, maintainer interventions | Weekly admin review and operator notes. |
| Project delivery impact | repeated context questions avoided, missed decisions, first-pass task completion, onboarding time if relevant | Product Owner weekly review. |
| Human confidence | pilot user confidence, operator confidence, explainability pass rate | Weekly numeric rating and short payload-safe note. |

## Hard No-Go Checks

The pilot cannot continue toward a PS-04 `GO` recommendation if any weekly cycle
shows:

- scoped safety leaks above zero
- stale-memory benchmark usage above zero
- source-link coverage below 100 percent for active memory-derived claims
- raw payload leakage in committed scorecard or cycle evidence
- unresolved critical stale, wrong, sensitive, or over-broad memory after 2
  business days
- missed weekly access-boundary review

## PS-04 Handoff

PS-04 closeout preparation may start before the observation window, but the
final recommendation must wait until 2026-06-29 or later. Completion requires
both weekly cycle records, or an explicit Product Owner record of why a missing
cycle should force `WATCH` or `NO-GO`. The closeout must use
[`benchmarks/project-success/scorecard-template.json`](../benchmarks/project-success/scorecard-template.json)
and
[`benchmarks/project-success/closeout-template.json`](../benchmarks/project-success/closeout-template.json),
then produce a payload-safe GO/WATCH/NO-GO recommendation by 2026-07-05.
