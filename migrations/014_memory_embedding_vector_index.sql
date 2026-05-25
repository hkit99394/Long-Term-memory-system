CREATE INDEX IF NOT EXISTS ix_memory_embeddings_embedding_32_hnsw_cosine
    ON memory_embeddings
    USING hnsw ((embedding::vector(32)) vector_cosine_ops)
    WHERE embedding_dimension = 32;
