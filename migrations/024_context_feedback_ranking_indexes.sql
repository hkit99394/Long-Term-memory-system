CREATE INDEX ix_memory_retrieval_feedback_context_source_signal
    ON memory_retrieval_feedback (
        source_type,
        source_id,
        target_scope_type,
        target_scope_id,
        role_id,
        created_at DESC
    )
    WHERE retrieval_mode = 'context_packet'
        AND source_type IS NOT NULL;

CREATE INDEX ix_memory_retrieval_feedback_context_missing_signal
    ON memory_retrieval_feedback (
        query_hash,
        target_scope_type,
        target_scope_id,
        role_id,
        created_at DESC
    )
    WHERE retrieval_mode = 'context_packet'
        AND feedback_type = 'missing'
        AND source_type IS NULL;
