# 0009 Membership and Grant Access Checks

## Status

Accepted.

## Context

M3-01 added one scope resolver for global, organization, project, user, role, agent, and session requests. M3-02 needs executable authorization decisions on top of those resolved scope records so event append and memory proposal writes no longer rely on namespace strings alone.

The schema already has organization memberships, project memberships, role assignments, and memory access grants. The application needs one policy boundary that combines those records consistently.

## Decision

The application layer owns `IMemoryAccessAuthorizer`. API handlers and workflows ask it for fine-grained `read`, `write`, `review`, or `admin` decisions using the authenticated principal, resolved scope, and optional namespace.

Scope access rules:

- `global` and `session` scopes are allowed after successful authentication and scope resolution.
- `user` scope requires the resolved user principal to match the authenticated principal.
- `agent` scope requires the resolved agent principal to match the authenticated principal.
- `role` scope requires a matching role assignment.
- `org` scope requires an organization membership level high enough for the requested permission.
- `project` scope requires a project membership level high enough for the requested permission, or organization `admin`/`owner` access to the project's organization.

Membership levels map upward by permission:

- `read`: reader, contributor, reviewer, admin, and organization owner.
- `write`: contributor, reviewer, admin, and organization owner.
- `review`: reviewer, admin, and organization owner.
- `admin`: admin and organization owner.

Namespace grants are required only when a request includes a namespace. This lets `POST /api/events` enforce append permission for the resolved scope, while `POST /api/memory/proposals` requires both scope access and a namespace write grant before durable memory is stored.

The PostgreSQL access reference store checks direct principal grants and role-based grants. Grant prefixes apply to the exact namespace or child namespaces. Grant permissions also map upward for read requests: a write, review, or admin grant satisfies read access; write is satisfied by write or admin; review is satisfied by review or admin; admin requires admin.

## Consequences

- Project and organization event appends now return `403` when the authenticated principal lacks membership.
- Stored memory proposals now return `403` and skip durable writes when the principal lacks either scope access or a namespace write grant.
- Access SQL stays in infrastructure, while API endpoints and application workflows share one authorization policy surface.
- M3-03 and M3-05 still need stricter consistency checks between namespace strings and resolved scope metadata.
- M3-04 still needs the blocked cross-project read test once a read path exists.
