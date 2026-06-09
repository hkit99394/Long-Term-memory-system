# Organization And Project Management OPM-04

Status: OPM-04 implemented.

Owner: Security Professional + Ops + Developer.

Created: 2026-06-09.

## Purpose

OPM-04 gives authorized operators a payload-safe access inventory and audited
revocation workflow for organization and project management. OPM-01 through
OPM-03 made organizations/projects visible and editable; OPM-04 closes the
cleanup loop so stale memberships, role assignments, and namespace grants can
be inspected and removed without SQL.

## API Contract

The API adds:

```text
GET /api/admin/organizations/{organizationId}/access-inventory
GET /api/admin/projects/{projectId}/access-inventory
POST /api/admin/access/revocations
```

Inventory responses use `contractId: OPM-04`, `payloadSafe: true`, and
`rawSourcePayloadsIncluded: false`. They include:

- scope summary for the organization or project
- organization memberships, including inherited org admin/owner rows for
  project inventory
- direct project memberships
- scoped role assignments
- namespace grants under the selected org/project namespace roots
- stale-access prompts for inactive principals or inactive projects

Revocation requests accept `scopeType`, `scopeId`, `accessRecordType`,
`accessRecordId` for role assignments and namespace grants, `principalId` for
membership records, `reason`, and `auditEvidenceId`.

## Guardrails

All inventory and revocation operations require admin access to the selected
organization, or admin access to the active project / owner-admin access to its
parent organization. Inactive projects follow the OPM-03 rule and require
organization authorization.

Project inventory and project-scoped revocation authorization failures return
generic `404` responses for missing or inaccessible project scopes.

Revocation removes concrete access records:

- organization memberships
- project memberships
- role assignments
- namespace grants

The workflow rejects self-revocation and rejects removal of the last
organization owner. Principal deletion, credential disablement, and directory
deprovisioning remain separate enterprise-access workflows.

Successful revocations create payload-safe access audit events using the
existing action family for the removed record, with `operation: revoked`,
`contractId: OPM-04`, reason text, audit evidence id, request path, method, and
correlation id.

## UI Scope

The `/admin/` Management detail pane now loads access inventory for the
selected organization or project. It shows grouped rows for memberships, role
assignments, namespace grants, and review prompts. Each row has a compact
revocation form requiring reason and audit evidence id. The revoke request uses
the concrete row's scope and id, so project memberships listed from an
organization inventory are revoked against their project scope rather than the
selected organization scope.

The source evidence pane shows the payload-safe inventory by default and the
payload-safe revocation response after a revoke action.

## Verification

Focused verification:

```bash
dotnet test tests/MemorySystem.UnitTests/MemorySystem.UnitTests.csproj --filter FullyQualifiedName~OrganizationProjectManagementOpm04Tests
MEMORYSYSTEM_REQUIRE_DATABASE_TESTS=true dotnet test tests/MemorySystem.IntegrationTests/MemorySystem.IntegrationTests.csproj --filter FullyQualifiedName~ApiAdminAccessInventoryTests
cd tools/ui
npm run build
npm run check
```
