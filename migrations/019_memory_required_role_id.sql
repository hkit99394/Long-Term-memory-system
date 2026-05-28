CREATE OR REPLACE FUNCTION memory_required_role_id(
    memory_namespace text,
    memory_scope_type text,
    memory_scope_id text,
    explicit_role_id text DEFAULT NULL)
RETURNS text
LANGUAGE sql
IMMUTABLE
AS $$
    SELECT COALESCE(
        CASE
            WHEN memory_scope_type = 'role' THEN NULLIF(memory_scope_id, '')
            ELSE NULL
        END,
        NULLIF(explicit_role_id, ''),
        CASE
            WHEN memory_namespace LIKE '/role/%' THEN NULLIF(split_part(memory_namespace, '/', 3), '')
            WHEN memory_namespace LIKE '/project/%/role/%' THEN NULLIF(split_part(memory_namespace, '/', 5), '')
            WHEN memory_namespace LIKE '/org/%/role/%' THEN NULLIF(split_part(memory_namespace, '/', 5), '')
            ELSE NULL
        END
    );
$$;
