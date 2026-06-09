# Organization And Project Management OPM-01

Status: OPM-01 through OPM-08 implemented.

Owner: Product Owner + Developer + Security Professional.

Created: 2026-06-09.

## Purpose

OPM-01 gives the admin surface a safe read model for organization and project
management. Project registration can already create a governed project
boundary, but Product Owners also need to list and inspect existing
organizations/projects before changing lifecycle, scope, roles, grants, or
settings.

This slice is intentionally read-only. Mutation workflows remain in the
existing registration and access-management APIs until later OPM items add
dedicated management UX and settings flows.

## API Contract

All OPM-01 endpoints require normal admin authentication and use the admin rate
limit:

```text
GET /api/admin/organizations?q=&limit=&cursor=
GET /api/admin/organizations/{organizationId}
GET /api/admin/projects?orgId=&status=&q=&limit=&cursor=
GET /api/admin/projects/{projectId}
```

Responses include:

- `contractId: OPM-01`
- payload-safe organization/project identifiers, names, statuses, timestamps,
  and the caller's management access level
- aggregate counts for projects, memberships, role assignments, role
  definitions, namespace grants, and project status buckets
- cursor pagination with `limit` and `nextCursor`
- latest project-registration audit evidence on project detail when available
- `payloadSafe: true` and `rawSourcePayloadsIncluded: false`

Responses do not include raw source documents, memory payloads, event payloads,
credential material, or free-form registration notes.

## Access Rules

Organization list/detail is visible only to callers with direct `admin` or
`owner` organization membership. Detail endpoints return `404` for missing or
inaccessible organizations so callers cannot distinguish absence from lack of
visibility.

Project list/detail is visible to callers with direct `admin` or `owner`
membership on the parent organization, or direct `admin` membership on the
project. Detail endpoints return `404` for missing or inaccessible resources so
callers cannot distinguish absence from lack of visibility.

Organization- and project-scoped management section endpoints use the same
no-leak rule. Missing or inaccessible scopes return generic `404` responses
rather than exposing parent ids or authorization decision details.

Direct project admins can list and inspect their projects without org directory leak.
Organization directory access still requires direct organization admin or owner
membership.

OPM-01 is a directory/read-model slice, not an access-preview replacement. When
an operator is about to change memberships, roles, grants, or namespaces, the
existing access-management preview and write endpoints remain the executable
policy gate.

## Target And Follow-On

| ID | Target date | Owner | Target | Acceptance criteria |
| --- | --- | --- | --- | --- |
| OPM-01 | 2026-06-09 | Product Owner + Developer + Security Professional | Admin org/project read model | Done. List/detail endpoints expose payload-safe, access-filtered organization and project summaries with pagination, counts, statuses, and latest registration evidence. |
| OPM-02 | 2026-06-16 | Product Owner + Designer + Developer | Admin management UI shell | Done. `/admin/` includes a Management view backed by OPM-01 endpoints, with Organizations and Projects sections, project status filtering, payload-safe detail evidence, and loading, empty, error, and not-visible states. |
| OPM-03 | 2026-06-23 | Product Owner + Security Professional + Developer | Lifecycle and scope settings | Done. [Organization And Project Management OPM-03](organization-project-management-opm03.md) adds guarded lifecycle/status and scope settings workflows with payload-safe audit evidence. |
| OPM-04 | 2026-06-30 | Security Professional + Ops + Developer | Access inventory and revocation | Done. [Organization And Project Management OPM-04](organization-project-management-opm04.md) adds payload-safe org/project access inventory, stale-access prompts, and audited revocation for memberships, role assignments, and namespace grants. |
| OPM-05 | 2026-07-07 | Product Owner + Knowledge Steward | Grant matrix management | Done. [Organization And Project Management OPM-05](organization-project-management-opm05.md) adds role/namespace grant matrix review and edit workflows using least-privilege presets and effective-access previews. |
| OPM-06 | 2026-07-14 | Product Owner + Tester/QA | Management success benchmark | Done. [Organization And Project Management OPM-06](organization-project-management-opm06.md) tracks whether Product Owners can find, inspect, and safely modify organization/project management data without SQL, raw payload exposure, or unmeasured access drift. |
| OPM-07 | 2026-07-21 | Product Owner + Security Professional + Developer | Management activity timeline | Done. [Organization And Project Management OPM-07](organization-project-management-opm07.md) adds payload-safe organization/project management activity endpoints and UI timeline backed by `access_audit_events` with whitelisted metadata only. |
| OPM-08 | 2026-07-28 | Product Owner + Knowledge Steward + Security Professional + Developer | Project role definition management | Done. [Organization And Project Management OPM-08](organization-project-management-opm08.md) adds payload-safe custom project role definition list/create/update/disable workflows with dependency guards, audited evidence, and Management UI support. |

## Verification

Focused verification:

```bash
dotnet test tests/MemorySystem.UnitTests/MemorySystem.UnitTests.csproj --filter FullyQualifiedName~OrganizationProjectManagementOpm01Tests
MEMORYSYSTEM_REQUIRE_DATABASE_TESTS=true dotnet test tests/MemorySystem.IntegrationTests/MemorySystem.IntegrationTests.csproj --filter FullyQualifiedName~ApiAdminOrganizationProjectManagementTests
```
