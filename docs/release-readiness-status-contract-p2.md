# Release Readiness Status Contract P2

Date: 2026-06-04

Status: complete for the external-pilot readiness gate.

## Purpose

P2 extracts the external-pilot readiness state into one machine-readable status
contract so docs, tests, future CI, and the P3 admin cockpit can read the same
gate names, statuses, evidence links, blockers, and next recommended work.

Before this contract, the EPR state lived in prose across the backlog, runbook,
evidence review, go/no-go record, and release checklist. Those docs remain the
human-readable record, but this status file is the structured source to consume
in code.

## Files

| File | Purpose |
| --- | --- |
| `docs/external-pilot-readiness-status.json` | Canonical current status for EPR gates, external invite decision, missing GO inputs, and next recommended work. |
| `docs/external-pilot-readiness-status.schema.json` | JSON Schema for the status contract. |
| `tests/MemorySystem.UnitTests/ReleaseReadinessStatusContractP2Tests.cs` | Guard test that verifies the contract, docs, and backlog stay linked. |

## Current Snapshot

| Gate | Status | External invite impact |
| --- | --- | --- |
| EPR-01 | Done | Runbook exists. |
| EPR-02 | Done | Local pilot-equivalent deployment smoke passed. |
| EPR-03 | Done | Local payload-safe release evidence attached. |
| EPR-04 | Done | External invite approved by v1.0.0 GO replacement. |
| EPR-05 | Done | P1 docs truth cleanup completed. |
| EPR-06 | Done | P2 status contract extracted. |
| EPR-07 | Done | P3 pilot operator cockpit added. |

The current decision is `go` and `externalInviteApproved` is `true`.

## Consumer Contract

Consumers should treat `docs/external-pilot-readiness-status.json` as the
canonical structured status for:

- rendering a pilot readiness dashboard
- deciding whether the external invite is approved
- listing post-GO evidence hardening work
- showing current GO/blocker state in CI or release automation
- linking operators back to the human-readable evidence records

The JSON status does not replace target-environment evidence, alert
acknowledgement, or the human-readable go/no-go record. It records the current
decision and whether those inputs are blockers or post-GO hardening work.

## P3 Consumer

P3 now builds the pilot operator cockpit on top of
`docs/external-pilot-readiness-status.json`. The cockpit and endpoint now render
the v1.0.0 GO decision and the remaining post-GO evidence hardening work.
