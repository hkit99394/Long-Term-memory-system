# 0011 Direct Memory Fact Read Access

## Status

Accepted.

## Context

M3-04 needs an executable blocked cross-project read test before full retrieval and context building exist. The long-term API plan includes `GET /api/memory/{id}` for retrieving one memory fact, while M6 will handle structured, full-text, vector, and context-packet reads.

Direct memory reads still need the same fine-grained policy as later retrieval: a caller must have access to the memory fact's resolved scope and namespace.

## Decision

Add a narrow `GET /api/memory/{id}` endpoint that retrieves one memory fact by id through an application read service.

The read service:

- loads the memory fact metadata and content by id.
- reconstructs a `MemoryScopeResolution` from the stored scope and owner columns.
- asks `IMemoryAccessAuthorizer` for `read` access using the memory fact namespace.
- returns the memory fact only when access is allowed.

Missing and unauthorized memory facts both return `404` with the same public shape. This keeps direct reads from confirming whether a specific inaccessible memory id exists.

## Consequences

- A principal with Project A access cannot read a Project B memory fact without explicit Project B membership and namespace grant.
- The direct read path now uses the M4 memory facts repository, but it does not replace M6 authorized retrieval queries.
- Future search/context retrieval must still apply authorization inside candidate queries, before ranking or candidate counts are observable.
