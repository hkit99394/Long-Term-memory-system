# Decision 0047: Directory Sync Is Provisioning Only

Date: 2026-06-01

Status: Accepted

## Context

EA-10 evaluates whether SCIM or provider-specific directory sync is needed for
the first enterprise pilot.

The project now has generic OIDC authentication, identity bindings,
service-account lifecycle records, admin access-management APIs, audit export,
and migration/rollback smoke. Runtime memory authorization is still based on
local memberships, role assignments, and namespace grants through
`IMemoryAccessAuthorizer`.

Directory data can help reduce provisioning work, but it can also create a
dangerous shortcut if provider groups become runtime authorization.

## Decision

Do not implement directory sync before the first enterprise pilot.

If pilot evidence later justifies sync, implement it as provisioning-only:

- directory subjects may create or update local principals and identity bindings
- directory groups may produce local access-change proposals
- approved proposals may write local memberships, role assignments, and
  namespace grants
- applied changes must write access audit events
- deprovisioning disables bindings or credentials after policy review; it does
  not delete memory or audit evidence

Directory groups, OIDC token roles, token scopes, and provider claims must not
directly grant memory read, write, review, or admin access at request time.

## Consequences

- The first pilot stays smaller and uses already-tested OIDC, service-account,
  admin access-management, audit export, and rollback paths.
- Operators can learn whether manual provisioning is actually painful enough to
  justify sync.
- Future SCIM or directory integrations have a clear boundary: they stage or
  apply local provisioning changes, but they do not join the runtime
  authorization decision.
- Provider-specific shortcuts remain possible later, but only behind the same
  provisioning-only contract.
- `DM-06` can resume after EA-10 because the enterprise access foundation is
  now stable enough for policy vocabulary cleanup.
