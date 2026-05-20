ALTER TABLE projects
    ADD CONSTRAINT uq_projects_id_org_id UNIQUE (id, org_id);

ALTER TABLE memory_access_grants
    DROP CONSTRAINT IF EXISTS memory_access_grants_check;

ALTER TABLE memory_access_grants
    ADD CONSTRAINT ck_memory_access_grants_exactly_one_target CHECK (
        (principal_id IS NOT NULL AND role_id IS NULL)
        OR (principal_id IS NULL AND role_id IS NOT NULL)
    );

ALTER TABLE events
    ADD COLUMN scope_type TEXT,
    ADD COLUMN scope_id TEXT,
    ADD COLUMN scope_org_id UUID REFERENCES organizations(id),
    ADD COLUMN scope_project_id UUID,
    ADD COLUMN scope_principal_id UUID REFERENCES principals(id),
    ADD COLUMN scope_role_id TEXT;

DO $$
DECLARE
    invalid_event_id UUID;
BEGIN
    SELECT legacy_event.id
    INTO invalid_event_id
    FROM events AS legacy_event
    WHERE (
        (legacy_event.principal_id IS NOT NULL)::int
        + (legacy_event.agent_principal_id IS NOT NULL)::int
        + (legacy_event.role_id IS NOT NULL)::int
        + (legacy_event.conversation_id IS NOT NULL)::int
    ) > 1
    LIMIT 1;

    IF invalid_event_id IS NOT NULL THEN
        RAISE EXCEPTION 'existing events row % has ambiguous legacy scope signals', invalid_event_id;
    END IF;
END;
$$;

UPDATE events
SET
    scope_type = CASE
        WHEN agent_principal_id IS NOT NULL THEN 'agent'
        WHEN principal_id IS NOT NULL THEN 'user'
        WHEN role_id IS NOT NULL THEN 'role'
        WHEN conversation_id IS NOT NULL THEN 'session'
        ELSE 'global'
    END,
    scope_id = CASE
        WHEN agent_principal_id IS NOT NULL THEN agent_principal_id::text
        WHEN principal_id IS NOT NULL THEN principal_id::text
        WHEN role_id IS NOT NULL THEN role_id
        WHEN conversation_id IS NOT NULL THEN conversation_id::text
        ELSE 'global'
    END,
    scope_principal_id = CASE
        WHEN agent_principal_id IS NOT NULL THEN agent_principal_id
        WHEN principal_id IS NOT NULL THEN principal_id
        ELSE NULL
    END,
    scope_role_id = CASE
        WHEN agent_principal_id IS NULL
            AND principal_id IS NULL
            AND role_id IS NOT NULL THEN role_id
        ELSE NULL
    END
WHERE scope_type IS NULL;

ALTER TABLE events
    ALTER COLUMN scope_type SET NOT NULL,
    ALTER COLUMN scope_id SET NOT NULL,
    ADD CONSTRAINT fk_events_scope_project_org FOREIGN KEY (scope_project_id, scope_org_id) REFERENCES projects(id, org_id),
    ADD CONSTRAINT ck_events_scope_role_id CHECK (scope_role_id IS NULL OR scope_role_id IN ('designer', 'developer', 'cto', 'cfo', 'coo', 'ceo')),
    ADD CONSTRAINT ck_events_scope_type CHECK (scope_type IN ('global', 'org', 'user', 'project', 'role', 'agent', 'session')),
    ADD CONSTRAINT ck_events_scope_consistency CHECK (
        (
            scope_type = 'global'
            AND scope_id = 'global'
            AND scope_org_id IS NULL
            AND scope_project_id IS NULL
            AND scope_principal_id IS NULL
            AND scope_role_id IS NULL
        )
        OR (
            scope_type = 'org'
            AND scope_org_id IS NOT NULL
            AND scope_id = scope_org_id::text
            AND scope_project_id IS NULL
            AND scope_principal_id IS NULL
            AND scope_role_id IS NULL
        )
        OR (
            scope_type IN ('user', 'agent')
            AND scope_principal_id IS NOT NULL
            AND scope_id = scope_principal_id::text
            AND scope_org_id IS NULL
            AND scope_project_id IS NULL
            AND scope_role_id IS NULL
        )
        OR (
            scope_type = 'project'
            AND scope_project_id IS NOT NULL
            AND scope_org_id IS NOT NULL
            AND scope_id = scope_project_id::text
            AND scope_principal_id IS NULL
            AND scope_role_id IS NULL
        )
        OR (
            scope_type = 'role'
            AND scope_role_id IS NOT NULL
            AND scope_id = scope_role_id
            AND scope_org_id IS NULL
            AND scope_project_id IS NULL
            AND scope_principal_id IS NULL
        )
        OR (
            scope_type = 'session'
            AND scope_id <> 'global'
            AND scope_org_id IS NULL
            AND scope_project_id IS NULL
            AND scope_principal_id IS NULL
            AND scope_role_id IS NULL
        )
    );

ALTER TABLE memory_facts
    ADD CONSTRAINT fk_memory_facts_project_org FOREIGN KEY (project_id, org_id) REFERENCES projects(id, org_id);

ALTER TABLE role_memory_lenses
    DROP CONSTRAINT IF EXISTS role_memory_lenses_project_id_fkey,
    ADD COLUMN scope_type TEXT,
    ADD COLUMN scope_id TEXT,
    ADD COLUMN org_id UUID REFERENCES organizations(id);

UPDATE role_memory_lenses AS lens
SET
    scope_type = 'project',
    scope_id = lens.project_id::text,
    org_id = project.org_id
FROM projects AS project
WHERE lens.project_id = project.id;

UPDATE role_memory_lenses AS lens
SET
    scope_type = 'org',
    scope_id = fact.org_id::text,
    org_id = fact.org_id
FROM memory_facts AS fact
WHERE lens.project_id IS NULL
    AND fact.id = lens.base_memory_fact_id
    AND fact.scope_type = 'org';

UPDATE role_memory_lenses
SET
    scope_type = 'global',
    scope_id = 'global'
WHERE scope_type IS NULL;

ALTER TABLE role_memory_lenses
    ALTER COLUMN scope_type SET NOT NULL,
    ALTER COLUMN scope_id SET NOT NULL,
    ADD CONSTRAINT fk_role_memory_lenses_project_org FOREIGN KEY (project_id, org_id) REFERENCES projects(id, org_id),
    ADD CONSTRAINT ck_role_memory_lenses_scope_type CHECK (scope_type IN ('global', 'org', 'project')),
    ADD CONSTRAINT ck_role_memory_lenses_scope_consistency CHECK (
        (
            scope_type = 'global'
            AND scope_id = 'global'
            AND org_id IS NULL
            AND project_id IS NULL
        )
        OR (
            scope_type = 'org'
            AND org_id IS NOT NULL
            AND scope_id = org_id::text
            AND project_id IS NULL
        )
        OR (
            scope_type = 'project'
            AND project_id IS NOT NULL
            AND org_id IS NOT NULL
            AND scope_id = project_id::text
        )
    );

ALTER TABLE memory_embeddings
    DROP CONSTRAINT IF EXISTS memory_embeddings_embedding_dimension_check,
    ADD CONSTRAINT ck_memory_embeddings_dimension_matches_vector CHECK (
        embedding_dimension > 0
        AND vector_dims(embedding) = embedding_dimension
    );

CREATE OR REPLACE FUNCTION validate_role_assignment_scope()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    IF NEW.scope_type = 'global' THEN
        RETURN NEW;
    END IF;

    IF NEW.scope_type = 'org' THEN
        PERFORM 1
        FROM organizations o
        WHERE o.id = NEW.scope_id;

        IF NOT FOUND THEN
            RAISE EXCEPTION 'role_assignments.scope_id % does not reference an organization', NEW.scope_id;
        END IF;

        RETURN NEW;
    END IF;

    IF NEW.scope_type = 'project' THEN
        PERFORM 1
        FROM projects p
        WHERE p.id = NEW.scope_id;

        IF NOT FOUND THEN
            RAISE EXCEPTION 'role_assignments.scope_id % does not reference a project', NEW.scope_id;
        END IF;

        RETURN NEW;
    END IF;

    RAISE EXCEPTION 'unsupported role_assignments.scope_type %', NEW.scope_type;
END;
$$;

DO $$
DECLARE
    invalid_assignment_id UUID;
BEGIN
    SELECT ra.id
    INTO invalid_assignment_id
    FROM role_assignments AS ra
    WHERE (
            ra.scope_type = 'org'
            AND NOT EXISTS (
                SELECT 1
                FROM organizations AS org
                WHERE org.id = ra.scope_id
            )
        )
        OR (
            ra.scope_type = 'project'
            AND NOT EXISTS (
                SELECT 1
                FROM projects AS project
                WHERE project.id = ra.scope_id
            )
        )
    LIMIT 1;

    IF invalid_assignment_id IS NOT NULL THEN
        RAISE EXCEPTION 'existing role_assignments row % has an invalid scope reference', invalid_assignment_id;
    END IF;
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
    LEFT JOIN memory_facts AS fact ON fact.id = lens.base_memory_fact_id
    LEFT JOIN projects AS project ON project.id = lens.project_id
    WHERE fact.id IS NULL
        OR (
            lens.scope_type = 'global'
            AND fact.scope_type <> 'global'
        )
        OR (
            lens.scope_type = 'org'
            AND (
                fact.scope_type <> 'org'
                OR fact.org_id IS DISTINCT FROM lens.org_id
            )
        )
        OR (
            lens.scope_type = 'project'
            AND (
                project.id IS NULL
                OR lens.org_id IS DISTINCT FROM project.org_id
                OR NOT (
                    (fact.scope_type = 'project' AND fact.project_id = lens.project_id)
                    OR (fact.scope_type = 'org' AND fact.org_id = project.org_id)
                )
            )
        )
        OR lens.scope_type NOT IN ('global', 'org', 'project')
    LIMIT 1;

    IF invalid_lens_id IS NOT NULL THEN
        RAISE EXCEPTION 'existing role_memory_lenses row % has an invalid base memory scope', invalid_lens_id;
    END IF;
END;
$$;

CREATE OR REPLACE FUNCTION validate_memory_chunk_source_scope()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
DECLARE
    expected_scope_type TEXT;
    expected_scope_id TEXT;
    expected_namespace TEXT;
    expected_namespace_prefix TEXT;
    lens_role_id TEXT;
    lens_scope_type TEXT;
    lens_scope_id TEXT;
    lens_org_id UUID;
    lens_project_id UUID;
BEGIN
    IF NEW.source_type = 'memory_fact' THEN
        SELECT mf.scope_type, mf.scope_id, mf.namespace
        INTO expected_scope_type, expected_scope_id, expected_namespace
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

        RETURN NEW;
    END IF;

    IF NEW.source_type = 'role_memory_lens' THEN
        SELECT rml.role_id, rml.scope_type, rml.scope_id, rml.org_id, rml.project_id
        INTO lens_role_id, lens_scope_type, lens_scope_id, lens_org_id, lens_project_id
        FROM role_memory_lenses rml
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
    LEFT JOIN memory_facts AS fact
        ON chunk.source_type = 'memory_fact'
        AND fact.id = chunk.source_id
    LEFT JOIN role_memory_lenses AS lens
        ON chunk.source_type = 'role_memory_lens'
        AND lens.id = chunk.source_id
    WHERE (
            chunk.source_type = 'memory_fact'
            AND (
                fact.id IS NULL
                OR chunk.scope_type <> fact.scope_type
                OR chunk.scope_id <> fact.scope_id
                OR chunk.namespace <> fact.namespace
            )
        )
        OR (
            chunk.source_type = 'role_memory_lens'
            AND (
                lens.id IS NULL
                OR chunk.scope_type <> CASE lens.scope_type
                    WHEN 'global' THEN 'role'
                    WHEN 'org' THEN 'org'
                    WHEN 'project' THEN 'project'
                    ELSE NULL
                END
                OR chunk.scope_id <> CASE lens.scope_type
                    WHEN 'global' THEN lens.role_id
                    WHEN 'org' THEN lens.scope_id
                    WHEN 'project' THEN lens.scope_id
                    ELSE NULL
                END
                OR NOT (
                    chunk.namespace = CASE lens.scope_type
                        WHEN 'global' THEN '/role/' || lens.role_id || '/shared'
                        WHEN 'org' THEN '/org/' || lens.org_id::text || '/role/' || lens.role_id || '/lens'
                        WHEN 'project' THEN '/project/' || lens.project_id::text || '/role/' || lens.role_id || '/lens'
                        ELSE NULL
                    END
                    OR chunk.namespace LIKE CASE lens.scope_type
                        WHEN 'global' THEN '/role/' || lens.role_id || '/shared/%'
                        WHEN 'org' THEN '/org/' || lens.org_id::text || '/role/' || lens.role_id || '/lens/%'
                        WHEN 'project' THEN '/project/' || lens.project_id::text || '/role/' || lens.role_id || '/lens/%'
                        ELSE NULL
                    END
                )
            )
        )
    LIMIT 1;

    IF invalid_chunk_id IS NOT NULL THEN
        RAISE EXCEPTION 'existing memory_chunks row % has an invalid source scope or namespace', invalid_chunk_id;
    END IF;
END;
$$;

CREATE TRIGGER trg_memory_chunks_validate_source_scope
    BEFORE INSERT OR UPDATE OF source_type, source_id, scope_type, scope_id, namespace ON memory_chunks
    FOR EACH ROW
    EXECUTE FUNCTION validate_memory_chunk_source_scope();

CREATE TRIGGER trg_role_assignments_validate_scope
    BEFORE INSERT OR UPDATE OF scope_type, scope_id ON role_assignments
    FOR EACH ROW
    EXECUTE FUNCTION validate_role_assignment_scope();

DROP TRIGGER IF EXISTS trg_role_memory_lenses_validate_base_scope ON role_memory_lenses;

CREATE TRIGGER trg_role_memory_lenses_validate_base_scope
    BEFORE INSERT OR UPDATE OF scope_type, scope_id, org_id, project_id, base_memory_fact_id ON role_memory_lenses
    FOR EACH ROW
    EXECUTE FUNCTION validate_role_memory_lens_base_scope();

CREATE INDEX ix_events_scope_created_at
    ON events (scope_type, scope_id, created_at DESC);

CREATE INDEX ix_events_scope_org_created_at
    ON events (scope_org_id, created_at DESC)
    WHERE scope_org_id IS NOT NULL;

CREATE INDEX ix_events_scope_project_created_at
    ON events (scope_project_id, created_at DESC)
    WHERE scope_project_id IS NOT NULL;

CREATE INDEX ix_events_scope_principal_created_at
    ON events (scope_principal_id, created_at DESC)
    WHERE scope_principal_id IS NOT NULL;

CREATE INDEX ix_role_memory_lenses_role_org_status
    ON role_memory_lenses (role_id, org_id, status);

CREATE INDEX ix_role_memory_lenses_scope_status
    ON role_memory_lenses (scope_type, scope_id, status);
