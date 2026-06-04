# Documentation Truth Cleanup P1

Date: 2026-06-04

Status: first pass complete for external-pilot readiness docs.

## Purpose

This P1 cleanup reconciles the documentation after the P0 external-pilot
readiness work. At the time of the P1 pass, the state was:

- EPR-01 is done: the target-environment rehearsal runbook exists.
- EPR-02 is done locally: pilot-equivalent deployment smoke passed.
- EPR-03 is done locally: payload-safe release evidence is attached.
- EPR-04 was blocked with a formal NO-GO record.
- The first external pilot user was blocked until a signed GO replacement record
  existed.

2026-06-04 v1.0.0 update: [External Pilot GO EPR-04 v1.0.0](external-pilot-go-epr04-v1.0.0-2026-06-04.md)
now supersedes that prior NO-GO state for the current release decision.

## Canonical Source Of Truth

| Topic | Source |
| --- | --- |
| Rehearsal gates and pass/fail criteria | [Target-Environment Pilot Rehearsal P0](target-environment-pilot-rehearsal-p0.md) |
| Local pilot-equivalent evidence | [Pilot Release Evidence EPR-03](pilot-release-evidence-epr03-2026-06-04.md) |
| Current external-pilot decision | [External Pilot GO EPR-04 v1.0.0](external-pilot-go-epr04-v1.0.0-2026-06-04.md) |
| Historical external-pilot NO-GO | [External Pilot Go/No-Go EPR-04](external-pilot-go-no-go-epr04-2026-06-04.md) |
| Machine-readable readiness status | [Release Readiness Status Contract P2](release-readiness-status-contract-p2.md) and `external-pilot-readiness-status.json` |
| Backlog state | [Backlog](backlog.md) |
| Release checklist expectations | [Production Release Checklists PI-07](production-release-checklists-pi07.md) |

## Cleanup Applied

| Document | P1 truth update |
| --- | --- |
| `docs/backlog.md` | Marks EPR-04 as blocked, adds this P1 cleanup item, and points the current focus at a signed GO replacement rather than another generic rehearsal. |
| `docs/roadmap.md` | Keeps the legacy target-rehearsal milestone wording for compatibility, then points at EPR-03 evidence and the EPR-04 NO-GO record. |
| `docs/product-improvement-plan.md` | Keeps the historical "next move should be a target-environment pilot rehearsal" phrase, then records the EPR-03/EPR-04 update. |
| `docs/pilot-readiness-evidence-review-2026-06-01.md` | Preserves the original review and adds the EPR-03/EPR-04 updates. |
| `docs/production-platform-rehearsal-pi08.md` | Replaces stale next-step language with the current EPR-03 local evidence and EPR-04 NO-GO state. |
| `docs/governance-compliance-release-smoke-gc08.md` | Updates the handoff from "next move is rehearsal" to "rehearsal evidence exists; EPR-04 is blocked." |
| `docs/governance-compliance-gate-lr06.md` | Keeps the original exit-criteria phrase for the LR-06 planning chain, then adds the current EPR-03/EPR-04 truth update. |
| `docs/benchmark-release-gate-lr03.md` | Updates the old live-smoke caveat to point at the fresh eight-task EPR-03 smoke while keeping the need for fresh pilot-model scorecards. |
| `docs/production-release-checklists-pi07.md` | Points future external pilot handoff at the EPR-04 GO replacement requirements. |

## Remaining Watchlist

Some older docs intentionally retain historical phrases such as
"target-environment pilot rehearsal" because existing contract tests use those
phrases to prove the earlier planning chain is still linked. Those docs now have
nearby dated updates that point to the current v1.0.0 GO state.

P2 now extracts a single release-readiness status contract so docs, admin UI, and
tests do not duplicate EPR gate names, statuses, and caveats. P3 then built the
pilot operator cockpit on that contract.
