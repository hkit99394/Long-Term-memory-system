# Project Success Pilot Baseline PS-02

Date: 2026-06-08

Status: first pilot project selected and baseline recorded. PS-03 has started
the observation runbook; the live observation window starts on 2026-06-15.

Owners: Product Owner, Tester/QA, Knowledge Steward, Security Professional, and
pilot operator.

## Purpose

PS-02 selects the first project-success pilot and records the baseline before
the two-week observation window. It keeps the first pilot deliberately narrow:
dogfood the Long-Term Memory System repository itself before widening to another
project.

This record is payload-safe. It does not include raw source event payloads,
memory bodies, review notes, queries, API keys, database connection strings, or
secret values.

## Selected Pilot

| Field | Value |
| --- | --- |
| Organization | `Personal AI Systems` |
| Organization id | `9f8e7d6c-5b4a-4321-9123-abcdef123001` |
| Project | `Long-Term Memory System` |
| Project id / scope id | `9f8e7d6c-5b4a-4321-9123-abcdef123002` |
| Pilot type | Internal dogfood pilot |
| Planning window | 2026-06-08 to 2026-06-14 |
| Observation window | 2026-06-15 to 2026-06-28 |
| Closeout due | 2026-07-05 |

The selected pilot uses the canonical project memory boundary from
[Project Memory Boundary](project-memory-boundary.md). The project id is the
target `scopeId` for shared project memory and role-lens checks.

## Active Roles

The first pilot uses the default operating roles already seeded for this
repository:

| Role id | Pilot responsibility |
| --- | --- |
| `product_owner` | Own project-success scorecard, GO/WATCH/NO-GO closeout, and backlog priority. |
| `cto` | Check architecture continuity, technical risk, and platform direction. |
| `security_professional` | Verify scoped access, payload safety, and access-boundary review. |
| `it_manager` | Operate health, backup/restore, alert, and runtime evidence checks. |
| `developer` | Use memory during implementation and record context feedback. |
| `tester_qa` | Verify benchmark, test, and scorecard evidence. |
| `release_manager` | Keep release evidence and promotion boundary clear. |
| `knowledge_steward` | Review stale, wrong, duplicate, missing, and source-drift memory signals. |

## Baseline Pains

The first pilot should test whether memory reduces these current project pains:

| Pain | Baseline observation | Measurement during PS-03 |
| --- | --- | --- |
| Repeated status reconstruction | The project has many completed gates, and the next product target needs to be re-established from roadmap, backlog, and release evidence. | Count repeated context questions avoided or still asked. |
| Technical-success versus project-success ambiguity | Existing release gates prove technical quality, but not whether a real project benefits. | Track project-success scorecard completion and confidence ratings. |
| Release-truth drift risk | Several docs can become stale when backlog or release status changes. | Run roadmap/backlog sync and source-backed memory hygiene during weekly review. |
| Manual review burden | Memory review, access review, evidence packaging, and scorecard closeout require operator discipline. | Track critical review age, manual evidence steps, and maintainer interventions. |
| Feedback loop incompleteness | Memory-informed work can happen without context feedback unless the wrapper habit is enforced. | Track feedback coverage against memory-informed work items. |

## Baseline Metrics

These are the starting values for the PS-03 observation window. Values marked
`pending` must be sampled during the first weekly cycle rather than inferred
from planning docs.

| Area | Metric | Baseline |
| --- | --- | --- |
| Adoption | Selected pilot project | Long-Term Memory System internal dogfood pilot. |
| Adoption | Active pilot roles | 8 default operating roles. |
| Adoption | Memory-informed work items in PS window | 0 before 2026-06-15. |
| Adoption | Feedback coverage | Pending first weekly sample. |
| Memory quality | Source-link coverage | Target is 100 percent; sample from `/api/operations/summary` during PS-03. |
| Memory quality | Stale-memory rate | Pending first weekly sample. |
| Memory quality | Missing-memory reports | Pending first weekly sample. |
| Trust and safety | Scoped safety leaks | Must remain 0. |
| Trust and safety | Raw payload leakage in scorecard evidence | Must remain 0. |
| Trust and safety | Weekly access-boundary review | First PS cycle pending. |
| Operator burden | Critical review resolution SLA | Target is 2 business days or less. |
| Operator burden | Maintainer interventions | Start at 0 for the PS observation window. |
| Project delivery impact | Repeated context questions avoided | Pending weekly manual count. |
| Project delivery impact | Missed decision count | Pending weekly manual count. |
| Human confidence | Pilot user confidence | Not rated before observation. |
| Human confidence | Operator confidence | Not rated before observation. |

## Evidence To Collect

During PS-03, collect payload-safe links or artifact paths for:

- `/api/operations/summary` memory-quality snapshot
- weekly admin review workflow output
- weekly access-boundary review output
- benchmark release-gate result when relevant
- project-success scorecard draft under `benchmarks/outputs/`
- Product Owner and operator confidence ratings
- short notes on repeated-context questions avoided, missed decisions, and manual
  evidence work

## PS-03 Entry Criteria

PS-03 may start when:

- this PS-02 baseline is committed
- the pilot project scope and roles above remain active
- the Product Owner accepts the first-pilot hard no-go gates from
  [Project Success Benchmark PS-01](project-success-benchmark-ps01.md)
- weekly memory review and access-boundary review owners are available for the
  2026-06-15 to 2026-06-28 window

If any entry criterion is not true by 2026-06-14, PS-03 should be delayed rather
than treating an unmeasured pilot as success evidence.

The PS-03 observation runbook is
[Project Success Observation PS-03](project-success-observation-ps03.md).
