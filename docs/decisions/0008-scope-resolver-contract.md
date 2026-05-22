# 0008 Scope Resolver Contract

## Status

Accepted.

## Context

M3 begins access and scope enforcement. Before membership and grant checks can be authoritative, requests need one shared place to normalize and resolve scope identifiers.

M2 already stored events and memory proposals with scope metadata, but project organization derivation and scope reference checks were split across mappers, stores, and database constraints.

## Decision

The application layer owns a scope resolver for request scope metadata. It resolves:

- `global`: canonical `scopeId = global`.
- `org`: existing organization id.
- `project`: existing project id plus derived organization id.
- `user`: the authenticated principal as the user scope.
- `agent`: active agent principal id.
- `role`: supported role id.
- `session`: non-global session id.

The API uses the resolver before appending events. The memory proposal workflow uses the resolver before broker decisions and source-event matching.

The resolver depends on an application interface for scope references. PostgreSQL-backed existence and project-to-organization lookup live in infrastructure.

## Consequences

- Event and proposal requests now share canonical scope normalization.
- Project scope no longer depends on clients to supply the organization id.
- M3-02 can layer membership and grant checks on resolved scope metadata rather than parsing raw request fields again.
- Database constraints remain the final safety net for scope consistency.
