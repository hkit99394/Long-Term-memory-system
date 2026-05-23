# 0015 Role Memory Lens Repository

## Status

Accepted.

## Context

M4-03 needs an application data-access boundary for `role_memory_lenses`. Role lenses are not standalone memory facts. They are role-specific interpretations over base memory facts, and the schema stores them separately from `memory_facts`.

The repository must preserve the distinction between shared role principles and project-role lenses:

- shared role principles use `role_memory_lenses` rows without a project id, scoped to global or organization memory.
- project-role lenses use `role_memory_lenses` rows with a project id, scoped to the target project.

The database owns the deeper base-fact validation rules, with repository-level validation recorded in [Decision 0016](0016-role-lens-base-fact-validation.md).

## Decision

Add `IRoleMemoryLensRepository` with:

- `FindAsync(id)` for direct role memory lens lookup.
- `FindByScopeAsync(query)` for active role memory lens retrieval by role id and global, organization, or project scope.
- `StoreAsync(command)` for inserting a role memory lens with source-event provenance.

Add a PostgreSQL implementation backed by `role_memory_lenses`.

The repository accepts only global, organization, and project scopes:

- global scope stores shared role principles with `scope_type = 'global'`, `scope_id = 'global'`, and no owner ids.
- organization scope stores shared role principles with `scope_type = 'org'`, `scope_id = org_id`, and no project id.
- project scope stores project-role lenses with `scope_type = 'project'`, `scope_id = project_id`, and both project and organization ids.

The repository validates supported role ids, supported status values, confidence range, non-empty base memory fact ids, and non-empty source event ids before issuing SQL.

## Consequences

- Memory facts and role memory lenses now have separate application repositories that mirror their separate tables.
- Shared role principles and project-role lenses can be stored and queried independently by scope.
- Future context-building can combine shared role memory and project-role lenses without conflating them with base project facts.
- Base-fact scope validation remains enforced by PostgreSQL and is also checked by the repository.
