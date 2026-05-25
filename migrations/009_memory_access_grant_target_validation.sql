DO $$
DECLARE
    invalid_grant_id UUID;
BEGIN
    SELECT id
    INTO invalid_grant_id
    FROM memory_access_grants
    WHERE NOT (
        (principal_id IS NOT NULL AND role_id IS NULL)
        OR (principal_id IS NULL AND role_id IS NOT NULL)
    )
    LIMIT 1;

    IF invalid_grant_id IS NOT NULL THEN
        RAISE EXCEPTION 'existing memory_access_grants row % does not have exactly one target principal_id or role_id',
            invalid_grant_id;
    END IF;
END;
$$;

ALTER TABLE memory_access_grants
    DROP CONSTRAINT IF EXISTS ck_memory_access_grants_exactly_one_target;

ALTER TABLE memory_access_grants
    ADD CONSTRAINT ck_memory_access_grants_exactly_one_target CHECK (
        (principal_id IS NOT NULL AND role_id IS NULL)
        OR (principal_id IS NULL AND role_id IS NOT NULL)
    );
