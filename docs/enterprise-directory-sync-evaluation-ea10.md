# Enterprise Directory Sync Evaluation EA-10

Status: Accepted evaluation

## Decision Summary

Do not add SCIM or provider-specific directory sync before the first enterprise
pilot.

The first pilot should use:

- OIDC for human authentication
- local `identity_bindings` for subject-to-principal mapping
- service-account lifecycle records for automation principals
- admin access-management workflows for memberships, role assignments, and
  namespace grants
- audit export for setup, rollback, and closeout evidence

Directory sync can be reconsidered after the first pilot. If it is added, it
must be provisioning-only. Provider groups may suggest local access changes, but
they must not directly grant memory read, write, review, or admin access at
runtime.

## Why Not Now

The current enterprise access foundation already covers the first pilot's
minimum needs:

- OIDC resolves external human subjects to active internal principals.
- API keys remain for service-account and break-glass operation.
- Admin endpoints can manage organization memberships, project memberships,
  role assignments, namespace grants, and effective-access previews.
- Audit export produces payload-safe evidence for authentication,
  authorization denial, access-management changes, service credentials, and
  export actions.
- The EA-08 smoke proves API-key-only, OIDC-only, dual-auth, service-account,
  and OIDC-disabled rollback modes without weakening namespace grants.

Adding directory sync before the pilot would increase blast radius around the
most sensitive part of the product: who can see which memory. The first pilot
should measure whether manual provisioning is actually painful enough to justify
another identity subsystem.

## Evaluation Criteria

Reconsider directory sync only if the pilot shows at least one of these
conditions:

| Signal | Threshold For Reconsideration |
| --- | --- |
| User volume | More than 25 active human users need frequent onboarding or offboarding. |
| Group churn | Role or project membership changes happen weekly or faster. |
| Operator load | Access-management work takes more than 30 minutes per week. |
| Error rate | Manual provisioning causes repeated binding, membership, or grant mistakes. |
| Compliance demand | A customer requires directory-sourced provisioning evidence. |
| Service scale | Multiple service accounts need owner, expiry, and credential review automation. |

If none of those signals appear, keep directory sync out of scope and continue
with local admin-managed access.

## Considered Options

| Option | Fit | Risks | Decision |
| --- | --- | --- | --- |
| No sync for first pilot | Best for a small pilot; uses the current tested access model. | Manual setup does not scale indefinitely. | Choose now. |
| Generic SCIM provisioning | Standard shape for users and groups across enterprise directories. | Requires new schemas, run tracking, deactivation semantics, conflict handling, and audit surfaces. | Revisit after pilot evidence. |
| Provider-specific group import | Faster for one provider such as Entra ID, Okta, or Auth0. | Locks behavior to one provider and risks encoding provider group claims into access policy. | Avoid as first implementation. |
| Runtime group-claim authorization | Simple on paper. | Bypasses local memberships, role assignments, namespace grants, previews, and audit controls. | Reject. |

## Allowed Future Sync Behavior

A future sync adapter may:

- create or update internal `principals`
- create or disable `identity_bindings`
- stage proposed organization memberships
- stage proposed project memberships
- stage proposed role assignments
- stage proposed namespace grants
- update external display names, emails, tenant ids, and provider metadata
- mark absent users or bindings as disabled after a configured grace period
- write access audit events for every applied change
- produce dry-run output before applying changes

Every applied membership, role, or namespace change must still be represented in
the local database tables used by `IMemoryAccessAuthorizer`.

## Disallowed Future Sync Behavior

A future sync adapter must not:

- authorize memory reads or writes from token group claims at request time
- grant `read`, `write`, `review`, or `admin` access directly from provider
  groups
- create broad namespace grants without an explicit local mapping and approval
- delete memory, source events, audit events, reviews, or vault exports
- erase a principal's data simply because a directory account disappeared
- bypass admin effective-access preview semantics
- skip audit records for access-affecting changes

## Suggested Future Shape

If pilot evidence justifies sync, prefer a generic provisioning boundary rather
than provider-specific runtime logic.

Suggested slices:

| Slice | Purpose | Acceptance |
| --- | --- | --- |
| DS-01 | Add directory sync planning schema. | Sync runs, external subjects, group mapping proposals, and dry-run results are stored without changing access. |
| DS-02 | Add dry-run importer. | A sample SCIM or provider export produces principal, binding, membership, role, and grant proposals with no applied changes. |
| DS-03 | Add approval workflow. | Operators approve staged changes through the admin surface and see effective-access previews before apply. |
| DS-04 | Add apply and audit path. | Approved changes write local tables and access audit events; rollback disables applied provisioning without deleting evidence. |
| DS-05 | Add deprovisioning policy. | Missing directory users disable identity bindings and service credentials after grace period; memory and audit records remain preserved. |

This keeps directory sync outside the request path and preserves the current
authorization model.

## Pilot Closeout Questions

Answer these at the end of the first pilot:

- How many human users were bound to OIDC?
- How many service accounts were created?
- How many membership, role, and namespace changes were made?
- How often did access need to change after initial setup?
- Were there any cross-project or role-boundary access mistakes?
- Did operators need group-derived suggestions, or were local admin workflows
  enough?
- Would dry-run provisioning have reduced risk, or just added ceremony?

## Final EA-10 Outcome

Directory sync is not needed for the first pilot. The enterprise access track is
coherent enough to continue with OIDC, local access-management workflows,
service-account lifecycle records, migration/rollback smoke, and audit export.

The next project cleanup was `DM-06`, because the enterprise access foundation
became stable enough to resume removal of duplicate policy and string
normalization helpers.
