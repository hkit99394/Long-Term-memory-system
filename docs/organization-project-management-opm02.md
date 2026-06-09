# Organization And Project Management OPM-02

Status: OPM-02 implemented.

Owner: Product Owner + Designer + Developer.

Created: 2026-06-09.

## Purpose

OPM-02 turns the OPM-01 organization/project read model into the first admin UI
management shell. The goal is discovery and inspection: Product Owners should
be able to find existing organizations and projects, inspect safe counts and
status evidence, and jump to the existing Registration or Access surfaces for
follow-up work.

Lifecycle changes, settings edits, access inventory/revocation, and grant
matrix editing remain later OPM slices.

## UI Scope

`/admin/` now includes a `Management` view. The view is bundled from
`tools/ui/src/admin/management-panel.ts` into
`src/MemorySystem.Api/wwwroot/admin/admin-console.js`.

The shell provides:

- one result pane with Organizations and Projects sections
- search across organization/project names and ids
- project status filtering for all, planned, active, archived, or deleted
- organization detail with project status counts and membership/grant totals
- project detail with status, role counts, membership counts, grant counts, and
  latest payload-safe registration evidence
- loading, empty, error, and not-visible states
- action buttons into Project Registration and Access Management
- a payload-safe JSON evidence pane for the selected detail record

The UI calls:

```text
GET /api/admin/organizations
GET /api/admin/organizations/{organizationId}
GET /api/admin/projects
GET /api/admin/projects/{projectId}
```

## Access Behavior

The UI does not broaden visibility. It renders whatever OPM-01 returns:

- org admin/owner callers can see organization rows and details
- direct project admins can see their project rows without organization
  directory access
- missing or hidden detail responses show a not-visible state instead of
  disclosing whether another organization/project exists

## Verification

Focused verification:

```bash
dotnet test tests/MemorySystem.UnitTests/MemorySystem.UnitTests.csproj --filter FullyQualifiedName~OrganizationProjectManagementOpm02Tests
cd tools/ui
npm run build
npm run check
```
