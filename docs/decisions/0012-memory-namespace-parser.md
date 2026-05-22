# 0012 Memory Namespace Parser

## Status

Accepted.

## Context

Earlier M3 work validated namespaces with scope-specific string prefixes. That was enough to block obvious drift, but retrieval and future context-building code need structured namespace metadata rather than repeated ad hoc string checks.

Namespace strings still are not authorization by themselves. They are parsed so application code can compare namespace scope metadata to resolved scope metadata before asking membership, role, and grant checks for an executable decision.

## Decision

Add an application-layer namespace parser for the supported namespace shapes:

- `/global/{category...}`
- `/org/{org_id}/{category...}`
- `/user/{user_id}/{category...}`
- `/project/{project_id}/{category...}`
- `/project/{project_id}/role/{role_id}/lens...`
- `/role/{role_id}/{category...}`
- `/agent/{agent_id}/{category...}`
- `/session/{session_id}/{category...}`

The parser returns canonical `scope_type`, `scope_id`, path segments, and role id when a namespace carries a role segment. It rejects empty path segments, relative path segments, unsupported scope types, malformed GUID ids, unsupported role ids, and `global` as a session namespace id.

Proposal scope normalization now uses the parser and then validates that the parsed namespace scope matches the resolved request scope. Existing public error wording remains stable for scope mismatch errors.

## Consequences

- Application validation can reason over namespace parts without re-splitting strings.
- Proposal writes now have structured namespace validation before scope resolution, access checks, and database constraints.
- Database constraints remain the final safety net for persisted rows.
- Future search and context retrieval can build authorized predicates from parsed namespace metadata.
