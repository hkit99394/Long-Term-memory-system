# Directory Sync Evaluation IP-18

Status: implemented for improvement plan item IP-18.

Owner: Security Professional + IT/Ops.

## Purpose

IP-18 revisits SCIM and provider group sync after human login, project roles,
namespace grants, and access-boundary review are stable. The outcome preserves
the existing security decision:

```text
Directory sync may be provisioning-only. Runtime memory authorization still
comes from local memberships, role assignments, namespace grants,
effective-access previews, and audit records.
```

This item does not add a SCIM adapter or runtime group-claim authorization. It
adds a repeatable evaluation gate for deciding whether a future provisioning
adapter is justified. In every outcome, runtime authorization stays local.

## Decision

Keep directory sync deferred unless pilot evidence shows real provisioning
pressure. If future evidence justifies sync, the adapter must stage or apply
local access records and must not join the request-time authorization decision.

This is consistent with:

- [Enterprise Directory Sync Evaluation EA-10](enterprise-directory-sync-evaluation-ea10.md)
- [Decision 0047: Directory Sync Is Provisioning Only](decisions/0047-directory-sync-provisioning-only.md)
- [Access Boundary Review IP-11](access-boundary-review-ip11.md)
- [Project-Defined Roles IP-05](project-defined-roles-ip05.md)

## Evaluation Command

Run the default payload-safe evaluation:

```bash
bash -n scripts/directory-sync-evaluation.sh
scripts/directory-sync-evaluation.sh --dry-run
```

Run with pilot closeout signals:

```bash
scripts/directory-sync-evaluation.sh --dry-run \
  --active-human-users 30 \
  --group-churn weekly \
  --operator-minutes-per-week 45 \
  --manual-provisioning-errors 2 \
  --service-accounts 3
```

The report includes:

- `recommendation`: `defer_directory_sync`,
  `reconsider_provisioning_only_directory_sync`, or
  `repair_policy_sources_before_evaluating_sync`
- `policyChecks` for login stability, project roles, access-boundary review,
  EA-10, Decision 0047, provisioning-only posture, and
  `IMemoryAccessAuthorizer`
- `pilotSignals` for active users, group churn, operator load, manual errors,
  compliance demand, and service-account scale
- `allowedFutureSyncBehavior` and `disallowedFutureSyncBehavior`
- `rawDirectoryPayloadsIncluded: false`

## Reconsideration Signals

Reconsider provisioning-only sync only when at least one signal is present:

| Signal | Threshold |
| --- | --- |
| User volume | More than 25 active human users need frequent onboarding or offboarding. |
| Group churn | Role or project membership changes happen weekly or faster. |
| Operator load | Access-management work takes more than 30 minutes per week. |
| Manual errors | Manual provisioning causes repeated binding, membership, or grant mistakes. |
| Compliance demand | A customer requires directory-sourced provisioning evidence. |
| Service scale | Multiple service accounts need owner, expiry, and credential review automation. |

If none of those signals appear, keep directory sync out of scope and continue
with OIDC, local identity bindings, local memberships, local role assignments,
local namespace grants, effective-access previews, and audit exports.

## Allowed Future Sync Behavior

A future provisioning adapter may:

- create or update local principals and identity bindings
- stage proposed organization memberships
- stage proposed project memberships
- stage proposed role assignments
- stage proposed namespace grants
- produce dry-run output before applying changes
- write access audit events for every applied change

## Disallowed Future Sync Behavior

A future provisioning adapter must not:

- authorize memory reads or writes from token group claims at request time
- grant `read`, `write`, `review`, or `admin` access directly from provider
  groups
- create broad namespace grants without explicit local mapping and approval
- delete memory, source events, audit events, reviews, or vault exports
- bypass admin effective-access preview semantics
- skip audit records for access-affecting changes

## Verification

IP-18 is complete when:

- `scripts/directory-sync-evaluation.sh --dry-run` emits a payload-safe
  evaluation report.
- the default report recommends `defer_directory_sync`.
- pilot pressure inputs can move the recommendation to
  `reconsider_provisioning_only_directory_sync` without allowing runtime group
  authorization.
- [Product Improvement Plan](product-improvement-plan.md),
  [Documentation Index](README.md), [Folder Structure](folder-structure.md),
  and [Testing Commands](testing.md) link to the workflow.
- unit tests cover the documentation contract and dry-run report.
