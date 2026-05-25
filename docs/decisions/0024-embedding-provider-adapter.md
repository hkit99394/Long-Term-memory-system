# 0024 Embedding Provider Adapter

## Status

Accepted.

## Context

M6-02 prepares semantic retrieval by adding an embedding boundary for memory chunks. The schema already stores embeddings by `chunk_id`, `embedding_model`, `embedding_dimension`, and pgvector value, but the worker did not yet produce rows.

The first implementation needs to prove the provider boundary, model selection, dimension validation, and idempotent storage path without requiring external embedding credentials during local development or CI.

## Decision

Add an application-level embedding provider contract that returns a model name, dimension, and vector values for chunk text.

For local development and tests, register a deterministic provider selected by the `Embeddings` configuration section:

```text
Embeddings:Provider=deterministic
Embeddings:Model=memory-deterministic-v1
Embeddings:Dimension=32
```

The deterministic provider hashes the selected model plus chunk input into a normalized vector with the configured dimension. It is not intended for relevance quality; it is a stable adapter implementation for exercising the storage and retrieval pipeline.

The indexing outbox handler now verifies that the payload references a searchable memory chunk, embeds that chunk text, and upserts `memory_embeddings` by `(chunk_id, embedding_model)`. The existing database constraint keeps `embedding_dimension` aligned with `vector_dims(embedding)`.

Production deployments must not use the deterministic provider for semantic retrieval or indexing. The production provider is selected with:

```text
Embeddings:Provider=openai
Embeddings:Model=text-embedding-3-small
Embeddings:Dimension=1536
Embeddings:Endpoint=https://api.openai.com/v1/embeddings
Embeddings:ApiKey=<secret>
```

The OpenAI endpoint must be HTTPS. Non-Development and non-Testing API semantic routes return unavailable when configured for deterministic embeddings, and the worker refuses to start semantic indexing with deterministic embeddings.

## Consequences

- Chunks can be embedded with a selected model and dimension without external network dependencies.
- Reprocessing an indexing outbox job is idempotent for the same chunk and model.
- Later provider implementations can replace the deterministic adapter behind the same application contract.
- M6-03 can build pgvector semantic search against populated `memory_embeddings` rows.
