# External Pilot Go/No-Go EPR-04

Date: 2026-06-04

Status: historical NO-GO record; superseded for the current decision by
[External Pilot GO EPR-04 v1.0.0](external-pilot-go-epr04-v1.0.0-2026-06-04.md).

Current decision update: version 1.0.0 is marked GO for external pilot by owner
approval in the workspace on 2026-06-04. This document remains the historical
NO-GO record that explains the prior blocker and the evidence requirements that
were accepted as post-GO hardening work.

## Decision

Decision: NO-GO

The first external pilot user must not be invited yet.

## Decision Reason

EPR-02 and EPR-03 have strong local pilot-equivalent evidence, but EPR-04 cannot
truthfully approve an external invite from the repository alone. The current
workspace does not contain:

- a controlled `release_evidence_bucket` prefix or equivalent audit-store upload
- real alert receiver acknowledgement for the pilot route
- a named release owner signature
- a named rollback owner signature
- a final rollback boundary and communication route signed by those owners

These are required by the P0 target-environment rehearsal before any external
pilot user is invited.

## Evidence Reviewed

| Evidence | Result |
| --- | --- |
| Target-environment runbook | [Target-Environment Pilot Rehearsal P0](target-environment-pilot-rehearsal-p0.md) defines the gates, pass/fail criteria, evidence bundle, and signature template. |
| Local pilot-equivalent evidence | [Pilot Release Evidence EPR-03](pilot-release-evidence-epr03-2026-06-04.md) records passed deployment smoke, benchmark gate, live agent-contract smoke, governance/compliance smoke, backup/restore validation, and alert artifact validation. |
| Pilot readiness review | [Pilot Readiness Evidence Review](pilot-readiness-evidence-review-2026-06-01.md) records the earlier no-go until target-environment evidence is produced. |
| Release checklist | [Production Release Checklists PI-07](production-release-checklists-pi07.md) requires final benchmark, governance/compliance, alert routing, rollback owner, and go/no-go evidence in the controlled audit store. |

## Gate Results

| Gate | Result | Notes |
| --- | --- | --- |
| Environment preflight | NO-GO | Pilot account, region, network boundary, controlled evidence prefix, and owner list are not present in the workspace. |
| Terraform/platform validation | NO-GO | No exact target-environment inputs were available for validation in this EPR-04 record. |
| Deployment smoke | Passed locally | Local pilot-equivalent deployment smoke passed in EPR-03. Target account execution is not attached. |
| Metrics and tracing | Passed locally | Operations metrics and observability artifact smoke passed. Target exporter status is not attached. |
| Benchmark gate | Passed locally | Fixture-backed gate passed; fresh live eight-task agent-contract smoke passed. Fresh pilot-model scorecards are not attached. |
| Governance/compliance | Passed locally | GC-08 release smoke passed locally. Target audit-store package is not attached. |
| Backup/restore | Passed locally | Local backup/export, restore validation, and pgvector verification passed. Managed backup/PITR evidence is not attached. |
| Alert receiver acknowledgement | NO-GO | Artifact route validation passed, but real receiver acknowledgement is not available in this workspace. |
| Evidence upload | NO-GO | No controlled `release_evidence_bucket` prefix or equivalent immutable audit-store upload is attached. |
| Owner signoff | NO-GO | Release owner and rollback owner signatures are not available in this workspace. |

## Go/No-Go Record

```text
Target-environment pilot rehearsal id: EPR-04-2026-06-04
Date/time UTC: 2026-06-04
Pilot environment: Not provided in workspace
Image digest: Not provided in workspace
Database target: Local pilot-equivalent PostgreSQL evidence only
Evidence prefix: Not provided; controlled audit-store upload pending
Release owner: Not provided
Rollback owner: Not provided
Alert-route owner: Not provided
Benchmark scorer: Not provided
Governance reviewer: Not provided

Gate results:
- Environment preflight: NO-GO
- Terraform/platform validation: NO-GO
- Deployment smoke: Passed locally; target execution not attached
- Metrics and tracing: Passed locally; target exporter evidence not attached
- Benchmark gate: Passed locally; fresh pilot-model scorecards not attached
- Governance/compliance: Passed locally; target audit-store package not attached
- Backup/restore: Passed locally; managed backup/PITR evidence not attached
- Alert receiver acknowledgement: NO-GO
- Evidence upload: NO-GO

Decision: NO-GO
Decision reason: Required target-environment evidence, controlled evidence prefix,
real alert receiver acknowledgement, rollback boundary, communication route, and
owner signatures are not available in the repository workspace.
Rollback boundary: Not signed
Communication route: Not signed
Release owner signature: Not signed
Rollback owner signature: Not signed
```

## Required To Flip To GO

EPR-04 can be replaced by a GO record only after all of these are attached:

1. Controlled evidence prefix under `release_evidence_bucket` or equivalent
   immutable audit store.
2. Target-environment deployment smoke or platform equivalent for migrator, API,
   worker, health, authenticated read/write, metrics, backup/restore, and
   rollback.
3. Fresh benchmark scorecards for the intended pilot model and fixture set.
4. Governance/compliance smoke or platform equivalent against the target
   database and evidence store.
5. Real alert receiver acknowledgement for the pilot route.
6. Signed rollback boundary and communication route.
7. Release owner and rollback owner signatures.

Until that replacement GO record exists, the correct external-pilot decision is
NO-GO.

## Recommended Next Work

1. P1 docs truth cleanup completed: [Documentation Truth Cleanup P1](documentation-truth-cleanup-p1-2026-06-04.md)
   reconciles the roadmap, backlog, release checklist, and readiness pages around
   the current state.
2. P2 release-readiness status contract completed: [Release Readiness Status Contract P2](release-readiness-status-contract-p2.md)
   and `external-pilot-readiness-status.json` centralize gate names, statuses,
   evidence links, blockers, missing GO inputs, and next recommended work.
3. P3 pilot operator cockpit completed: [Pilot Operator Cockpit P3](pilot-operator-cockpit-p3.md)
   adds an authenticated admin view and API endpoint that surface the EPR gates,
   evidence links, current NO-GO reason, and signed GO replacement checklist in
   one place.
