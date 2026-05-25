DO $$
DECLARE
    duplicate_scope_type TEXT;
    duplicate_scope_id TEXT;
    duplicate_memory_type TEXT;
    duplicate_subject TEXT;
    duplicate_predicate TEXT;
BEGIN
    SELECT
        scope_type,
        scope_id,
        memory_type,
        lower(btrim(subject)),
        lower(btrim(predicate))
    INTO
        duplicate_scope_type,
        duplicate_scope_id,
        duplicate_memory_type,
        duplicate_subject,
        duplicate_predicate
    FROM memory_facts
    WHERE status = 'active'
    GROUP BY
        scope_type,
        scope_id,
        memory_type,
        lower(btrim(subject)),
        lower(btrim(predicate))
    HAVING count(*) > 1
    LIMIT 1;

    IF duplicate_scope_type IS NOT NULL THEN
        RAISE EXCEPTION 'existing active memory facts contain duplicate subject/predicate in scope %, id %, type %, subject %, predicate %',
            duplicate_scope_type,
            duplicate_scope_id,
            duplicate_memory_type,
            duplicate_subject,
            duplicate_predicate;
    END IF;
END;
$$;

DROP INDEX IF EXISTS ix_memory_facts_active_subject_predicate;
DROP INDEX IF EXISTS ux_memory_facts_active_subject_predicate;
DROP INDEX IF EXISTS ux_memory_facts_active_dedupe;

CREATE UNIQUE INDEX ux_memory_facts_active_subject_predicate
    ON memory_facts (scope_type, scope_id, memory_type, lower(btrim(subject)), lower(btrim(predicate)))
    WHERE status = 'active';

INSERT INTO principals (id, principal_type, display_name, status)
VALUES (
    '00000000-0000-4000-8000-000000000007'::uuid,
    'service',
    'Legacy System Provenance',
    'active'
)
ON CONFLICT (id) DO NOTHING;

DO $$
DECLARE
    ambiguous_source_event_id UUID;
BEGIN
    WITH source_targets AS (
        SELECT
            fact.source_event_id,
            fact.scope_type,
            fact.scope_id,
            COALESCE(
                fact.proposed_by_principal_id,
                event.principal_id,
                event.scope_principal_id,
                event.agent_principal_id,
                '00000000-0000-4000-8000-000000000007'::uuid) AS proposed_by_principal_id,
            fact.trust_level
        FROM memory_facts AS fact
        INNER JOIN events AS event ON event.id = fact.source_event_id

        UNION ALL

        SELECT
            lens.source_event_id,
            lens.scope_type,
            lens.scope_id,
            COALESCE(
                event.principal_id,
                event.scope_principal_id,
                event.agent_principal_id,
                '00000000-0000-4000-8000-000000000007'::uuid) AS proposed_by_principal_id,
            event.trust_level
        FROM role_memory_lenses AS lens
        INNER JOIN events AS event ON event.id = lens.source_event_id
    )
    SELECT source_event_id
    INTO ambiguous_source_event_id
    FROM source_targets
    GROUP BY source_event_id
    HAVING count(DISTINCT (scope_type, scope_id, proposed_by_principal_id, trust_level)) > 1
    LIMIT 1;

    IF ambiguous_source_event_id IS NOT NULL THEN
        RAISE EXCEPTION 'source event % is reused by durable memory rows with different scope, proposer, or trust; split provenance before applying migration 007',
            ambiguous_source_event_id;
    END IF;
END;
$$;

UPDATE events AS event
SET
    scope_type = fact.scope_type,
    scope_id = fact.scope_id,
    scope_org_id = fact.org_id,
    scope_project_id = fact.project_id,
    scope_principal_id = CASE
        WHEN fact.scope_type IN ('user', 'agent') THEN COALESCE(fact.user_principal_id, fact.agent_principal_id)
        ELSE NULL
    END,
    scope_role_id = CASE
        WHEN fact.scope_type = 'role' THEN fact.role_id
        ELSE NULL
    END
FROM memory_facts AS fact
WHERE fact.source_event_id = event.id
    AND event.scope_type = 'user'
    AND fact.scope_type <> 'user'
    AND event.principal_id IS NOT NULL
    AND (
        fact.proposed_by_principal_id IS NULL
        OR fact.proposed_by_principal_id = event.principal_id
    );

UPDATE events AS event
SET
    scope_type = lens.scope_type,
    scope_id = lens.scope_id,
    scope_org_id = lens.org_id,
    scope_project_id = lens.project_id,
    scope_principal_id = NULL,
    scope_role_id = NULL
FROM role_memory_lenses AS lens
WHERE lens.source_event_id = event.id
    AND event.scope_type = 'user'
    AND lens.scope_type <> 'user'
    AND event.principal_id IS NOT NULL;

UPDATE memory_facts AS fact
SET proposed_by_principal_id = COALESCE(
    event.principal_id,
    event.scope_principal_id,
    event.agent_principal_id,
    '00000000-0000-4000-8000-000000000007'::uuid)
FROM events AS event
WHERE event.id = fact.source_event_id
    AND fact.proposed_by_principal_id IS NULL;

DO $$
DECLARE
    invalid_memory_fact_id UUID;
BEGIN
    SELECT fact.id
    INTO invalid_memory_fact_id
    FROM memory_facts AS fact
    WHERE fact.proposed_by_principal_id IS NULL
    LIMIT 1;

    IF invalid_memory_fact_id IS NOT NULL THEN
        RAISE EXCEPTION 'existing memory_facts row % does not have a durable proposer', invalid_memory_fact_id;
    END IF;
END;
$$;

ALTER TABLE memory_facts
    ALTER COLUMN proposed_by_principal_id SET NOT NULL;

CREATE OR REPLACE FUNCTION validate_memory_fact_source_event_evidence()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
DECLARE
    source_scope_type TEXT;
    source_scope_id TEXT;
    source_principal_id UUID;
    source_trust_level TEXT;
    source_retention_class TEXT;
    source_redaction_status TEXT;
BEGIN
    IF NEW.source_event_id IS NULL THEN
        RETURN NEW;
    END IF;

    IF NEW.proposed_by_principal_id IS NULL THEN
        RETURN NEW;
    END IF;

    SELECT
        event.scope_type,
        event.scope_id,
        COALESCE(
            event.principal_id,
            event.scope_principal_id,
            event.agent_principal_id,
            '00000000-0000-4000-8000-000000000007'::uuid),
        event.trust_level,
        event.retention_class,
        event.redaction_status
    INTO source_scope_type, source_scope_id, source_principal_id, source_trust_level, source_retention_class, source_redaction_status
    FROM events AS event
    WHERE event.id = NEW.source_event_id;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'memory_facts.source_event_id % does not reference an event', NEW.source_event_id;
    END IF;

    IF source_retention_class = 'erasure_requested'
        OR source_redaction_status <> 'none' THEN
        RAISE EXCEPTION 'memory_facts.source_event_id % references redacted or erasure-requested evidence', NEW.source_event_id;
    END IF;

    IF source_scope_type <> NEW.scope_type
        OR source_scope_id <> NEW.scope_id THEN
        RAISE EXCEPTION 'memory_facts.source_event_id % does not match memory fact scope', NEW.source_event_id;
    END IF;

    IF source_principal_id IS DISTINCT FROM NEW.proposed_by_principal_id THEN
        RAISE EXCEPTION 'memory_facts.source_event_id % does not match proposed_by_principal_id', NEW.source_event_id;
    END IF;

    IF source_trust_level <> NEW.trust_level THEN
        RAISE EXCEPTION 'memory_facts.source_event_id % does not match memory fact trust_level', NEW.source_event_id;
    END IF;

    RETURN NEW;
END;
$$;

DO $$
DECLARE
    invalid_memory_fact_id UUID;
BEGIN
    SELECT fact.id
    INTO invalid_memory_fact_id
    FROM memory_facts AS fact
    INNER JOIN events AS event ON event.id = fact.source_event_id
    WHERE event.scope_type <> fact.scope_type
        OR event.scope_id <> fact.scope_id
        OR COALESCE(
            event.principal_id,
            event.scope_principal_id,
            event.agent_principal_id,
            '00000000-0000-4000-8000-000000000007'::uuid)
            IS DISTINCT FROM fact.proposed_by_principal_id
        OR event.trust_level <> fact.trust_level
        OR event.retention_class = 'erasure_requested'
        OR event.redaction_status <> 'none'
    LIMIT 1;

    IF invalid_memory_fact_id IS NOT NULL THEN
        RAISE EXCEPTION 'existing memory_facts row % references source event evidence that disagrees with the fact', invalid_memory_fact_id;
    END IF;
END;
$$;

DROP TRIGGER IF EXISTS trg_memory_facts_validate_source_event_evidence ON memory_facts;

CREATE TRIGGER trg_memory_facts_validate_source_event_evidence
    BEFORE INSERT OR UPDATE OF scope_type, scope_id, source_event_id, proposed_by_principal_id, trust_level ON memory_facts
    FOR EACH ROW
    EXECUTE FUNCTION validate_memory_fact_source_event_evidence();

CREATE OR REPLACE FUNCTION validate_memory_chunk_source_scope()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
DECLARE
    expected_scope_type TEXT;
    expected_scope_id TEXT;
    expected_namespace TEXT;
    expected_source_event_id UUID;
    expected_trust_level TEXT;
    expected_namespace_prefix TEXT;
    lens_role_id TEXT;
    lens_scope_type TEXT;
    lens_scope_id TEXT;
    lens_org_id UUID;
    lens_project_id UUID;
BEGIN
    IF NEW.source_event_id IS NULL THEN
        RETURN NEW;
    END IF;

    IF NEW.source_type = 'memory_fact' THEN
        SELECT mf.scope_type, mf.scope_id, mf.namespace, mf.source_event_id, mf.trust_level
        INTO expected_scope_type, expected_scope_id, expected_namespace, expected_source_event_id, expected_trust_level
        FROM memory_facts mf
        WHERE mf.id = NEW.source_id;

        IF NOT FOUND THEN
            RAISE EXCEPTION 'memory_chunks.source_id % does not reference a memory fact', NEW.source_id;
        END IF;

        IF NEW.scope_type <> expected_scope_type
            OR NEW.scope_id <> expected_scope_id
            OR NEW.namespace <> expected_namespace THEN
            RAISE EXCEPTION 'memory chunk scope/namespace must match source memory fact %', NEW.source_id;
        END IF;

        IF NEW.source_event_id <> expected_source_event_id THEN
            RAISE EXCEPTION 'memory chunk source_event_id must match source memory fact %', NEW.source_id;
        END IF;

        IF NEW.trust_level <> expected_trust_level THEN
            RAISE EXCEPTION 'memory chunk trust_level must match source memory fact %', NEW.source_id;
        END IF;

        RETURN NEW;
    END IF;

    IF NEW.source_type = 'role_memory_lens' THEN
        SELECT rml.role_id, rml.scope_type, rml.scope_id, rml.org_id, rml.project_id, rml.source_event_id, event.trust_level
        INTO lens_role_id, lens_scope_type, lens_scope_id, lens_org_id, lens_project_id, expected_source_event_id, expected_trust_level
        FROM role_memory_lenses rml
        INNER JOIN events AS event ON event.id = rml.source_event_id
        WHERE rml.id = NEW.source_id;

        IF NOT FOUND THEN
            RAISE EXCEPTION 'memory_chunks.source_id % does not reference a role memory lens', NEW.source_id;
        END IF;

        IF lens_scope_type = 'global' THEN
            expected_scope_type := 'role';
            expected_scope_id := lens_role_id;
            expected_namespace_prefix := '/role/' || lens_role_id || '/shared';
        ELSIF lens_scope_type = 'org' THEN
            expected_scope_type := 'org';
            expected_scope_id := lens_scope_id;
            expected_namespace_prefix := '/org/' || lens_org_id::text || '/role/' || lens_role_id || '/lens';
        ELSIF lens_scope_type = 'project' THEN
            expected_scope_type := 'project';
            expected_scope_id := lens_scope_id;
            expected_namespace_prefix := '/project/' || lens_project_id::text || '/role/' || lens_role_id || '/lens';
        ELSE
            RAISE EXCEPTION 'unsupported role_memory_lenses.scope_type %', lens_scope_type;
        END IF;

        IF NEW.scope_type <> expected_scope_type
            OR NEW.scope_id <> expected_scope_id
            OR NOT (
                NEW.namespace = expected_namespace_prefix
                OR NEW.namespace LIKE expected_namespace_prefix || '/%'
            ) THEN
            RAISE EXCEPTION 'memory chunk scope/namespace must match source role memory lens %', NEW.source_id;
        END IF;

        IF NEW.source_event_id <> expected_source_event_id THEN
            RAISE EXCEPTION 'memory chunk source_event_id must match source role memory lens %', NEW.source_id;
        END IF;

        IF NEW.trust_level <> expected_trust_level THEN
            RAISE EXCEPTION 'memory chunk trust_level must match source role memory lens %', NEW.source_id;
        END IF;
    END IF;

    RETURN NEW;
END;
$$;

DO $$
DECLARE
    invalid_chunk_id UUID;
BEGIN
    SELECT chunk.id
    INTO invalid_chunk_id
    FROM memory_chunks AS chunk
    INNER JOIN memory_facts AS fact
        ON chunk.source_type = 'memory_fact'
        AND fact.id = chunk.source_id
    WHERE chunk.source_event_id <> fact.source_event_id
        OR chunk.trust_level <> fact.trust_level
    LIMIT 1;

    IF invalid_chunk_id IS NOT NULL THEN
        RAISE EXCEPTION 'existing memory_chunks row % has source evidence that disagrees with its memory fact', invalid_chunk_id;
    END IF;

    SELECT chunk.id
    INTO invalid_chunk_id
    FROM memory_chunks AS chunk
    INNER JOIN role_memory_lenses AS lens
        ON chunk.source_type = 'role_memory_lens'
        AND lens.id = chunk.source_id
    INNER JOIN events AS event
        ON event.id = lens.source_event_id
    WHERE chunk.source_event_id <> lens.source_event_id
        OR chunk.trust_level <> event.trust_level
    LIMIT 1;

    IF invalid_chunk_id IS NOT NULL THEN
        RAISE EXCEPTION 'existing memory_chunks row % has source evidence that disagrees with its role memory lens', invalid_chunk_id;
    END IF;
END;
$$;

DROP TRIGGER IF EXISTS trg_memory_chunks_validate_source_scope ON memory_chunks;

CREATE TRIGGER trg_memory_chunks_validate_source_scope
    BEFORE INSERT OR UPDATE OF source_type, source_id, scope_type, scope_id, namespace, source_event_id, trust_level ON memory_chunks
    FOR EACH ROW
    EXECUTE FUNCTION validate_memory_chunk_source_scope();

ALTER TABLE role_memory_lenses
    ADD COLUMN IF NOT EXISTS proposed_by_principal_id UUID;

UPDATE role_memory_lenses AS lens
SET proposed_by_principal_id = COALESCE(
    event.principal_id,
    event.scope_principal_id,
    event.agent_principal_id,
    '00000000-0000-4000-8000-000000000007'::uuid)
FROM events AS event
WHERE event.id = lens.source_event_id
    AND lens.proposed_by_principal_id IS NULL;

ALTER TABLE role_memory_lenses
    ALTER COLUMN proposed_by_principal_id SET NOT NULL,
    ADD CONSTRAINT fk_role_memory_lenses_proposed_by_principal FOREIGN KEY (proposed_by_principal_id) REFERENCES principals(id);

CREATE OR REPLACE FUNCTION validate_role_memory_lens_source_event_scope()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
DECLARE
    source_scope_type TEXT;
    source_scope_id TEXT;
    source_principal_id UUID;
    source_retention_class TEXT;
    source_redaction_status TEXT;
BEGIN
    SELECT
        event.scope_type,
        event.scope_id,
        COALESCE(
            event.principal_id,
            event.scope_principal_id,
            event.agent_principal_id,
            '00000000-0000-4000-8000-000000000007'::uuid),
        event.retention_class,
        event.redaction_status
    INTO source_scope_type, source_scope_id, source_principal_id, source_retention_class, source_redaction_status
    FROM events AS event
    WHERE event.id = NEW.source_event_id;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'role_memory_lenses.source_event_id % does not reference an event', NEW.source_event_id;
    END IF;

    IF source_retention_class = 'erasure_requested'
        OR source_redaction_status <> 'none' THEN
        RAISE EXCEPTION 'role_memory_lenses.source_event_id % references redacted or erasure-requested evidence', NEW.source_event_id;
    END IF;

    IF source_scope_type <> NEW.scope_type
        OR source_scope_id <> NEW.scope_id THEN
        RAISE EXCEPTION 'role_memory_lenses.source_event_id % does not match role memory lens scope', NEW.source_event_id;
    END IF;

    IF source_principal_id IS DISTINCT FROM NEW.proposed_by_principal_id THEN
        RAISE EXCEPTION 'role_memory_lenses.source_event_id % does not match proposed_by_principal_id', NEW.source_event_id;
    END IF;

    RETURN NEW;
END;
$$;

DO $$
DECLARE
    invalid_lens_id UUID;
BEGIN
    SELECT lens.id
    INTO invalid_lens_id
    FROM role_memory_lenses AS lens
    INNER JOIN events AS event ON event.id = lens.source_event_id
    WHERE event.scope_type <> lens.scope_type
        OR event.scope_id <> lens.scope_id
        OR COALESCE(
            event.principal_id,
            event.scope_principal_id,
            event.agent_principal_id,
            '00000000-0000-4000-8000-000000000007'::uuid)
            IS DISTINCT FROM lens.proposed_by_principal_id
        OR event.retention_class = 'erasure_requested'
        OR event.redaction_status <> 'none'
    LIMIT 1;

    IF invalid_lens_id IS NOT NULL THEN
        RAISE EXCEPTION 'existing role_memory_lenses row % references a source event outside its scope', invalid_lens_id;
    END IF;
END;
$$;

DROP TRIGGER IF EXISTS trg_role_memory_lenses_validate_source_event_scope ON role_memory_lenses;

CREATE TRIGGER trg_role_memory_lenses_validate_source_event_scope
    BEFORE INSERT OR UPDATE OF scope_type, scope_id, source_event_id, proposed_by_principal_id ON role_memory_lenses
    FOR EACH ROW
    EXECUTE FUNCTION validate_role_memory_lens_source_event_scope();
