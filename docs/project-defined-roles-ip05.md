# Project-Defined Roles IP-05

Date completed: 2026-06-07

Status: implemented

## Purpose

IP-05 lets each project define its own role vocabulary without losing the
default role set that earlier releases use for role lenses, assignments, and
grants.

Default roles are now default role templates. They remain available for global
role scope, organization role lenses, and project role lenses:

- `product_owner`
- `cto`
- `security_professional`
- `it_manager`
- `developer`
- `tester_qa`
- `release_manager`
- `knowledge_steward`
- `designer`
- `cfo`
- `coo`
- `ceo`

Projects can add project-specific role ids such as `implementation_lead` or
`qa-lead`. Custom role ids are valid only as project role definitions; they do
not become shared global or organization roles.

## Runtime Contract

Project role definitions are stored in `project_role_definitions`.

Each definition records:

- project id
- role id
- display name
- optional description
- optional default template role id
- status: `active` or `disabled`
- created and updated timestamps

Role ids are normalized to lower case and must match:

```text
^[a-z][a-z0-9_-]{0,63}$
```

## Admin API

Create or update a project role definition with:

```http
POST /api/admin/access/project-roles
```

Example:

```json
{
  "projectId": "44444444-4444-4444-8444-444444444444",
  "roleId": "implementation_lead",
  "displayName": "Implementation Lead",
  "description": "Owns sequencing, merge readiness, and delivery risk.",
  "templateRoleId": "developer",
  "status": "active"
}
```

The caller must have admin access to the target project. The change is recorded
as `project_role_definition_change` in access audit events.

Project admins can also create grants for project role-lens namespaces without
holding the target role themselves. Runtime reads still require the requesting
principal to have both the matching role assignment and namespace grant.

## Validation Rules

- Project-scoped role assignments can use a default template role or an active project role definition.
- Project namespace grants can target a default template role or an active
  project role definition.
- Project role-lens proposals can use a default template role or an active
  project role definition.
- Global role scope and organization role lenses keep the default template role
  set only.
- Disabled or undefined project roles fail closed before assignment, grant, or
  role-lens write.

Project-defined role lenses use the existing project role namespace shape:

```text
/project/{projectId}/role/{roleId}/lens
```

The role lens rule still applies: role lenses are role-specific interpretation
over shared truth, not separate facts or private policy stores.
