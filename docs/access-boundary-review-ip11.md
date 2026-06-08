# Access Boundary Review IP-11

Status: implemented for improvement plan item IP-11.

Owner: Security Professional.

## Purpose

IP-11 turns enterprise access posture into a weekly, payload-safe review queue.
It audits memberships, role assignments, namespace grants, service accounts,
service credentials, OIDC identity bindings, break-glass key posture, and
permission drift without exposing secrets, raw memory, or raw source payloads.

Run the dry run without API calls:

```bash
scripts/access-boundary-review.sh --dry-run
```

Run the canonical project review:

```bash
scripts/access-boundary-review.sh \
  --scope-type project \
  --scope-id 9f8e7d6c-5b4a-4321-9123-abcdef123002
```

The review also loads
`docs/access-boundary-accepted-findings.json` by default. Accepted findings
must be payload-safe and must include owner role, approver role, accepted
reason, cleanup action, and review due date. Accepted findings are still
reported in the JSON output, but they do not block `status: clear` while they
remain current and exactly match the accepted rule.

The live command calls:

```text
POST /api/admin/access/permission-drift
```

The permission-drift report is already payload-safe. IP-11 wraps it into a
security review queue and adds the manual break-glass checks that cannot be
safely inferred from secret values.

## Review Sources

| Review area | Source | Required review |
| --- | --- | --- |
| Memberships | `organizationMemberships` and `projectMemberships` from `/api/admin/access/permission-drift` | Confirm admin and owner access still has a named need, and inactive principals have no remaining access. |
| Role assignments | `roleAssignments` from `/api/admin/access/permission-drift` | Confirm global roles are not used for pilot or production unless explicitly approved. |
| Namespace grants | `namespaceGrants` and `effectiveAccessPreviews` from `/api/admin/access/permission-drift` | Confirm admin grants and full-prefix grants are required or narrow them to read/write/review. |
| Service accounts | `serviceAccounts` and `serviceCredentials` from `/api/admin/access/permission-drift` | Confirm owner, review due date, expiry, credential status, last use, rotation, and least-privilege grants. |
| OIDC bindings | `identityBindings` from `/api/admin/access/permission-drift` plus OIDC login audit evidence | Confirm active bindings map to active human principals and token claims do not grant memory access. |
| Break-glass keys | Managed secret-store configuration plus audit export | Confirm every key has a human owner, narrow recovery access, last-use evidence, and rotation/removal decision. |
| Audit evidence | `POST /api/admin/audit-exports` | Attach access-management, authentication, denial, service credential, break-glass, and audit export evidence for changes. |

## Output Contract

`scripts/access-boundary-review.sh` emits JSON with:

- `payloadSafe: true`
- `rawSourcePayloadsIncluded: false`
- `acceptedFindingSource` with the accepted-finding file path and active rule
  count
- `acceptedFindings` and `unacceptedFindings`
- `permissionDriftReport` summary with counts, findings by severity/code, and
  bounded finding details for unresolved and accepted findings
- `reviewSections.memberships`
- `reviewSections.roleAssignments`
- `reviewSections.namespaceGrants`
- `reviewSections.serviceAccounts`
- `reviewSections.oidcIdentityBindings`
- `reviewSections.breakGlassKeys`
- `nextActions` for security review closeout

The output may include ids, statuses, timestamps, hashed issuer identifiers,
namespace prefixes, finding codes, and recommended actions. It must not include
API keys, service credential fingerprints, raw OIDC subjects, issuer URLs,
external emails, provider metadata, raw memory, source payloads, review notes,
queries, or embeddings.

## Weekly Habit

Run IP-11 after the IP-09 memory hygiene queue:

```bash
scripts/weekly-admin-review-workflow.sh
scripts/access-boundary-review.sh \
  --scope-type project \
  --scope-id 9f8e7d6c-5b4a-4321-9123-abcdef123002
```

Close the weekly access review only when:

1. every high-severity permission-drift finding is resolved or covered by a
   current accepted-finding rule with owner, reason, cleanup action, and review
   due date
2. admin memberships, global roles, namespace admin grants, and effective admin
   previews are approved or narrowed
3. service-account owners, review dates, expiry dates, and credential rotation
   are current
4. stale or inactive OIDC bindings are disabled or explicitly retained
5. break-glass keys have owner, last-use, audit export, and rotation/removal
   evidence
6. any changed access records have an audit export attached

## Break-Glass Rules

Break-glass keys are not enumerated by the script because the secret store is
the authority and secret values must never be printed. The weekly review must
still record payload-safe evidence:

- key id or secret-store object name, never the key value
- owner principal id and owner role
- reason for keeping the key active
- last-use timestamp or "not used in review window"
- memberships and namespace grants that make recovery possible
- rotation/removal decision and next review due date
- audit export id for any activation window

Console break-glass access must map to an active human principal. Service
principals must not be accepted by the console break-glass login.

## Failure Rules

The weekly access review is not clear when:

- permission-drift findings remain unowned, unaccepted, or past the accepted
  review due date
- inactive principals, disabled bindings, or disabled service accounts retain
  access records
- service credentials are expired, stale, or past review due date
- OIDC bindings are stale without an owner decision
- break-glass keys have no named owner, last-use evidence, or rotation/removal
  decision
- provider groups, token roles, token scopes, or directory claims are used as
  runtime authorization instead of local memberships, role assignments, and
  namespace grants

## Accepted Finding Rules

The accepted-finding file records short-lived exceptions for bootstrap or
break-glass access that cannot be safely removed yet. It is not a suppression
list. The script still reports raw drift counts and accepted finding matches.

Accepted rules must not approve root namespace grants such as `/project`,
`/org`, `/global`, `/user`, `/role`, `/agent`, or `/session`. Root grants must
be removed or narrowed because they cross project-registration and
least-privilege boundaries. A project-specific root such as
`/project/{projectId}` may be accepted only as a time-bound bootstrap or
break-glass admin anchor with owner, reason, cleanup action, and review due
date.

## Completion Evidence

IP-11 is complete when:

- `scripts/access-boundary-review.sh` emits a payload-safe dry run and live
  permission-drift review queue.
- the queue covers memberships, role assignments, namespace grants, service
  accounts, service credentials, OIDC bindings, break-glass posture, and audit
  evidence.
- [Weekly Admin Review Workflow IP-09](weekly-admin-review-workflow-ip09.md)
  and [Project Memory Runbook](project-memory-runbook.md) include the IP-11
  weekly habit.
- tests cover the documentation contract, script syntax, and dry-run output.
