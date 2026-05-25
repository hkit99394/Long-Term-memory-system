# 0023 Authorized Full-Text Memory Search

## Status

Accepted.

## Context

M6-01 starts hybrid retrieval by making keyword search executable over indexed memory chunks. The schema already had `memory_chunks.search_vector`, a trigger, and a GIN index, but the read path needed an explicit query surface that proves full-text candidate selection happens only inside authorization predicates.

The context builder is not complete yet, so this milestone focuses on a small full-text search API and repository boundary rather than ranking, compression, vector retrieval, or context packet assembly.

## Decision

Add a PostgreSQL-backed full-text memory chunk search using `websearch_to_tsquery('english', @query)` against `memory_chunks.search_vector`.

The search query only returns chunks whose source memory fact or role memory lens is active. It applies principal scope, membership, role assignment, namespace grant, and read-permission predicates inside the SQL query before ranking and limiting results.

The API exposes this path as:

```text
GET /api/memory/search?q={query}&limit={1..50}
```

The response returns the authorized matching chunks, source ids, scope, namespace, rank, trust level, and source event id.

Add migration `012_memory_chunk_full_text_search.sql` to recreate the search-vector trigger, backfill existing chunks, make `search_vector` non-null, and ensure the GIN index exists.

## Consequences

- Unauthorized rows do not enter full-text candidate sets or ranking.
- Existing memory chunk inserts and updates keep `search_vector` populated.
- Full-text retrieval is available as a simple API while later M6 work adds embedding, vector, hybrid ranking, and context packets.
- The SQL access predicate intentionally duplicates the current read authorization rules so PostgreSQL can filter before ranking.
