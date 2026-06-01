# Decision 0048: Governance And Compliance Gate

Date: 2026-06-01

Status: Accepted

## Context

MR-09 implemented authenticated governance workflows for legal holds, erasure
execution, and retention reporting. EA-07 added scoped payload-safe audit
export. PI-04 and PI-08 added backup/export and restore-validation evidence
inside the production-platform path. Enterprise access, context productization,
Domain extraction, and platform integration have now stabilized the main policy
and operator surfaces.

The remaining long-run governance gap is broader compliance automation:
environment-specific retention policy, data residency evidence, backup erasure
replay, permission-drift reporting, external payload-store checks, and
repeatable compliance evidence packages. These should be scoped before runtime
implementation starts because they touch backup recovery, deletion guarantees,
audit exports, platform evidence, and operator workflows.

## Decision

Adopt [LR-06 Governance And Compliance Gate](../governance-compliance-gate-lr06.md)
as the governance and compliance implementation boundary.

The governance and compliance track will:

- preserve PostgreSQL as the authority for legal hold, erasure, retention,
  access, and audit state
- keep runtime memory authorization backed by local memberships, role
  assignments, namespace grants, and effective-access previews
- define environment data residency and retention policy before enforcement
- require backup erasure replay or validation before restored databases can be
  promoted after an erasure has occurred
- add permission-drift reporting without making reports an authorization path
- keep evidence packages payload-safe by default with ids, counts, hashes,
  timestamps, statuses, and links instead of raw content
- keep external payload-store support disabled until environment policy allows
  explicit schemes and verify-mode existence, deletion, and evidence checks pass
- implement follow-on `GC-*` slices without hidden schema, endpoint, or runtime
  churn in the planning slice

## Consequences

- LR-06 completes as a plan and guardrail, not as a hidden compliance feature.
- Follow-on `GC-*` items can implement policy, reports, replay validation,
  minimization, evidence packaging, and admin UI in small slices.
- Backup health is not enough for compliance readiness; restored backups must
  also prove erasure and redaction state is still honored.
- Permission-drift reports identify risk but remediation remains an audited
  access-management action.
- Evidence bundles can be shared with pilot reviewers without exposing raw
  source payloads, memory bodies, review notes, queries, embeddings, API keys,
  or secret values.
- Any later provider-specific residency or external payload-store integration
  must satisfy this boundary rather than bypassing it.
