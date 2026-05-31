CREATE TABLE memory_context_packets (
    id UUID PRIMARY KEY,
    principal_id UUID NOT NULL REFERENCES principals(id),
    query_hash TEXT NOT NULL,
    target_scope_type TEXT,
    target_scope_id TEXT,
    role_id TEXT,
    item_count INTEGER NOT NULL,
    first_observed_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    last_observed_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    observation_count BIGINT NOT NULL DEFAULT 1,
    CHECK (query_hash LIKE 'sha256:%'),
    CHECK (item_count >= 0),
    CHECK (observation_count > 0),
    CHECK (
        (target_scope_type IS NULL AND target_scope_id IS NULL)
        OR (target_scope_type IS NOT NULL AND target_scope_id IS NOT NULL)
    ),
    CHECK (target_scope_type IS NULL OR target_scope_type IN ('global', 'org', 'user', 'project', 'role', 'agent', 'session')),
    CHECK (role_id IS NULL OR role_id IN ('designer', 'developer', 'cto', 'cfo', 'coo', 'ceo'))
);

CREATE INDEX ix_memory_context_packets_principal_observed
    ON memory_context_packets (principal_id, last_observed_at DESC);

CREATE INDEX ix_memory_context_packets_query_scope_role
    ON memory_context_packets (query_hash, target_scope_type, target_scope_id, role_id);
