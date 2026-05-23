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
    WHERE mf.id = NEW.base_memory_fact_id;

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
        AND fact.status <> 'active'
    LIMIT 1;

    IF invalid_lens_id IS NOT NULL THEN
        RAISE EXCEPTION 'existing role_memory_lenses row % references an inactive base memory fact', invalid_lens_id;
    END IF;
END;
$$;

CREATE OR REPLACE FUNCTION validate_memory_fact_active_lens_reference()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    IF OLD.status = 'active'
        AND NEW.status <> 'active'
        AND EXISTS (
            SELECT 1
            FROM role_memory_lenses AS lens
            WHERE lens.base_memory_fact_id = NEW.id
                AND lens.status = 'active'
        ) THEN
        RAISE EXCEPTION 'memory_facts.id % is referenced by an active role memory lens', NEW.id;
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
    BEFORE UPDATE OF status ON memory_facts
    FOR EACH ROW
    EXECUTE FUNCTION validate_memory_fact_active_lens_reference();
