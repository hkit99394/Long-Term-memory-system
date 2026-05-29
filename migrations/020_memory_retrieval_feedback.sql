CREATE TABLE memory_retrieval_feedback (
    id UUID PRIMARY KEY,
    principal_id UUID NOT NULL REFERENCES principals(id),
    retrieval_mode TEXT NOT NULL,
    query_hash TEXT NOT NULL,
    target_scope_type TEXT,
    target_scope_id TEXT,
    role_id TEXT,
    source_type TEXT,
    source_id UUID,
    feedback_type TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CHECK (retrieval_mode IN ('context_packet')),
    CHECK (query_hash LIKE 'sha256:%'),
    CHECK (
        (target_scope_type IS NULL AND target_scope_id IS NULL)
        OR (target_scope_type IS NOT NULL AND target_scope_id IS NOT NULL)
    ),
    CHECK (target_scope_type IS NULL OR target_scope_type IN ('global', 'org', 'user', 'project', 'role', 'agent', 'session')),
    CHECK (role_id IS NULL OR role_id IN ('designer', 'developer', 'cto', 'cfo', 'coo', 'ceo')),
    CHECK (
        (source_type IS NULL AND source_id IS NULL)
        OR (source_type IS NOT NULL AND source_id IS NOT NULL)
    ),
    CHECK (source_type IS NULL OR source_type IN ('memory_fact', 'role_memory_lens')),
    CHECK (feedback_type IN ('useful', 'stale', 'missing', 'noisy'))
);

CREATE INDEX ix_memory_retrieval_feedback_principal_created_at
    ON memory_retrieval_feedback (principal_id, created_at DESC);

CREATE INDEX ix_memory_retrieval_feedback_feedback_type_created_at
    ON memory_retrieval_feedback (feedback_type, created_at DESC);

CREATE INDEX ix_memory_retrieval_feedback_source
    ON memory_retrieval_feedback (source_type, source_id)
    WHERE source_type IS NOT NULL;
