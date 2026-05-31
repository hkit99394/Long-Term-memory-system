# Enterprise Access Gate

Date: 2026-05-31

Status: Scoped

## Purpose

LR-01 scopes the first enterprise access gate. The goal is to move from
private-alpha API-key-only operation to team-ready identity without weakening the
existing memory authorization model.

The key invariant is:

```text
Authentication resolves a principal.
Authorization still uses database-backed memberships, role assignments, and
namespace grants.
```

OIDC, SSO, service accounts, and admin UI flows must not make token claims or
directory groups a direct substitute for `IMemoryAccessAuthorizer`.

## Current Baseline

The production-pilot baseline already has:

- API keys mapped to active `principals`
- `principal_type` values for `human`, `agent`, and `service`
- organization memberships, project memberships, role assignments, and memory
  access grants
- one application authorization boundary through `IMemoryAccessAuthorizer`
- admin memory/source inspection and audit browsing
- governance endpoints for legal holds, erasure execution, and retention
  reporting
- benchmark release gates that catch scoped-safety leaks and stale-memory usage

The missing enterprise layer is first-class identity lifecycle: external
identity bindings, SSO login, service-account credential posture, role/grant
administration, and exportable access audit evidence.

## Scope

LR-01 covers the implementation plan for:

- OIDC or SSO authentication for human users
- service accounts for agents, integrations, and automation
- a role assignment and namespace grant UI in the admin console
- audit export for authentication, authorization, and access-management changes
- migration from API-key-only operation to dual-auth and then SSO-first operation
- pilot acceptance checks proving existing namespace grants still control memory
  access

LR-01 does not implement OIDC or change runtime authentication yet. It defines
the next build slices and acceptance gates.

## Target Architecture

### Principal Resolution

All authentication methods should converge on one internal result:

| Field | Purpose |
| --- | --- |
| `principalId` | Existing `principals.id` used by access checks. |
| `principalType` | Existing `human`, `agent`, or `service` type. |
| `displayName` | Operator-readable identity label. |
| `authMethod` | `api_key`, `oidc`, `service_account`, or future method. |
| `externalIssuer` | OIDC issuer or service-account issuer when applicable. |
| `externalSubject` | Provider subject, workload identity subject, or client id. |
| `credentialId` | API key id, identity binding id, or service credential id for audit. |

API endpoints should continue to read the authenticated principal from claims or
request context. They should not accept caller-supplied principal overrides
except for existing compatibility fields that must still match the authenticated
principal.

### Identity Bindings

Add a durable identity-binding model before enabling SSO:

| Concept | Requirement |
| --- | --- |
| Binding key | Unique `(provider, issuer, subject)` mapped to one principal. |
| Provider metadata | Store issuer, audience or tenant id, display name, email, and last-seen time. |
| Status | `active`, `disabled`, and `deleted` bindings. |
| Conflict handling | A subject cannot map to multiple active principals. |
| Audit | Create, disable, delete, and remap operations produce audit records. |

External directory groups may suggest access during provisioning, but runtime
memory access still comes from local membership, role assignment, and namespace
grant rows.

### OIDC or SSO

Use OIDC JWT bearer validation for the first enterprise identity path.

Minimum requirements:

- configurable trusted issuer and audience
- JWKS-based token validation
- HTTPS metadata outside development and testing
- token lifetime and clock-skew limits
- stable `sub` claim mapping through identity bindings
- optional email and display-name synchronization
- no automatic memory grants from token roles, scopes, or groups
- disabled principals and disabled bindings are rejected
- authentication method and binding id are recorded for audit

The first SSO provider should be generic OIDC. Provider-specific shortcuts such
as Entra ID, Okta, or Auth0 group sync can come later behind the same binding
model.

### Service Accounts

Service accounts should be first-class principals with explicit owners and
least-privilege grants.

Minimum requirements:

- `principal_type = 'service'`
- owner organization or project
- human owner or admin contact
- allowed authentication method, such as workload identity, OIDC client
  credentials, or rotated service token
- expiry or review date for long-lived credentials
- namespace grants no broader than the integration needs
- audit records for credential creation, rotation, disablement, and use

Long-lived API keys may remain as a migration and break-glass path, but new
service integrations should prefer provider-managed workload identity or
rotatable client credentials.

### Role Assignment And Grant UI

The admin console should grow an access-management section for authorized
operators.

Required workflows:

- list principals, identity bindings, memberships, role assignments, and grants
- create or remove organization and project memberships
- assign and revoke supported roles for global, organization, and project scope
- create and revoke memory namespace grants
- preview the effective permissions for a principal, scope, namespace, and
  action before saving
- show why a request would be allowed or denied using the same authorizer
  semantics as the API
- prevent self-escalation unless a separate break-glass policy is explicitly
  added later
- write audit records for every access-management change

The UI may surface directory group hints, but saved access must be explicit in
PostgreSQL.

### Audit Export

Enterprise access needs exportable evidence for security review and pilot
operations.

The first audit export should support:

- time-window and organization/project filters
- principal, identity-binding, membership, role-assignment, and namespace-grant
  changes
- authentication successes and failures at aggregated or event level, depending
  on retention policy
- authorization denials for sensitive surfaces such as admin, governance, source
  event read, and memory read/write
- legal hold, erasure, retention, review, and export actions already covered by
  governance workflows
- newline-delimited JSON as the canonical export format, with CSV as a later
  convenience format
- a manifest containing export id, created time, filters, row counts, and a
  content hash

Exports should not include raw event payloads or raw memory content by default.
Evidence links and identifiers are acceptable; payload export should require a
separate governed process.

## Migration From API-Key-Only Operation

Use a staged rollout so existing private-alpha clients keep working while SSO is
introduced.

| Phase | Description | Exit Criteria |
| --- | --- | --- |
| 0. Baseline | Current API-key principal resolution remains unchanged. | Existing authorization and benchmark gates pass. |
| 1. Schema | Add identity bindings, service-account metadata, and access audit tables or views. | Migration tests prove uniqueness, status constraints, and rollback safety. |
| 2. Resolver | Introduce a shared principal-resolution abstraction used by API key and future OIDC schemes. | API-key behavior is unchanged; auth audit records include method and credential id. |
| 3. OIDC shadow | Validate OIDC tokens in a non-default or pilot-only path and map them to existing principals. | Disabled bindings fail; unbound subjects fail closed; no authorization bypass occurs. |
| 4. Admin UI | Add access-management UI and audit export. | Operators can manage memberships, roles, and grants without direct SQL. |
| 5. Dual auth | Enable API key plus OIDC for pilot users. | The same principal has the same memory access under both auth methods. |
| 6. SSO first | Make OIDC the preferred human path and restrict API keys to service or break-glass cases. | Human API keys are rotated down or disabled; service accounts have owners and review dates. |

Rollback must be possible through configuration: disabling OIDC should return the
system to API-key authentication without modifying memory grants or memberships.

## Pilot Acceptance Checks

The enterprise access gate is ready for implementation only when the following
checks are automated or documented as release checks:

| Check | Expected Result |
| --- | --- |
| Existing API-key path | Current API-key tests and benchmark release gate still pass. |
| OIDC principal mapping | A valid token maps to exactly one active principal through an active binding. |
| Unbound OIDC subject | Request fails closed without creating a principal or grant implicitly. |
| Disabled identity | Disabled principal or disabled binding cannot authenticate. |
| Namespace grant preservation | OIDC-authenticated requests still require the same namespace grants as API-key requests. |
| Cross-project denial | A Project A user cannot read Project B memory without membership and grant. |
| Role boundary | Role namespaces require the matching role assignment and grant. |
| Service account least privilege | A service principal can call only the scopes and namespaces explicitly granted. |
| Admin UI self-escalation | A non-owner admin cannot grant themselves broader access unless a future break-glass decision allows it. |
| Audit completeness | Login, denial, membership change, role assignment, grant change, service credential change, and audit export are represented in export output. |
| Payload safety | Audit exports omit raw memory and raw source payloads by default. |
| Rollback | Disabling OIDC leaves API-key operation and access checks intact. |

## Implementation Backlog

| ID | Priority | Status | Item | Acceptance Criteria |
| --- | --- | --- | --- | --- |
| EA-01 | P0 | Todo | Add identity-binding schema. | Migrations add provider, issuer, subject, principal, status, display metadata, timestamps, and uniqueness constraints; disabled bindings fail lookup; migration tests cover duplicate and deleted-binding behavior. |
| EA-02 | P0 | Todo | Introduce shared principal resolution. | API key authentication and future OIDC authentication both produce one principal-resolution result containing principal id, type, auth method, and credential or binding id; current API-key tests remain unchanged. |
| EA-03 | P0 | Todo | Add access audit event model. | Authentication, authorization denial, membership change, role assignment change, namespace grant change, service credential change, and audit export records can be written without storing raw memory payloads. |
| EA-04 | P0 | Todo | Add generic OIDC authentication. | Configured issuer, audience, JWKS, HTTPS metadata, lifetime validation, and identity binding lookup authenticate human principals; unbound, disabled, or ambiguous identities fail closed. |
| EA-05 | P0 | Todo | Add service-account lifecycle. | Service principals have owner metadata, allowed auth method, credential review or expiry date, rotation/disable path, and least-privilege namespace grants. |
| EA-06 | P0 | Todo | Add admin access-management UI. | Authorized operators can manage memberships, role assignments, and namespace grants with effective-access preview, authorizer-backed explanations, and audited changes. |
| EA-07 | P0 | Todo | Add audit export. | Operators can export access-management and auth audit records for a time window and scope as newline-delimited JSON with manifest hash and payload-safe fields. |
| EA-08 | P0 | Todo | Add migration and rollback smoke. | A smoke script proves API-key-only, OIDC-only, dual-auth, service-account, and OIDC-disabled rollback modes without weakening namespace grants. |
| EA-09 | P1 | Todo | Document pilot operator runbook. | Runbook covers provider setup, identity binding, service-account creation, role/grant review, audit export, rollback, and break-glass API-key handling. |
| EA-10 | P1 | Todo | Evaluate directory sync. | Decide whether SCIM or provider group sync is needed after the first pilot; any sync remains provisioning-only and does not bypass local grants. |

## Risks

| Risk | Severity | Mitigation |
| --- | --- | --- |
| Token claims bypass local grants. | High | Treat token groups and roles as provisioning hints only; runtime access remains local authorizer decisions. |
| Identity binding mistakes expose another user's memory. | High | Enforce unique active issuer/subject bindings, fail ambiguous mappings, audit remaps, and require admin review for changes. |
| Service accounts become broad shared secrets. | High | Require owner metadata, least-privilege grants, rotation/review dates, and audit records for credential use. |
| Admin UI creates self-escalation paths. | High | Add effective-access previews, restrict who can grant admin permissions, and audit all access-management writes. |
| Audit exports leak payloads. | Medium | Export identifiers and metadata by default; raw payload export requires a separate governed workflow. |
| OIDC rollout breaks private-alpha clients. | Medium | Keep dual-auth migration phases and a config rollback to API-key-only operation. |

## Success Criteria

LR-01 is successful when the team can start implementation with a clear answer
to these questions:

- Which external identity maps to which internal principal?
- Which local records authorize memory access after authentication?
- How do service accounts get scoped and reviewed?
- How does an operator grant or revoke access without direct SQL?
- What audit evidence can be exported for a pilot security review?
- How can the system roll back to API-key operation without changing memory
  grants?
