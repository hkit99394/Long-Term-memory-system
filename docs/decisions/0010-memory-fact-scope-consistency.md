# 0010 Memory Fact Scope Consistency

## Status

Accepted.

## Context

Memory facts carry both canonical scope metadata and typed owner columns. For example, project memory stores `scope_type = 'project'`, `scope_id = project_id::text`, `project_id`, `org_id`, and a `/project/{project_id}/...` namespace.

Those columns are deliberately redundant so retrieval predicates can be fast and auditable. The redundancy is only safe if the database rejects rows where the canonical scope id, namespace prefix, and owner columns disagree.

## Decision

Add a named `memory_facts` check constraint, `ck_memory_facts_scope_owner_namespace_consistency`, that validates every supported scope shape:

- global facts use `scope_id = 'global'`, no owner columns, and `/global/`.
- organization facts use `org_id`, no narrower owner columns, and `/org/{org_id}/`.
- user facts use `user_principal_id` and `/user/{principal_id}/`.
- project facts use `project_id`, `org_id`, and `/project/{project_id}/`.
- role facts use `role_id` and `/role/{role_id}/`.
- agent facts use `agent_principal_id` and `/agent/{principal_id}/`.
- session facts use a non-global session id, no owner columns, and `/session/{scope_id}/`.

The constraint uses exact prefix comparison instead of SQL `LIKE` so scope ids containing wildcard characters cannot accidentally authorize a different namespace prefix. The existing project-to-organization foreign key remains responsible for proving `memory_facts.project_id` belongs to `memory_facts.org_id`.

## Consequences

- Memory fact owner columns cannot be updated independently from `scope_id` and namespace.
- Database-backed tests now exercise both owner-column drift and wildcard-sensitive namespace drift.
- [Decision 0012](0012-memory-namespace-parser.md) adds a namespace parser for cleaner application-level validation and future read-path query construction.
