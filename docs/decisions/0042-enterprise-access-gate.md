# Decision 0042: Enterprise Access Gate

Date: 2026-05-31

Status: Accepted

## Context

LR-01 starts the Long Run enterprise access track. The project currently
authenticates HTTP callers with API keys mapped to active `principals`. Memory
authorization already uses organization memberships, project memberships, role
assignments, and namespace grants through the application authorizer.

The next step is not to replace that authorization model. It is to add
enterprise identity around it so human users, service accounts, and operators can
authenticate, manage access, and export audit evidence without direct database
changes or long-lived human API keys.

## Decision

Adopt [Enterprise Access Gate](../enterprise-access-gate.md) as the LR-01
implementation scope.

The enterprise access architecture will:

- keep `principals` as the canonical identity used by authorization
- add external identity bindings for OIDC or SSO subjects
- keep API-key authentication as a migration, service, and break-glass path
- add service-account lifecycle metadata and credential posture
- route every authentication method through one principal-resolution result
- keep `IMemoryAccessAuthorizer` and database-backed memberships, role
  assignments, and namespace grants as the runtime authorization boundary
- add admin-console workflows for memberships, role assignments, and namespace
  grants
- add audit events and export for authentication, authorization denials,
  access-management changes, service credential changes, and export actions
- require migration and rollback checks before any external pilot uses SSO

OIDC token roles, scopes, and directory groups may be used as provisioning
hints. They must not directly grant memory read, write, review, or admin access
at runtime.

## Consequences

- The system can add SSO without weakening the existing namespace grant model.
- Existing API-key clients can migrate through a dual-auth period instead of a
  big-bang cutover.
- Service accounts become explicit principals with owners, scoped grants, and
  audit trails rather than anonymous long-lived shared keys.
- Admin access management becomes a product surface, not a SQL-only operation.
- Audit export becomes a pilot requirement for identity and access changes.
- Implementation work is split into the `EA-*` backlog items before runtime OIDC
  code is added.
