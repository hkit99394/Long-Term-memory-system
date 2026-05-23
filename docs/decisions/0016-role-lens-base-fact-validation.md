# 0016 Role Lens Base Fact Validation

## Status

Accepted.

## Context

M4-04 needs role memory lenses to validate the scope of their base memory fact before insertion. The database trigger on `role_memory_lenses` already prevents invalid rows, but the repository should make the rule explicit for application callers.

The rule separates reusable role guidance from project-specific interpretation:

- global shared role principles must reference global memory facts.
- organization shared role principles must reference memory facts from the same organization.
- project-role lenses must reference either target project facts or same-organization facts.

## Decision

Validate the base memory fact in `PostgresRoleMemoryLensRepository.StoreAsync`.

Before inserting a role memory lens, the repository reads the base memory fact's `scope_type`, `org_id`, and `project_id` and rejects invalid combinations with an application-level error:

- global role lens plus non-global base fact is rejected.
- organization role lens plus non-organization or different-organization base fact is rejected.
- project role lens plus global, different-project, or different-organization base fact is rejected.
- project role lens plus target-project or same-organization base fact is accepted.

The PostgreSQL trigger remains in place as a defense-in-depth check for direct SQL writes and future services.

## Consequences

- Repository callers get clear role-lens validation failures before PostgreSQL constraint errors.
- Shared role principles cannot accidentally depend on project-specific truth.
- Project-role lenses cannot borrow facts from unrelated projects.
- The M4 repository contract now mirrors the database scope rules that later context-building will rely on.
