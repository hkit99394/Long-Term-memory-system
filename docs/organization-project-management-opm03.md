# Organization And Project Management OPM-03

Status: OPM-03 implemented.

Owner: Product Owner + Security Professional + Developer.

Created: 2026-06-09.

## Purpose

OPM-03 turns the Management surface from inspection-only into the first guarded
management workflow. Authorized operators can change project lifecycle status
and persist project scope settings without raw source payload exposure or SQL.

## API Contract

The API adds:

```text
GET /api/admin/projects/{projectId}/scope-settings
PATCH /api/admin/projects/{projectId}/lifecycle
PUT /api/admin/projects/{projectId}/scope-settings
```

Lifecycle updates accept `projectStatus`, `reason`, and `auditEvidenceId`.
Statuses are `planned`, `active`, `archived`, or `deleted`.

Scope settings updates accept `defaultNamespacePrefix`, `sourceHashRequired`,
`memoryRetentionClass`, `reviewCadenceDays`, `reason`, and `auditEvidenceId`.
The default namespace must stay below `/project/{projectId}/...`; project-root
and cross-project prefixes are rejected. Retention is limited to `ephemeral`,
`standard`, `audit`, or `legal_hold`, and review cadence is 1 to 365 days.

Responses use `contractId: OPM-03`, `payloadSafe: true`, and
`rawSourcePayloadsIncluded: false`.

Free-form `reason` and audit evidence ids are required and capped at 500
characters.

## Audit And Access

All writes require admin access to the active project or owner/admin access to
the parent organization. Inactive project lifecycle and settings changes use
organization authorization, because project-scope runtime authorization is
intentionally active-project only.

Missing or inaccessible project-management scopes return generic `404`
responses, matching OPM-01 no-leak project detail behavior.

Successful writes create payload-safe audit events:

- `project_lifecycle_change`
- `project_scope_settings_change`

Audit metadata records only ids, status names, namespace prefixes, reason text,
retention class, cadence, and caller-supplied audit evidence id.

Lifecycle and scope-settings mutations commit in the same database transaction
as their audit event. If audit evidence cannot be written, the management
change is rolled back.

## UI Scope

The `/admin/` Management project detail now shows default or stored scope
settings and provides compact forms for:

- lifecycle status update
- default namespace prefix
- source hash requirement
- retention class
- review cadence

Both forms require reason and audit evidence id before submission. The source
evidence pane shows the payload-safe response from the latest lifecycle or
settings action.

## Verification

Focused verification:

```bash
dotnet test tests/MemorySystem.UnitTests/MemorySystem.UnitTests.csproj --filter FullyQualifiedName~OrganizationProjectManagementOpm03Tests
MEMORYSYSTEM_REQUIRE_DATABASE_TESTS=true dotnet test tests/MemorySystem.IntegrationTests/MemorySystem.IntegrationTests.csproj --filter FullyQualifiedName~ApiAdminProjectLifecycleSettingsTests
cd tools/ui
npm run build
npm run check
```
