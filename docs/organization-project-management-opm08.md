# Organization And Project Management OPM-08

Status: OPM-08 implemented.

Owner: Product Owner + Knowledge Steward + Security Professional + Developer.

Created: 2026-06-09.

Target date: 2026-07-28.

## Purpose

OPM-08 brings project-defined role management into the same Product Owner
Management surface as lifecycle, scope, access inventory, grant matrix, and
activity review. IP-05 introduced custom project roles and validation rules;
OPM-08 adds the project-level operational workflow for listing, creating,
updating, and disabling those definitions without SQL fallback.

## API Contract

All OPM-08 endpoints require normal admin authentication, use the admin rate
limit, and reuse the OPM project-management authorization gate:

```text
GET /api/admin/projects/{projectId}/role-definitions
PUT /api/admin/projects/{projectId}/role-definitions/{roleId}
```

Responses use `contractId: OPM-08`, `payloadSafe: true`, and
`rawSourcePayloadsIncluded: false`.

The list response returns the project context, custom project role definitions,
active/disabled counts, assignment counts, and role-targeted grant counts.

The update request requires:

- `roleId`
- `displayName`
- optional `description`
- optional default-template `templateRoleId`
- `status` as `active` or `disabled`
- `reason`
- `auditEvidenceId`

Default role templates such as `developer`, `product_owner`, and
`knowledge_steward` are not accepted as project-defined role ids. They remain
platform templates and can only be used as optional templates for custom roles.

## Safety Rules

Disabling a custom project role is refused while either role assignments or
role-targeted namespace grants still depend on that role. Operators must remove
the dependent access first through OPM-04 access revocation or OPM-05 grant
matrix management.

Successful writes record `project_role_definition_change` audit evidence with
`contractId: OPM-08`, operation, status, previous status, template role id,
dependency counts, reason, audit evidence id, request path, method, and
correlation id.

OPM-07 management activity can show OPM-08 events, but only through the existing
safe metadata allow-list. The free-form reason stays in audit storage and is not
rendered by the activity timeline.

## Management UI

The `/admin/` Management project detail now includes a Project role definitions
section. Product Owners can see active/disabled custom roles, dependency counts,
existing descriptions and templates, plus create/update forms that require a
reason and audit evidence id.

The OPM-06 success benchmark evidence includes the role-definition surface in
the raw-payload leakage check and records the managed custom-role count for the
selected project.

## Acceptance Criteria

- Product Owners can list custom project roles from the Management view and API.
- Product Owners can create or update custom project role definitions with
  reason and audit evidence.
- Disabling refuses roles with active assignments or role-targeted grants.
- Default role templates cannot be overwritten as project-defined roles.
- OPM-08 audit evidence is payload-safe and appears in OPM-07 activity without
  exposing free-form reason text.
- Static, database-backed, UI bundle, and browser smoke checks cover the slice.

## Verification

Focused verification:

```bash
dotnet test tests/MemorySystem.UnitTests/MemorySystem.UnitTests.csproj --filter FullyQualifiedName~OrganizationProjectManagementOpm08Tests
MEMORYSYSTEM_REQUIRE_DATABASE_TESTS=true dotnet test tests/MemorySystem.IntegrationTests/MemorySystem.IntegrationTests.csproj --filter FullyQualifiedName~ApiAdminProjectRoleDefinitionManagementTests
cd tools/ui
npm run build
npm run check
```
