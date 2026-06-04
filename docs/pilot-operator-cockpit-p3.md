# Pilot Operator Cockpit P3

Date: 2026-06-04

Status: first cockpit slice complete.

## Purpose

P3 adds an operator-facing cockpit for the external-pilot readiness state. The
cockpit is intentionally driven by the P2 machine-readable status contract
instead of duplicating EPR gate state in the UI.

## Operator Surface

| Surface | Contract |
| --- | --- |
| API endpoint | `GET /api/admin/pilot/readiness` |
| Admin view | `/admin/` with the `Pilot` view selected |
| Source status | `docs/external-pilot-readiness-status.json` |
| Auth | Same authenticated admin API key/OIDC path as the existing admin console |
| Payload safety | Response includes only gate ids, statuses, evidence paths, payload-safety state, and post-GO hardening work; no raw source payloads are included. |

## Cockpit Behavior

The first slice shows:

- current decision: `go`
- `externalInviteApproved: true`
- EPR-01 through EPR-07 gates
- EPR-04 as the owner-approved v1.0.0 GO gate
- evidence links for each gate
- post-GO evidence hardening work after the EPR-04 v1.0.0 GO replacement
- canonical documents and next recommended work

## Verification

The cockpit is guarded by:

- `ApiAdminPilotReadinessTests`
- `PilotOperatorCockpitP3Tests`
- `ReleaseReadinessStatusContractP2Tests`

## Next Work

The next external-pilot action is attaching post-GO target-environment evidence,
real alert receiver acknowledgement, rollback boundary, and communication route
under the controlled evidence trail.
