# 0025 Authorized pgvector Semantic Search

## Status

Accepted.

## Context

M6-03 adds the vector retrieval half of hybrid search. M6-02 already writes `memory_embeddings` rows for indexed chunks with a selected model and dimension, so semantic search can query those rows without introducing a separate vector database.

The main safety requirement is that unauthorized rows must not enter the vector ranking set. Distance calculations, ordering, limits, and response counts should reflect only rows the caller may read.

## Decision

Add a PostgreSQL-backed semantic chunk search endpoint:

```text
GET /api/memory/search/semantic?q={query}&limit={1..50}
```

The API embeds the query with the configured embedding provider, then searches `memory_embeddings` rows matching that provider's model and dimension.

Use pgvector cosine distance via the `<=>` operator. The response keeps the existing memory search shape and maps distance to a higher-is-better rank as `1 - cosine_distance`.

The SQL query first builds an `authorized_chunks AS MATERIALIZED` candidate set that includes only active, non-redacted chunks the principal may read by scope, membership, role assignment, and namespace grant. The vector distance expression is applied only against that materialized authorized set.

Do not add an ANN vector index yet. The schema still allows configurable model and dimension, so this slice uses the existing `(embedding_model, embedding_dimension)` narrowing index plus exact pgvector ordering. A model-specific HNSW or IVFFlat index should be added once the production embedding model and dimension are fixed.

## Consequences

- Semantic search runs inside PostgreSQL with pgvector and the same authorization rules as full-text memory search.
- Unauthorized rows are excluded before cosine distance ranking and limiting.
- The endpoint is ready for M6-04 hybrid ranking to combine full-text and semantic result sets.
- Exact vector ordering is acceptable for the MVP data size; larger deployments will need a model-specific ANN index decision.
