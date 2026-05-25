DO $$
DECLARE
    duplicate_role_id TEXT;
    duplicate_scope_type TEXT;
    duplicate_scope_id TEXT;
    duplicate_base_memory_fact_id UUID;
    duplicate_interpretation TEXT;
BEGIN
    SELECT
        role_id,
        scope_type,
        scope_id,
        base_memory_fact_id,
        lower(btrim(interpretation))
    INTO
        duplicate_role_id,
        duplicate_scope_type,
        duplicate_scope_id,
        duplicate_base_memory_fact_id,
        duplicate_interpretation
    FROM role_memory_lenses
    WHERE status = 'active'
    GROUP BY
        role_id,
        scope_type,
        scope_id,
        base_memory_fact_id,
        lower(btrim(interpretation))
    HAVING count(*) > 1
    LIMIT 1;

    IF duplicate_role_id IS NOT NULL THEN
        RAISE EXCEPTION 'existing active role memory lenses contain a duplicate role %, scope %, id %, base fact %, interpretation %',
            duplicate_role_id,
            duplicate_scope_type,
            duplicate_scope_id,
            duplicate_base_memory_fact_id,
            duplicate_interpretation;
    END IF;
END;
$$;

DROP INDEX IF EXISTS ux_role_memory_lenses_active_dedupe;

CREATE UNIQUE INDEX ux_role_memory_lenses_active_dedupe
    ON role_memory_lenses (role_id, scope_type, scope_id, base_memory_fact_id, lower(btrim(interpretation)))
    WHERE status = 'active';
