CREATE EXTENSION IF NOT EXISTS vector;

CREATE TABLE principals (
    id UUID PRIMARY KEY,
    principal_type TEXT NOT NULL,
    display_name TEXT NOT NULL,
    status TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CHECK (principal_type IN ('human', 'agent', 'service')),
    CHECK (status IN ('active', 'disabled', 'deleted'))
);

CREATE TABLE organizations (
    id UUID PRIMARY KEY,
    name TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE projects (
    id UUID PRIMARY KEY,
    org_id UUID NOT NULL REFERENCES organizations(id),
    name TEXT NOT NULL,
    status TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CHECK (status IN ('active', 'archived', 'deleted'))
);

CREATE TABLE organization_memberships (
    org_id UUID NOT NULL REFERENCES organizations(id),
    principal_id UUID NOT NULL REFERENCES principals(id),
    access_level TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (org_id, principal_id),
    CHECK (access_level IN ('reader', 'contributor', 'reviewer', 'admin', 'owner'))
);

CREATE TABLE project_memberships (
    project_id UUID NOT NULL REFERENCES projects(id),
    principal_id UUID NOT NULL REFERENCES principals(id),
    access_level TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (project_id, principal_id),
    CHECK (access_level IN ('reader', 'contributor', 'reviewer', 'admin'))
);

CREATE TABLE role_assignments (
    id UUID PRIMARY KEY,
    principal_id UUID NOT NULL REFERENCES principals(id),
    role_id TEXT NOT NULL,
    scope_type TEXT NOT NULL,
    scope_id UUID,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CHECK (role_id IN ('designer', 'developer', 'cto', 'cfo', 'coo', 'ceo')),
    CHECK (scope_type IN ('global', 'org', 'project')),
    CHECK (
        (scope_type = 'global' AND scope_id IS NULL)
        OR (scope_type IN ('org', 'project') AND scope_id IS NOT NULL)
    )
);

CREATE TABLE memory_access_grants (
    id UUID PRIMARY KEY,
    principal_id UUID REFERENCES principals(id),
    role_id TEXT,
    namespace_prefix TEXT NOT NULL,
    permission TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CHECK (permission IN ('read', 'write', 'review', 'admin')),
    CHECK (principal_id IS NOT NULL OR role_id IS NOT NULL),
    CHECK (role_id IS NULL OR role_id IN ('designer', 'developer', 'cto', 'cfo', 'coo', 'ceo')),
    CHECK (namespace_prefix LIKE '/%')
);

CREATE TABLE events (
    id UUID PRIMARY KEY,
    principal_id UUID REFERENCES principals(id),
    conversation_id UUID,
    agent_principal_id UUID REFERENCES principals(id),
    role_id TEXT,
    event_type TEXT NOT NULL,
    content JSONB NOT NULL,
    content_hash TEXT,
    external_payload_uri TEXT,
    retention_class TEXT NOT NULL DEFAULT 'standard',
    sensitivity TEXT NOT NULL DEFAULT 'none',
    redaction_status TEXT NOT NULL DEFAULT 'none',
    redacted_at TIMESTAMPTZ,
    redaction_event_id UUID REFERENCES events(id),
    trust_level TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CHECK (role_id IS NULL OR role_id IN ('designer', 'developer', 'cto', 'cfo', 'coo', 'ceo')),
    CHECK (event_type IN (
        'user_message',
        'assistant_message',
        'tool_call',
        'memory_proposed',
        'memory_written',
        'memory_deleted',
        'memory_redacted',
        'memory_reviewed'
    )),
    CHECK (trust_level IN (
        'system_trusted',
        'human_approved',
        'user_scoped',
        'agent_private',
        'tool_output',
        'retrieved_untrusted',
        'web_content'
    )),
    CHECK (retention_class IN ('ephemeral', 'standard', 'audit', 'legal_hold', 'erasure_requested')),
    CHECK (sensitivity IN ('none', 'personal', 'secret', 'regulated')),
    CHECK (redaction_status IN ('none', 'pending', 'redacted', 'erased'))
);

CREATE TABLE memory_facts (
    id UUID PRIMARY KEY,
    scope_type TEXT NOT NULL,
    scope_id TEXT NOT NULL,
    namespace TEXT NOT NULL,
    user_principal_id UUID REFERENCES principals(id),
    project_id UUID REFERENCES projects(id),
    org_id UUID REFERENCES organizations(id),
    role_id TEXT,
    agent_principal_id UUID REFERENCES principals(id),
    memory_type TEXT NOT NULL,
    visibility TEXT NOT NULL,
    subject TEXT NOT NULL,
    predicate TEXT NOT NULL,
    object TEXT NOT NULL,
    confidence NUMERIC(4,3) NOT NULL,
    status TEXT NOT NULL,
    source_event_id UUID NOT NULL REFERENCES events(id),
    proposed_by_principal_id UUID REFERENCES principals(id),
    superseded_by UUID REFERENCES memory_facts(id),
    valid_from TIMESTAMPTZ,
    valid_until TIMESTAMPTZ,
    expires_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CHECK (scope_type IN ('global', 'org', 'user', 'project', 'role', 'agent', 'session')),
    CHECK (role_id IS NULL OR role_id IN ('designer', 'developer', 'cto', 'cfo', 'coo', 'ceo')),
    CHECK (visibility IN ('private', 'role_shared', 'project_shared', 'org_shared', 'system')),
    CHECK (status IN ('active', 'tentative', 'superseded', 'contradicted', 'expired', 'deleted', 'redacted')),
    CHECK (confidence >= 0 AND confidence <= 1),
    CHECK (valid_until IS NULL OR valid_from IS NULL OR valid_until >= valid_from),
    CHECK (namespace LIKE '/%'),
    CHECK (
        (
            scope_type = 'global'
            AND scope_id = 'global'
            AND org_id IS NULL
            AND project_id IS NULL
            AND user_principal_id IS NULL
            AND agent_principal_id IS NULL
            AND role_id IS NULL
            AND namespace LIKE '/global/%'
        )
        OR (
            scope_type = 'org'
            AND org_id IS NOT NULL
            AND scope_id = org_id::text
            AND project_id IS NULL
            AND user_principal_id IS NULL
            AND agent_principal_id IS NULL
            AND role_id IS NULL
            AND namespace LIKE '/org/' || org_id::text || '/%'
        )
        OR (
            scope_type = 'user'
            AND user_principal_id IS NOT NULL
            AND scope_id = user_principal_id::text
            AND org_id IS NULL
            AND project_id IS NULL
            AND agent_principal_id IS NULL
            AND role_id IS NULL
            AND namespace LIKE '/user/' || user_principal_id::text || '/%'
        )
        OR (
            scope_type = 'project'
            AND project_id IS NOT NULL
            AND org_id IS NOT NULL
            AND scope_id = project_id::text
            AND user_principal_id IS NULL
            AND agent_principal_id IS NULL
            AND role_id IS NULL
            AND namespace LIKE '/project/' || project_id::text || '/%'
        )
        OR (
            scope_type = 'role'
            AND role_id IS NOT NULL
            AND scope_id = role_id
            AND org_id IS NULL
            AND project_id IS NULL
            AND user_principal_id IS NULL
            AND agent_principal_id IS NULL
            AND namespace LIKE '/role/' || role_id || '/%'
        )
        OR (
            scope_type = 'agent'
            AND agent_principal_id IS NOT NULL
            AND scope_id = agent_principal_id::text
            AND org_id IS NULL
            AND project_id IS NULL
            AND user_principal_id IS NULL
            AND role_id IS NULL
            AND namespace LIKE '/agent/' || agent_principal_id::text || '/%'
        )
        OR (
            scope_type = 'session'
            AND scope_id <> 'global'
            AND org_id IS NULL
            AND project_id IS NULL
            AND user_principal_id IS NULL
            AND agent_principal_id IS NULL
            AND role_id IS NULL
            AND namespace LIKE '/session/' || scope_id || '/%'
        )
    )
);

CREATE TABLE role_memory_lenses (
    id UUID PRIMARY KEY,
    role_id TEXT NOT NULL,
    project_id UUID REFERENCES projects(id),
    base_memory_fact_id UUID NOT NULL REFERENCES memory_facts(id),
    interpretation TEXT NOT NULL,
    confidence NUMERIC(4,3) NOT NULL,
    status TEXT NOT NULL,
    source_event_id UUID NOT NULL REFERENCES events(id),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CHECK (role_id IN ('designer', 'developer', 'cto', 'cfo', 'coo', 'ceo')),
    CHECK (status IN ('active', 'tentative', 'superseded', 'contradicted', 'expired', 'deleted', 'redacted')),
    CHECK (confidence >= 0 AND confidence <= 1)
);

CREATE TABLE memory_chunks (
    id UUID PRIMARY KEY,
    source_type TEXT NOT NULL,
    source_id UUID NOT NULL,
    namespace TEXT NOT NULL,
    scope_type TEXT NOT NULL,
    scope_id TEXT NOT NULL,
    title TEXT,
    content TEXT NOT NULL,
    content_hash TEXT NOT NULL,
    trust_level TEXT NOT NULL,
    source_event_id UUID NOT NULL REFERENCES events(id),
    search_vector TSVECTOR,
    redacted_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CHECK (source_type IN ('memory_fact', 'role_memory_lens', 'document', 'summary', 'vault_export')),
    CHECK (scope_type IN ('global', 'org', 'user', 'project', 'role', 'agent', 'session')),
    CHECK (namespace LIKE '/%'),
    CHECK (trust_level IN (
        'system_trusted',
        'human_approved',
        'user_scoped',
        'agent_private',
        'tool_output',
        'retrieved_untrusted',
        'web_content'
    ))
);

CREATE TABLE memory_embeddings (
    chunk_id UUID NOT NULL REFERENCES memory_chunks(id) ON DELETE CASCADE,
    embedding_model TEXT NOT NULL,
    embedding_dimension INT NOT NULL,
    embedding vector NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (chunk_id, embedding_model),
    CHECK (embedding_dimension > 0)
);

CREATE TABLE memory_reviews (
    id UUID PRIMARY KEY,
    memory_fact_id UUID NOT NULL REFERENCES memory_facts(id),
    review_status TEXT NOT NULL,
    reviewer_id UUID REFERENCES principals(id),
    notes TEXT,
    source_event_id UUID NOT NULL REFERENCES events(id),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CHECK (review_status IN ('pending', 'approved', 'rejected', 'needs_changes'))
);

CREATE TABLE memory_redactions (
    id UUID PRIMARY KEY,
    target_type TEXT NOT NULL,
    target_id UUID NOT NULL,
    redaction_type TEXT NOT NULL,
    reason TEXT NOT NULL,
    requested_by_principal_id UUID REFERENCES principals(id),
    source_event_id UUID NOT NULL REFERENCES events(id),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CHECK (target_type IN ('event', 'memory_fact', 'role_memory_lens', 'memory_chunk', 'vault_export')),
    CHECK (redaction_type IN ('expire', 'delete', 'redact'))
);

CREATE TABLE api_idempotency_keys (
    id UUID PRIMARY KEY,
    principal_id UUID NOT NULL REFERENCES principals(id),
    endpoint TEXT NOT NULL,
    idempotency_key TEXT NOT NULL,
    request_hash TEXT NOT NULL,
    response_status INT,
    response_body JSONB,
    resource_type TEXT,
    resource_id UUID,
    status TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    expires_at TIMESTAMPTZ NOT NULL,
    UNIQUE (principal_id, endpoint, idempotency_key),
    CHECK (status IN ('processing', 'completed', 'failed'))
);

CREATE TABLE outbox_jobs (
    id UUID PRIMARY KEY,
    job_type TEXT NOT NULL,
    aggregate_type TEXT NOT NULL,
    aggregate_id UUID NOT NULL,
    idempotency_key TEXT NOT NULL UNIQUE,
    payload JSONB NOT NULL,
    status TEXT NOT NULL,
    attempts INT NOT NULL DEFAULT 0,
    available_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    locked_until TIMESTAMPTZ,
    locked_by TEXT,
    last_error TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CHECK (status IN ('pending', 'processing', 'completed', 'failed', 'dead_letter')),
    CHECK (attempts >= 0)
);

CREATE OR REPLACE FUNCTION set_updated_at()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    NEW.updated_at = now();
    RETURN NEW;
END;
$$;

CREATE OR REPLACE FUNCTION set_memory_chunk_search_vector()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    NEW.search_vector = to_tsvector('english', concat_ws(' ', NEW.title, NEW.content));
    RETURN NEW;
END;
$$;

CREATE OR REPLACE FUNCTION validate_role_memory_lens_base_scope()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
DECLARE
    base_scope_type TEXT;
    base_project_id UUID;
    base_org_id UUID;
    target_org_id UUID;
BEGIN
    SELECT mf.scope_type, mf.project_id, mf.org_id
    INTO base_scope_type, base_project_id, base_org_id
    FROM memory_facts mf
    WHERE mf.id = NEW.base_memory_fact_id;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'role_memory_lenses.base_memory_fact_id % does not reference a memory fact', NEW.base_memory_fact_id;
    END IF;

    IF NEW.project_id IS NULL THEN
        IF base_scope_type NOT IN ('global', 'org') THEN
            RAISE EXCEPTION 'shared role lenses must reference global or organization facts, not % facts', base_scope_type;
        END IF;

        RETURN NEW;
    END IF;

    SELECT p.org_id
    INTO target_org_id
    FROM projects p
    WHERE p.id = NEW.project_id;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'role_memory_lenses.project_id % does not reference a project', NEW.project_id;
    END IF;

    IF NOT (
        (base_scope_type = 'project' AND base_project_id = NEW.project_id)
        OR (base_scope_type = 'org' AND base_org_id = target_org_id)
    ) THEN
        RAISE EXCEPTION 'project role lenses must reference target project facts or that project organization facts';
    END IF;

    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_principals_set_updated_at
    BEFORE UPDATE ON principals
    FOR EACH ROW
    EXECUTE FUNCTION set_updated_at();

CREATE TRIGGER trg_organizations_set_updated_at
    BEFORE UPDATE ON organizations
    FOR EACH ROW
    EXECUTE FUNCTION set_updated_at();

CREATE TRIGGER trg_projects_set_updated_at
    BEFORE UPDATE ON projects
    FOR EACH ROW
    EXECUTE FUNCTION set_updated_at();

CREATE TRIGGER trg_memory_facts_set_updated_at
    BEFORE UPDATE ON memory_facts
    FOR EACH ROW
    EXECUTE FUNCTION set_updated_at();

CREATE TRIGGER trg_role_memory_lenses_set_updated_at
    BEFORE UPDATE ON role_memory_lenses
    FOR EACH ROW
    EXECUTE FUNCTION set_updated_at();

CREATE TRIGGER trg_memory_chunks_set_updated_at
    BEFORE UPDATE ON memory_chunks
    FOR EACH ROW
    EXECUTE FUNCTION set_updated_at();

CREATE TRIGGER trg_memory_reviews_set_updated_at
    BEFORE UPDATE ON memory_reviews
    FOR EACH ROW
    EXECUTE FUNCTION set_updated_at();

CREATE TRIGGER trg_api_idempotency_keys_set_updated_at
    BEFORE UPDATE ON api_idempotency_keys
    FOR EACH ROW
    EXECUTE FUNCTION set_updated_at();

CREATE TRIGGER trg_outbox_jobs_set_updated_at
    BEFORE UPDATE ON outbox_jobs
    FOR EACH ROW
    EXECUTE FUNCTION set_updated_at();

CREATE TRIGGER trg_memory_chunks_set_search_vector
    BEFORE INSERT OR UPDATE OF title, content ON memory_chunks
    FOR EACH ROW
    EXECUTE FUNCTION set_memory_chunk_search_vector();

CREATE TRIGGER trg_role_memory_lenses_validate_base_scope
    BEFORE INSERT OR UPDATE OF project_id, base_memory_fact_id ON role_memory_lenses
    FOR EACH ROW
    EXECUTE FUNCTION validate_role_memory_lens_base_scope();

CREATE INDEX ix_projects_org_id
    ON projects (org_id);

CREATE INDEX ix_organization_memberships_principal_id
    ON organization_memberships (principal_id);

CREATE INDEX ix_project_memberships_principal_id
    ON project_memberships (principal_id);

CREATE INDEX ix_role_assignments_principal_scope
    ON role_assignments (principal_id, scope_type, scope_id);

CREATE INDEX ix_role_assignments_role_scope
    ON role_assignments (role_id, scope_type, scope_id);

CREATE INDEX ix_memory_access_grants_principal_permission
    ON memory_access_grants (principal_id, permission)
    WHERE principal_id IS NOT NULL;

CREATE INDEX ix_memory_access_grants_role_permission
    ON memory_access_grants (role_id, permission)
    WHERE role_id IS NOT NULL;

CREATE INDEX ix_memory_access_grants_namespace_prefix
    ON memory_access_grants (namespace_prefix);

CREATE INDEX ix_events_principal_created_at
    ON events (principal_id, created_at DESC);

CREATE INDEX ix_events_type_created_at
    ON events (event_type, created_at DESC);

CREATE INDEX ix_events_role_created_at
    ON events (role_id, created_at DESC)
    WHERE role_id IS NOT NULL;

CREATE INDEX ix_memory_facts_scope_status
    ON memory_facts (scope_type, scope_id, status);

CREATE INDEX ix_memory_facts_namespace
    ON memory_facts (namespace);

CREATE INDEX ix_memory_facts_visibility_status
    ON memory_facts (visibility, status);

CREATE INDEX ix_memory_facts_source_event_id
    ON memory_facts (source_event_id);

CREATE INDEX ix_memory_facts_project_status
    ON memory_facts (project_id, status)
    WHERE project_id IS NOT NULL;

CREATE INDEX ix_memory_facts_user_status
    ON memory_facts (user_principal_id, status)
    WHERE user_principal_id IS NOT NULL;

CREATE UNIQUE INDEX ux_memory_facts_active_dedupe
    ON memory_facts (scope_type, scope_id, memory_type, lower(subject), lower(predicate), md5(object))
    WHERE status = 'active';

CREATE INDEX ix_role_memory_lenses_role_project_status
    ON role_memory_lenses (role_id, project_id, status);

CREATE INDEX ix_role_memory_lenses_base_memory_fact_id
    ON role_memory_lenses (base_memory_fact_id);

CREATE INDEX ix_role_memory_lenses_source_event_id
    ON role_memory_lenses (source_event_id);

CREATE INDEX ix_memory_chunks_source
    ON memory_chunks (source_type, source_id);

CREATE INDEX ix_memory_chunks_scope
    ON memory_chunks (scope_type, scope_id);

CREATE INDEX ix_memory_chunks_namespace
    ON memory_chunks (namespace);

CREATE INDEX ix_memory_chunks_source_event_id
    ON memory_chunks (source_event_id);

CREATE INDEX ix_memory_chunks_search_vector
    ON memory_chunks USING GIN (search_vector);

CREATE INDEX ix_memory_embeddings_model_dimension
    ON memory_embeddings (embedding_model, embedding_dimension);

CREATE INDEX ix_memory_reviews_status_created_at
    ON memory_reviews (review_status, created_at);

CREATE INDEX ix_memory_reviews_memory_fact_id
    ON memory_reviews (memory_fact_id);

CREATE INDEX ix_memory_reviews_source_event_id
    ON memory_reviews (source_event_id);

CREATE INDEX ix_memory_redactions_target
    ON memory_redactions (target_type, target_id);

CREATE INDEX ix_memory_redactions_source_event_id
    ON memory_redactions (source_event_id);

CREATE INDEX ix_api_idempotency_keys_status_expires_at
    ON api_idempotency_keys (status, expires_at);

CREATE INDEX ix_outbox_jobs_status_available_at
    ON outbox_jobs (status, available_at);

CREATE INDEX ix_outbox_jobs_locked_until
    ON outbox_jobs (locked_until)
    WHERE locked_until IS NOT NULL;
