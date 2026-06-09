# Organization And Project Management OPM-07

Status: OPM-07 implemented.

Owner: Product Owner + Security Professional + Developer.

Created: 2026-06-09.

## Purpose

OPM-07 adds a payload-safe management activity timeline for organization and
project management. After Product Owners change lifecycle, scope settings,
access inventory, or grant matrices, they need a fast way to answer:

```text
What changed, who changed it, when did it happen, and what audit evidence
backs the change?
```

The timeline reads from `access_audit_events`; it does not create a second
activity log. It only returns management action types and whitelisted metadata
fields.

## API Contract

All OPM-07 endpoints require normal admin authentication and use the admin rate
limit:

```text
GET /api/admin/organizations/{organizationId}/management-activity?limit=&cursor=
GET /api/admin/projects/{projectId}/management-activity?limit=&cursor=
```

Responses include:

- `contractId: OPM-07`
- organization or project scope summary
- paged audit entries ordered newest first
- actor and target principal ids when available
- action type, outcome, scope, resource, request method/path, and correlation id
- payload-safe metadata such as `contractId`, `operation`, `auditEvidenceId`,
  registration evidence ids, source-document counts, status changes, grant
  counts, preset ids, and access record type
- `payloadSafe: true` and `rawSourcePayloadsIncluded: false`

Responses do not include raw source documents, memory payloads, request bodies,
free-form change reasons, API keys, database connection strings, or raw audit
metadata blobs.

## Access Rules

Organization activity uses the existing organization management boundary:
callers must have admin access to the organization.

Project activity uses the existing project management boundary: callers must
have admin access to the active project or owner/admin access to the parent
organization. Missing or inaccessible project scopes return generic `404`
responses so callers cannot infer hidden projects or parent organizations.

Organization activity includes direct organization-scoped management events and
project-scoped management events for projects under the organization. Project
activity includes events scoped to the project and project-resource audit
records such as registration and grant-matrix evidence.

## UI Evidence

The `/admin/` Management view now loads the activity timeline with the selected
organization or project detail. The activity section shows entry counts,
summaries, outcomes, timestamps, request/resource/scope context, principal ids,
role or permission context, and whitelisted metadata.

The Management source rail uses the activity response as payload-safe evidence
and includes a Management Activity API link. The OPM-06 benchmark raw-payload
check includes the OPM-07 response flag.

## Verification

Focused verification:

```bash
dotnet test tests/MemorySystem.UnitTests/MemorySystem.UnitTests.csproj --filter FullyQualifiedName~OrganizationProjectManagementOpm07Tests
MEMORYSYSTEM_REQUIRE_DATABASE_TESTS=true dotnet test tests/MemorySystem.IntegrationTests/MemorySystem.IntegrationTests.csproj --filter FullyQualifiedName~ApiAdminManagementActivityTests
cd tools/ui
npm run build
npm run check
```
