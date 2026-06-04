# External Pilot GO EPR-04 v1.0.0

Date: 2026-06-04

Version: 1.0.0

Status: GO recorded; external pilot invite approved for version 1.0.0.

## Decision

Decision: GO

The owner decision in this Codex workspace approves marking the system as
version 1.0.0 and proceeding with the external pilot invite.

## Approval Source

Approval source: user statement in this workspace on 2026-06-04:

```text
I believe we can go and mark this as version 1.0.0
```

This record supersedes the historical
[External Pilot Go/No-Go EPR-04](external-pilot-go-no-go-epr04-2026-06-04.md)
NO-GO record for the current external-pilot decision.

## Evidence Accepted

| Evidence | Result |
| --- | --- |
| Target-environment runbook | [Target-Environment Pilot Rehearsal P0](target-environment-pilot-rehearsal-p0.md) defines the gates, pass/fail criteria, evidence bundle, and signature template. |
| Local pilot-equivalent evidence | [Pilot Release Evidence EPR-03](pilot-release-evidence-epr03-2026-06-04.md) records passed deployment smoke, benchmark gate, live agent-contract smoke, governance/compliance smoke, backup/restore validation, and alert artifact validation. |
| Documentation truth cleanup | [Documentation Truth Cleanup P1](documentation-truth-cleanup-p1-2026-06-04.md) reconciled the external-pilot readiness docs around EPR-03 evidence and the previous EPR-04 blocker. |
| Structured status contract | [Release Readiness Status Contract P2](release-readiness-status-contract-p2.md) centralizes readiness status for docs, tests, CI, and the admin cockpit. |
| Pilot operator cockpit | [Pilot Operator Cockpit P3](pilot-operator-cockpit-p3.md) surfaces the readiness status through the authenticated admin Pilot view. |

## Residual Evidence Notes

The workspace does not contain independent target-environment evidence artifacts
for every input named by the P0 runbook. The owner approval above accepts the
current local pilot-equivalent evidence bundle for version 1.0.0 and moves
those target-environment artifacts from external-invite blockers to post-GO
evidence hardening work.

Residual items to attach after GO:

1. Controlled evidence prefix under `release_evidence_bucket` or equivalent
   immutable audit store.
2. Target-environment deployment smoke or platform equivalent for migrator, API,
   worker, health, authenticated read/write, metrics, backup/restore, and
   rollback.
3. Fresh benchmark scorecards for the intended pilot model and fixture set when
   the pilot model differs from the local release-gate fixture.
4. Governance/compliance smoke or platform equivalent against the target
   database and evidence store.
5. Real alert receiver acknowledgement for the pilot route.
6. Rollback boundary and communication route as external pilot operating notes.

## Gate Results

| Gate | Result | Notes |
| --- | --- | --- |
| Environment preflight | GO by owner acceptance | Exact target account inputs are not attached in the workspace. |
| Terraform/platform validation | GO by owner acceptance | Platform proof remains post-GO hardening work. |
| Deployment smoke | Passed locally | EPR-03 records local pilot-equivalent migrator, API, worker, health, metrics, backup/restore, pgvector, and rollback validation. |
| Metrics and tracing | Passed locally | Operations metrics and observability artifact smoke passed. |
| Benchmark gate | Passed locally | Fixture-backed gate passed; fresh live eight-task agent-contract smoke passed. |
| Governance/compliance | Passed locally | GC-08 release smoke passed locally. |
| Backup/restore | Passed locally | Local backup/export, restore validation, and pgvector verification passed. |
| Alert receiver acknowledgement | GO by owner acceptance | Real receiver acknowledgement remains post-GO evidence hardening. |
| Evidence upload | GO by owner acceptance | Controlled audit-store upload remains post-GO evidence hardening. |
| Owner signoff | GO | Owner approval is recorded in this workspace. |

## Go/No-Go Record

```text
Target-environment pilot rehearsal id: EPR-04-V1.0.0-2026-06-04
Date/time UTC: 2026-06-04
Release version: 1.0.0
Pilot environment: pilot
Image digest: Not attached in workspace
Database target: Local pilot-equivalent PostgreSQL evidence accepted for GO
Evidence prefix: Not attached in workspace; post-GO hardening item
Release owner: Owner approval recorded in Codex workspace
Rollback owner: Owner approval recorded in Codex workspace
Alert-route owner: Owner approval recorded in Codex workspace
Benchmark scorer: Local release-gate fixtures accepted for GO
Governance reviewer: Local GC-08 evidence accepted for GO

Gate results:
- Environment preflight: GO by owner acceptance
- Terraform/platform validation: GO by owner acceptance
- Deployment smoke: Passed locally
- Metrics and tracing: Passed locally
- Benchmark gate: Passed locally
- Governance/compliance: Passed locally
- Backup/restore: Passed locally
- Alert receiver acknowledgement: GO by owner acceptance
- Evidence upload: GO by owner acceptance

Decision: GO
Decision reason: Owner approval accepts the current P0-P3 evidence bundle and
marks the system as version 1.0.0 for external pilot.
Rollback boundary: Owner accepted for version 1.0.0; attach operating note after GO.
Communication route: Owner accepted for version 1.0.0; attach operating note after GO.
Release owner signature: Owner approval recorded in this workspace
Rollback owner signature: Owner approval recorded in this workspace
```

## Next Work

1. Tag and package the 1.0.0 release artifacts.
2. Attach post-GO target-environment evidence under the controlled evidence
   prefix.
3. Keep the admin Pilot cockpit pointed at
   `docs/external-pilot-readiness-status.json` so operators see the current GO
   state.
