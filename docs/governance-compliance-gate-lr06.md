# LR-06 Governance And Compliance Gate

Date: 2026-06-01

Status: Planning accepted

## Goal

LR-06 scopes the governance and compliance gate after enterprise access,
context productization, Domain extraction, and platform integration are stable.
The goal is to turn the existing legal hold, erasure, retention reporting,
audit export, backup/restore, and platform evidence paths into an implementable
compliance track without weakening deletion, authorization, or payload-safety
guarantees.

This is a scoping slice. It does not introduce schema changes, endpoints,
runtime jobs, or new provider integrations.

The key invariant is:

```text
Compliance automation can preserve, report, restore, or erase evidence only
through the same local policy, namespace grants, legal hold, erasure, and audit
boundaries already enforced by the service.
```

## Non-Goals

- Do not implement new migrations, APIs, workers, admin UI, or Terraform
  resources in this slice.
- Do not add a legal-opinion engine or claim regulatory certification.
- Do not make backup deletion a substitute for live erasure records.
- Do not let platform policy override local membership, role assignment,
  namespace grant, legal hold, or erasure decisions.
- Do not export raw memory, source payloads, review notes, queries, API keys, or
  secret values as part of compliance evidence.

## Current Architecture Evidence

The repo already has the core ingredients for a compliance implementation:

- [Retention Policy](retention-policy.md) defines retention classes,
  sensitivity handling, legal hold precedence, erasure workflow, audit
  preservation, and remaining automation gaps.
- [Backup and Restore Runbook](backup-restore.md) documents backup sensitivity,
  restore validation, and the need to replay later redaction and erasure actions
  when restoring old backups.
- [Enterprise Access Gate](enterprise-access-gate.md) and
  [Enterprise Access Pilot Operator Runbook](enterprise-access-pilot-operator-runbook.md)
  define identity binding, service-account lifecycle, access-management review,
  audit export, rollback, and break-glass handling.
- [Production Platform Rehearsal PI-08](production-platform-rehearsal-pi08.md)
  records migration, health, metrics, benchmark, backup/restore, rollback, and
  audit evidence from the isolated platform rehearsal.
- `PostgresAdminGovernanceStore` implements authenticated legal hold,
  erasure execution, and retention reporting over authorized source events and
  derived copies.
- `PostgresAdminAuditExportStore` and `/api/admin/audit-exports` provide
  scoped payload-safe audit exports with manifest hashes.

The visible gaps are broader compliance automation: environment-specific policy,
data residency declarations, backup erasure replay, permission-drift reporting,
external payload-store checks, and a repeatable evidence package for pilot or
production reviews.

## Target Compliance Boundary

| Area | Application owns | Platform owns | Operator owns |
| --- | --- | --- | --- |
| Data residency | Policy metadata, scope/namespace tags, export-safe evidence | Region, storage location, backup/evidence bucket placement, provider controls | Environment approval and exception records |
| Erasure replay | Redaction ledger, erasure action records, replay validation logic | Backup inventory, restore validation target, evidence storage | Approval, legal-hold check, replay acceptance |
| Retention policy | Retention classes, sensitivity rules, minimization jobs, report queries | Schedules, storage lifecycle, external payload store configuration | Window approval, override and hold decisions |
| Permission drift | Effective-access preview semantics, role/grant/membership queries | Scheduled report execution and delivery | Review, signoff, remediation assignment |
| Compliance evidence | Payload-safe manifests, hashes, counts, ids, status values | Durable evidence upload, retention, and access controls | Release or audit package approval |

The compliance layer can observe and report policy state, but durable authority
remains PostgreSQL plus the existing authorization and governance workflows.

## Gate Scope

LR-06 covers the implementation plan for:

- environment-specific data residency and retention policy contracts
- backup erasure replay and restore validation after erasure
- permission-drift reports for memberships, role assignments, namespace grants,
  service credentials, identity bindings, and effective-access previews
- standard and audit retention minimization beyond the existing ephemeral path
- external payload-store retention and erasure evidence checks
- compliance evidence packages that combine audit export, retention report,
  erasure replay, backup/restore, release checklist, benchmark, and alert-route
  evidence
- admin/operator workflows for reviewing compliance reports without raw payload
  disclosure

## Data Residency Contract

The first implementation should define a policy contract before enforcing any
provider-specific residency behavior.

Minimum fields:

| Field | Meaning |
| --- | --- |
| `environment` | `local`, `ci`, `pilot`, or `production`. |
| `allowedRegions` | Approved runtime, database, backup, telemetry, and evidence locations. |
| `dataClasses` | Event payloads, memory facts, embeddings, exports, audit rows, backups, telemetry, and generated benchmark evidence. |
| `scopePolicy` | Whether organization or project scopes may require stricter residency. |
| `externalPayloadPolicy` | Whether external payload stores are allowed and where evidence is recorded. |
| `exceptionPolicy` | How approved exceptions are recorded, reviewed, and expired. |

Residency policy is a deployment and evidence constraint. It must not authorize
memory reads or writes, and it must not bypass namespace grants.

## Backup Erasure Replay

Backups can contain payloads that were erased after the backup was created.
Future restore validation must prove that a restored database can replay later
erasure and redaction actions before being promoted.

Required behavior:

- maintain a payload-safe erasure replay ledger from existing erasure and
  redaction records
- identify backups that predate each erasure action
- restore into a validation database, never directly over production
- rerun migrations, then replay or verify all erasure actions newer than the
  backup timestamp
- prove source-event reads, memory reads, chunks, embeddings, reviews, and vault
  exports no longer expose erased payloads after replay
- emit evidence with backup id, restore id, erasure count, replay count, held
  count, validation status, operator, and hashes

Legal hold still wins. A held payload must not be erased from the only required
preservation copy until the hold is released.

## Permission-Drift Reporting

Permission-drift reports should answer "who can see or administer what, and why"
without relying on direct SQL inspection.

Initial report dimensions:

- principals by type and status
- identity bindings by provider, issuer, status, and last-seen time
- service-account credentials by owner, review date, expiry, and status
- organization and project memberships by access level
- role assignments by scope and role id
- namespace grants by target principal or role, prefix, permission, and age
- effective-access previews for representative read, write, review, and admin
  operations
- stale, orphaned, over-broad, expired, or conflicting records

Reports must use the same access-management and authorizer semantics as runtime
access checks. They should flag possible drift; remediation remains an audited
operator action.

## Environment Retention Policy

The existing retention policy defines default windows. LR-06 turns that into an
implementation track for environment-specific enforcement.

The policy should support:

- per-environment windows for `ephemeral`, `standard`, `audit`,
  `legal_hold`, and `erasure_requested`
- sensitivity-specific stricter windows for `personal`, `secret`, and
  `regulated`
- legal-hold overrides
- dry-run and execute modes for minimization jobs
- payload-safe reports of eligible, minimized, skipped, held, and failed items
- external payload-store checks for `external_payload_uri`
- metrics and audit records for each batch

Minimization must update derived copies together or prove why they were skipped.
Partial success needs alertable evidence.

## Compliance Evidence Package

The first evidence package should be a generated bundle or manifest, not a new
data lake.

Inputs:

- scoped audit export manifest and row hash
- retention report snapshot
- legal hold summary
- erasure execution or replay evidence
- backup export and restore-validation evidence
- release checklist evidence
- benchmark release-gate report
- alert-route smoke result
- permission-drift report

Outputs:

- one payload-safe JSON manifest with ids, timestamps, row counts, hashes,
  environment, version, and operator
- optional NDJSON files that omit raw event payloads, memory bodies, review
  notes, queries, embeddings, and secrets
- a deterministic hash over the evidence manifest

## Implementation Backlog

| ID | Priority | Status | Item | Acceptance Criteria |
| --- | --- | --- | --- | --- |
| GC-01 | P0 | Done | Define environment governance policy contract. | [Environment Governance Policy GC-01](environment-governance-policy-gc01.md) and `EnvironmentGovernancePolicyValidator` document and validate the policy shape for data residency, retention windows, external payload stores, exception records, and evidence locations for local, CI, pilot, and production without changing runtime authorization. |
| GC-02 | P0 | Done | Add permission-drift report. | [Permission-Drift Report GC-02](permission-drift-report-gc02.md) and `POST /api/admin/access/permission-drift` let operators generate a payload-safe report for principals, bindings, service accounts, memberships, role assignments, namespace grants, and `IMemoryAccessAuthorizer` effective-access previews, with stale, expired, inactive, over-broad, and effective-admin-access records flagged for review. |
| GC-03 | P0 | Done | Add backup erasure replay validation. | [Backup Erasure Replay Validation GC-03](backup-erasure-replay-validation-gc03.md), `scripts/platform-erasure-replay-ledger-export.sh`, and `scripts/platform-restore-validation.sh` let operators export a payload-safe redaction ledger, require replay for a selected backup timestamp, replay or verify post-backup erasure actions in a restored database, and emit evidence/metrics proving erased payloads and derived projections remain hidden. |
| GC-04 | P0 | Done | Implement standard and audit retention minimization. | [Standard And Audit Retention Minimization GC-04](standard-audit-retention-minimization-gc04.md), `scripts/platform-retention-minimization.sh`, and the platform runtime contract support dry-run and execute modes for non-ephemeral minimization, respect legal holds, leave external payload pointers for GC-05 checks, clear review-note payload copies, preserve durable memory projections, and emit metrics plus payload-safe audit evidence. |
| GC-05 | P1 | Done | Check external payload-store retention. | [External Payload Retention Check GC-05](external-payload-retention-check-gc05.md), `scripts/platform-external-payload-retention-check.sh`, and the platform runtime contract inspect `external_payload_uri` rows, classify expected provider object state from PostgreSQL lifecycle state, probe supported providers in verify mode, reject disabled-policy pointers, and emit payload-safe evidence and metrics without logging URI content, payload bytes, or secret material. |
| GC-06 | P1 | Done | Generate compliance evidence package. | [Compliance Evidence Package GC-06](compliance-evidence-package-gc06.md), `scripts/platform-compliance-evidence-package.sh`, and the platform runtime contract create a payload-safe manifest, NDJSON artifact index, SHA-256 sidecar, and metrics that link audit export, retention report, legal hold, erasure replay, backup/restore, release checklist, benchmark, alert-route, external payload, and permission-drift evidence without embedding raw payloads. |
| GC-07 | P1 | Done | Add governance/compliance admin console view. | [Governance/Compliance Admin Console GC-07](governance-compliance-admin-console-gc07.md) adds `/api/admin/compliance/status` and a `/admin/` Compliance view that surfaces retention, erasure replay, legal hold, permission drift, and evidence package status with links to existing payload-safe reports and no raw source payloads in lists. |
| GC-08 | P1 | Done | Add governance/compliance release smoke. | [Governance/Compliance Release Smoke GC-08](governance-compliance-release-smoke-gc08.md) and `scripts/governance-compliance-release-smoke.sh` verify policy config, permission-drift report generation, erasure replay evidence, retention dry run, audit export, and strict compliance evidence manifest creation against an isolated database. |

## Risk Register

| Risk | Severity | Likelihood | Why It Matters | Mitigation |
| --- | --- | --- | --- | --- |
| Restore resurrects erased payloads. | High | Medium | Old backups may contain data removed from the live database after an erasure request. | Require restore-to-new-database validation plus erasure replay evidence before promotion. |
| Permission drift silently widens access. | High | Medium | Memberships, role assignments, namespace grants, service credentials, and identity bindings can accumulate stale authority. | Add scheduled drift reports and require audited remediation through existing access-management paths. |
| Compliance evidence leaks payloads. | High | Low | Evidence packages are likely to be shared more broadly than operational databases. | Make evidence manifests ids/counts/hashes only and reuse audit export payload-safety rules. |
| Residency policy becomes a false guarantee. | Medium | Medium | A doc-only region claim is not useful if backups, telemetry, or evidence are stored elsewhere. | Require platform evidence for runtime, database, backup, telemetry, and evidence locations. |
| Retention minimization breaks source evidence. | High | Low | Aggressive minimization can make memories unprovable or reviews unexplainable. | Preserve event ids, hashes, policy metadata, and source links; require dry-run reports and legal-hold checks. |
| External payload stores drift from PostgreSQL state. | Medium | Medium | Future external payload pointers can keep content after database erasure. | Add store-specific checks and evidence before allowing external payload storage in pilot. |

## Exit Criteria

LR-06 is complete when:

- this plan exists and is linked from the documentation index
- Decision 0048 accepts the governance and compliance gate boundary
- `docs/backlog.md` marks LR-06 done and adds `GC-*` follow-on work
- product planning points to a target-environment pilot rehearsal before
  inviting the first external pilot user
- a doc guard test verifies the plan, decision, backlog, and index links
- no schema, endpoint, or runtime behavior changes are introduced by the slice
