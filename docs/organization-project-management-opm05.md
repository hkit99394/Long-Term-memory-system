# Organization And Project Management OPM-05

Status: OPM-05 implemented.

Owner: Product Owner + Knowledge Steward + Developer.

Created: 2026-06-09.

## Purpose

OPM-05 gives Product Owners and Knowledge Stewards a payload-safe grant matrix
workflow for project roles. OPM-04 made concrete access visible and revocable;
OPM-05 adds the matrix-level review/edit loop so role-targeted namespace grants
can be compared to least-privilege presets, previewed against currently assigned
principals, and replaced without SQL.

## API Contract

The API adds:

```text
GET /api/admin/projects/{projectId}/grant-matrix
PUT /api/admin/projects/{projectId}/grant-matrix/roles/{roleId}
```

Responses use `contractId: OPM-05`, `payloadSafe: true`, and
`rawSourcePayloadsIncluded: false`. The grant matrix response includes:

- project management context
- least-privilege preset templates
- default and project-defined roles
- current role-targeted namespace grants under the project namespace
- preset alignment labels
- effective-access previews for principals currently assigned to each role

Updates replace only the selected role's grants under the selected project
namespace. Request bodies include `roleId`, optional `presetId`, grant rows,
`reason`, and `auditEvidenceId`.

## Guardrails

All matrix reads and writes require the same project-management authorization as
OPM-03 and OPM-04: active project admin access, or owner/admin access to the
parent organization. Inactive projects require organization-level access.

Matrix updates:

- only target default role templates or active project-defined roles
- only create role-targeted grants, never principal-targeted grants
- keep namespaces under `/project/{projectId}/`
- reject project-root grants
- reject role-lens grants for a different role id
- reject namespace `admin` grants; break-glass admin remains in the dedicated
  access-management workflow
- require reason and audit evidence id

Successful updates create payload-safe `namespace_grant_change` audit events
with `contractId: OPM-05`, `operation: grant_matrix_replaced`, preset id, reason,
audit evidence id, request path, method, correlation id, and previous/new grant
counts.

## UI Scope

The `/admin/` Management project detail pane now loads the grant matrix beside
access inventory, lifecycle, and scope settings. It shows role cards with
current grants, preset alignment, effective preview counts, preset selectors,
editable grant rows, and required reason/audit evidence fields.

Selecting a preset fills concrete project/role namespaces. Submitting a role
card updates that role's matrix and refreshes the directory, project detail,
access inventory, and matrix evidence pane.

## Verification

Focused verification:

```bash
dotnet test tests/MemorySystem.UnitTests/MemorySystem.UnitTests.csproj --filter FullyQualifiedName~OrganizationProjectManagementOpm05Tests
MEMORYSYSTEM_REQUIRE_DATABASE_TESTS=true dotnet test tests/MemorySystem.IntegrationTests/MemorySystem.IntegrationTests.csproj --filter FullyQualifiedName~ApiAdminGrantMatrixTests
cd tools/ui
npm run build
npm run check
```
