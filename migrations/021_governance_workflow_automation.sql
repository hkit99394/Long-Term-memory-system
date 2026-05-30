CREATE TABLE governance_legal_holds (
    id UUID PRIMARY KEY,
    status TEXT NOT NULL,
    reason TEXT NOT NULL,
    release_reason TEXT,
    created_by_principal_id UUID NOT NULL REFERENCES principals(id),
    released_by_principal_id UUID REFERENCES principals(id),
    scope_type TEXT,
    scope_id TEXT,
    namespace_prefix TEXT,
    retention_class TEXT,
    sensitivity TEXT,
    created_from TIMESTAMPTZ,
    created_to TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    released_at TIMESTAMPTZ,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CHECK (status IN ('active', 'released')),
    CHECK (reason = btrim(reason) AND reason <> ''),
    CHECK (release_reason IS NULL OR (release_reason = btrim(release_reason) AND release_reason <> '')),
    CHECK (
        (scope_type IS NULL AND scope_id IS NULL)
        OR (scope_type IS NOT NULL AND scope_id IS NOT NULL)
    ),
    CHECK (scope_type IS NULL OR scope_type IN ('global', 'org', 'user', 'project', 'role', 'agent', 'session')),
    CHECK (namespace_prefix IS NULL OR namespace_prefix LIKE '/%'),
    CHECK (retention_class IS NULL OR retention_class IN ('ephemeral', 'standard', 'audit', 'legal_hold', 'erasure_requested')),
    CHECK (sensitivity IS NULL OR sensitivity IN ('none', 'personal', 'secret', 'regulated')),
    CHECK (created_to IS NULL OR created_from IS NULL OR created_to >= created_from),
    CHECK (
        (status = 'active' AND released_at IS NULL AND released_by_principal_id IS NULL)
        OR (status = 'released' AND released_at IS NOT NULL AND released_by_principal_id IS NOT NULL)
    )
);

CREATE TABLE governance_legal_hold_events (
    legal_hold_id UUID NOT NULL REFERENCES governance_legal_holds(id) ON DELETE CASCADE,
    event_id UUID NOT NULL REFERENCES events(id),
    original_retention_class TEXT NOT NULL,
    held_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    released_at TIMESTAMPTZ,
    PRIMARY KEY (legal_hold_id, event_id),
    CHECK (original_retention_class IN ('ephemeral', 'standard', 'audit', 'legal_hold', 'erasure_requested')),
    CHECK (released_at IS NULL OR released_at >= held_at)
);

CREATE TRIGGER trg_governance_legal_holds_set_updated_at
    BEFORE UPDATE ON governance_legal_holds
    FOR EACH ROW
    EXECUTE FUNCTION set_updated_at();

CREATE INDEX ix_governance_legal_holds_status_created_at
    ON governance_legal_holds (status, created_at DESC);

CREATE INDEX ix_governance_legal_holds_scope
    ON governance_legal_holds (scope_type, scope_id)
    WHERE scope_type IS NOT NULL;

CREATE INDEX ix_governance_legal_holds_namespace_prefix
    ON governance_legal_holds (namespace_prefix)
    WHERE namespace_prefix IS NOT NULL;

CREATE INDEX ix_governance_legal_hold_events_event_id
    ON governance_legal_hold_events (event_id);

CREATE OR REPLACE FUNCTION validate_event_durable_memory_references()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
DECLARE
    source_principal_id UUID;
    invalid_memory_fact_id UUID;
    invalid_lens_id UUID;
    invalid_chunk_id UUID;
    event_payload_hidden BOOLEAN;
BEGIN
    source_principal_id := COALESCE(
        NEW.principal_id,
        NEW.scope_principal_id,
        NEW.agent_principal_id,
        '00000000-0000-4000-8000-000000000007'::uuid);
    event_payload_hidden := NEW.retention_class = 'erasure_requested'
        OR NEW.redaction_status <> 'none';

    SELECT fact.id
    INTO invalid_memory_fact_id
    FROM memory_facts AS fact
    WHERE fact.source_event_id = NEW.id
        AND (
            NEW.scope_type <> fact.scope_type
            OR NEW.scope_id <> fact.scope_id
            OR source_principal_id IS DISTINCT FROM fact.proposed_by_principal_id
            OR NEW.trust_level <> fact.trust_level
            OR (
                event_payload_hidden
                AND fact.status NOT IN ('expired', 'deleted', 'redacted')
            )
        )
    LIMIT 1;

    IF invalid_memory_fact_id IS NOT NULL THEN
        RAISE EXCEPTION 'events.id % cannot be changed because memory_facts row % depends on its provenance', NEW.id, invalid_memory_fact_id;
    END IF;

    SELECT lens.id
    INTO invalid_lens_id
    FROM role_memory_lenses AS lens
    WHERE lens.source_event_id = NEW.id
        AND (
            NEW.scope_type <> lens.scope_type
            OR NEW.scope_id <> lens.scope_id
            OR source_principal_id IS DISTINCT FROM lens.proposed_by_principal_id
            OR (
                event_payload_hidden
                AND lens.status NOT IN ('expired', 'deleted', 'redacted')
            )
        )
    LIMIT 1;

    IF invalid_lens_id IS NOT NULL THEN
        RAISE EXCEPTION 'events.id % cannot be changed because role_memory_lenses row % depends on its provenance', NEW.id, invalid_lens_id;
    END IF;

    SELECT chunk.id
    INTO invalid_chunk_id
    FROM memory_chunks AS chunk
    WHERE chunk.source_event_id = NEW.id
        AND (
            NEW.trust_level <> chunk.trust_level
            OR (
                event_payload_hidden
                AND chunk.redacted_at IS NULL
            )
        )
    LIMIT 1;

    IF invalid_chunk_id IS NOT NULL THEN
        RAISE EXCEPTION 'events.id % cannot be changed because memory_chunks row % depends on its provenance', NEW.id, invalid_chunk_id;
    END IF;

    RETURN NEW;
END;
$$;
