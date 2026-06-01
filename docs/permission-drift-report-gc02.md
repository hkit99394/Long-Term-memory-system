# GC-02 Permission-Drift Report

Date: 2026-06-01

Status: Implemented

## Goal

GC-02 adds a payload-safe permission-drift report for organization and project
scopes. Operators can generate the report through:

```text
POST /api/admin/access/permission-drift
```

The report answers who can see or administer the requested scope, why that
access exists, and which records need review. It does not create, remove, or
override access. Remediation still happens through the audited access-management
and service-account lifecycle paths.

## Request

```json
{
  "scopeType": "project",
  "scopeId": "44444444-4444-4444-8444-444444444444",
  "namespacePrefix": "/project/44444444-4444-4444-8444-444444444444",
  "staleAfterDays": 90,
  "maxPreviewPrincipals": 20
}
```

| Field | Rule |
| --- | --- |
| `scopeType` | `org` or `project`. |
| `scopeId` | Organization or active project id. |
| `namespacePrefix` | Optional namespace prefix; defaults to `/org/{orgId}` or `/project/{projectId}`. |
| `staleAfterDays` | Optional positive stale-window threshold. Defaults to `90`. |
| `maxPreviewPrincipals` | Optional preview cap from `1` to `100`. Defaults to `20`. |

The caller must already have admin access to the requested scope and namespace
under `IMemoryAccessAuthorizer`.

## Response Shape

The response includes:

| Section | Contents |
| --- | --- |
| `principals` | Principal ids, principal type, status, and timestamps. |
| `identityBindings` | Binding id, principal id, provider, issuer hash, status, last-seen time, and timestamps. |
| `serviceAccounts` | Service principal id, owner scope, owner principal id, auth method, status, review due, expiry, and timestamps. |
| `serviceCredentials` | Credential id, service principal id, auth method, status, review due, expiry, last used time, and timestamps. |
| `organizationMemberships` | Organization id, principal id, access level, and creation time. |
| `projectMemberships` | Project id, organization id, principal id, access level, and creation time. |
| `roleAssignments` | Assignment id, principal id, role id, scope type/id, and creation time. |
| `namespaceGrants` | Grant id, target principal or role id, namespace prefix, permission, and creation time. |
| `effectiveAccessPreviews` | Authorizer-backed read, write, review, and admin previews for representative principals. |
| `findings` | Stale, expired, inactive, over-broad, and effective-admin-access records that need review. |

## Payload-Safety Rules

The report omits raw memory, source payloads, review notes, queries, embeddings,
API keys, service credential fingerprints, identity subjects, external emails,
and provider metadata.

Identity issuers are represented as `issuerHash`, not raw issuer text. Service
credential records use ids and posture fields, not secret material.

## Finding Codes

| Code | Meaning |
| --- | --- |
| `inactive_principal_has_access` | A disabled or deleted principal still appears in scoped access records. |
| `inactive_identity_binding` | A binding is disabled or deleted but still belongs to a scoped principal. |
| `stale_identity_binding` | An active binding has not been seen inside the stale window. |
| `inactive_service_account` | A service account is disabled or deleted while scoped records still reference it. |
| `service_account_review_due` | Service-account owner review is due or overdue. |
| `service_account_expired` | Service-account expiry is due or overdue. |
| `inactive_service_credential` | A credential is rotated, disabled, or expired. |
| `service_credential_review_due` | Credential review is due or overdue. |
| `service_credential_expired` | Credential expiry is due or overdue. |
| `stale_service_credential` | An active credential has no recent use inside the stale window. |
| `over_broad_org_membership` | Organization membership grants `admin` or `owner`. |
| `over_broad_project_membership` | Project membership grants `admin`. |
| `global_role_assignment` | A role assignment applies globally. |
| `over_broad_namespace_admin_grant` | A namespace grant allows `admin`. |
| `broad_namespace_prefix` | A grant applies to the full reported namespace prefix. |
| `effective_admin_access` | The authorizer preview allows admin access. |

## Runtime Boundary

Permission-drift reporting reads the same PostgreSQL authority used by runtime
authorization:

- `principals`
- `identity_bindings`
- `service_accounts`
- `service_account_credentials`
- `organization_memberships`
- `project_memberships`
- `role_assignments`
- `memory_access_grants`

Effective access is evaluated with `IMemoryAccessAuthorizer.PreviewAsync`, so
the report follows the same membership, role, grant, and namespace semantics as
runtime access checks.

The report is diagnostic evidence. It must not become an authorization path.

## Follow-On Work

GC-03 pairs permission-drift evidence with backup erasure replay validation, and
GC-04 adds standard/audit retention minimization evidence. GC-05 adds external
payload-store retention checks. GC-06 includes this report in the compliance
evidence package without changing the response payload-safety rules.
