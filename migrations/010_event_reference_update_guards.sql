CREATE OR REPLACE FUNCTION validate_role_memory_lens_base_scope()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
DECLARE
    base_scope_type TEXT;
    base_project_id UUID;
    base_org_id UUID;
    base_status TEXT;
    target_org_id UUID;
BEGIN
    SELECT mf.scope_type, mf.project_id, mf.org_id, mf.status
    INTO base_scope_type, base_project_id, base_org_id, base_status
    FROM memory_facts mf
    WHERE mf.id = NEW.base_memory_fact_id
    FOR UPDATE;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'role_memory_lenses.base_memory_fact_id % does not reference a memory fact', NEW.base_memory_fact_id;
    END IF;

    IF NEW.status = 'active' AND base_status <> 'active' THEN
        RAISE EXCEPTION 'active role memory lenses must reference active memory facts';
    END IF;

    IF NEW.scope_type = 'global' THEN
        IF base_scope_type <> 'global' THEN
            RAISE EXCEPTION 'global role lenses must reference global facts, not % facts', base_scope_type;
        END IF;

        RETURN NEW;
    END IF;

    IF NEW.scope_type = 'org' THEN
        IF base_scope_type <> 'org'
            OR base_org_id IS DISTINCT FROM NEW.org_id THEN
            RAISE EXCEPTION 'organization role lenses must reference facts from the same organization';
        END IF;

        RETURN NEW;
    END IF;

    IF NEW.scope_type <> 'project' THEN
        RAISE EXCEPTION 'unsupported role_memory_lenses.scope_type %', NEW.scope_type;
    END IF;

    SELECT p.org_id
    INTO target_org_id
    FROM projects p
    WHERE p.id = NEW.project_id;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'role_memory_lenses.project_id % does not reference a project', NEW.project_id;
    END IF;

    IF NEW.org_id IS DISTINCT FROM target_org_id THEN
        RAISE EXCEPTION 'role_memory_lenses.org_id must match the target project organization';
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

DO $$
DECLARE
    invalid_lens_id UUID;
BEGIN
    SELECT lens.id
    INTO invalid_lens_id
    FROM role_memory_lenses AS lens
    INNER JOIN memory_facts AS fact ON fact.id = lens.base_memory_fact_id
    WHERE lens.status = 'active'
        AND (
            fact.status <> 'active'
            OR NOT (
                (lens.scope_type = 'global' AND fact.scope_type = 'global')
                OR (
                    lens.scope_type = 'org'
                    AND fact.scope_type = 'org'
                    AND fact.org_id IS NOT DISTINCT FROM lens.org_id
                )
                OR (
                    lens.scope_type = 'project'
                    AND (
                        (
                            fact.scope_type = 'project'
                            AND fact.project_id IS NOT DISTINCT FROM lens.project_id
                        )
                        OR (
                            fact.scope_type = 'org'
                            AND fact.org_id IS NOT DISTINCT FROM lens.org_id
                        )
                    )
                )
            )
        )
    LIMIT 1;

    IF invalid_lens_id IS NOT NULL THEN
        RAISE EXCEPTION 'existing role_memory_lenses row % references a base memory fact that cannot support an active role lens', invalid_lens_id;
    END IF;
END;
$$;

CREATE OR REPLACE FUNCTION validate_memory_fact_active_lens_reference()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
DECLARE
    invalid_lens_id UUID;
BEGIN
    SELECT lens.id
    INTO invalid_lens_id
    FROM role_memory_lenses AS lens
    WHERE lens.base_memory_fact_id = NEW.id
        AND lens.status = 'active'
        AND (
            NEW.status <> 'active'
            OR NOT (
                (lens.scope_type = 'global' AND NEW.scope_type = 'global')
                OR (
                    lens.scope_type = 'org'
                    AND NEW.scope_type = 'org'
                    AND NEW.org_id IS NOT DISTINCT FROM lens.org_id
                )
                OR (
                    lens.scope_type = 'project'
                    AND (
                        (
                            NEW.scope_type = 'project'
                            AND NEW.project_id IS NOT DISTINCT FROM lens.project_id
                        )
                        OR (
                            NEW.scope_type = 'org'
                            AND NEW.org_id IS NOT DISTINCT FROM lens.org_id
                        )
                    )
                )
            )
        )
    LIMIT 1;

    IF invalid_lens_id IS NOT NULL THEN
        RAISE EXCEPTION 'memory_facts.id % cannot be changed because active role memory lens % depends on it', NEW.id, invalid_lens_id;
    END IF;

    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_role_memory_lenses_validate_base_scope ON role_memory_lenses;

CREATE TRIGGER trg_role_memory_lenses_validate_base_scope
    BEFORE INSERT OR UPDATE OF scope_type, scope_id, org_id, project_id, base_memory_fact_id, status ON role_memory_lenses
    FOR EACH ROW
    EXECUTE FUNCTION validate_role_memory_lens_base_scope();

DROP TRIGGER IF EXISTS trg_memory_facts_validate_active_lens_reference ON memory_facts;

CREATE TRIGGER trg_memory_facts_validate_active_lens_reference
    BEFORE UPDATE OF status, scope_type, scope_id, org_id, project_id ON memory_facts
    FOR EACH ROW
    EXECUTE FUNCTION validate_memory_fact_active_lens_reference();

CREATE OR REPLACE FUNCTION validate_event_durable_memory_references()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
DECLARE
    source_principal_id UUID;
    invalid_memory_fact_id UUID;
    invalid_lens_id UUID;
    invalid_chunk_id UUID;
BEGIN
    source_principal_id := COALESCE(
        NEW.principal_id,
        NEW.scope_principal_id,
        NEW.agent_principal_id,
        '00000000-0000-4000-8000-000000000007'::uuid);

    SELECT fact.id
    INTO invalid_memory_fact_id
    FROM memory_facts AS fact
    WHERE fact.source_event_id = NEW.id
        AND (
            NEW.scope_type <> fact.scope_type
            OR NEW.scope_id <> fact.scope_id
            OR source_principal_id IS DISTINCT FROM fact.proposed_by_principal_id
            OR NEW.trust_level <> fact.trust_level
            OR NEW.retention_class = 'erasure_requested'
            OR NEW.redaction_status <> 'none'
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
            OR NEW.retention_class = 'erasure_requested'
            OR NEW.redaction_status <> 'none'
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
            OR NEW.retention_class = 'erasure_requested'
            OR NEW.redaction_status <> 'none'
        )
    LIMIT 1;

    IF invalid_chunk_id IS NOT NULL THEN
        RAISE EXCEPTION 'events.id % cannot be changed because memory_chunks row % depends on its provenance', NEW.id, invalid_chunk_id;
    END IF;

    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_events_validate_durable_memory_references ON events;

CREATE TRIGGER trg_events_validate_durable_memory_references
    BEFORE UPDATE OF scope_type, scope_id, principal_id, scope_principal_id, agent_principal_id, trust_level, retention_class, redaction_status ON events
    FOR EACH ROW
    EXECUTE FUNCTION validate_event_durable_memory_references();
