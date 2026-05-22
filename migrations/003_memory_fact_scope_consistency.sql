ALTER TABLE memory_facts
    ADD CONSTRAINT ck_memory_facts_scope_owner_namespace_consistency CHECK (
        (
            scope_type = 'global'
            AND scope_id = 'global'
            AND org_id IS NULL
            AND project_id IS NULL
            AND user_principal_id IS NULL
            AND agent_principal_id IS NULL
            AND role_id IS NULL
            AND left(namespace, length('/global/')) = '/global/'
        )
        OR (
            scope_type = 'org'
            AND org_id IS NOT NULL
            AND scope_id = org_id::text
            AND project_id IS NULL
            AND user_principal_id IS NULL
            AND agent_principal_id IS NULL
            AND role_id IS NULL
            AND left(namespace, length('/org/' || org_id::text || '/')) = '/org/' || org_id::text || '/'
        )
        OR (
            scope_type = 'user'
            AND user_principal_id IS NOT NULL
            AND scope_id = user_principal_id::text
            AND org_id IS NULL
            AND project_id IS NULL
            AND agent_principal_id IS NULL
            AND role_id IS NULL
            AND left(namespace, length('/user/' || user_principal_id::text || '/')) = '/user/' || user_principal_id::text || '/'
        )
        OR (
            scope_type = 'project'
            AND project_id IS NOT NULL
            AND org_id IS NOT NULL
            AND scope_id = project_id::text
            AND user_principal_id IS NULL
            AND agent_principal_id IS NULL
            AND role_id IS NULL
            AND left(namespace, length('/project/' || project_id::text || '/')) = '/project/' || project_id::text || '/'
        )
        OR (
            scope_type = 'role'
            AND role_id IS NOT NULL
            AND scope_id = role_id
            AND org_id IS NULL
            AND project_id IS NULL
            AND user_principal_id IS NULL
            AND agent_principal_id IS NULL
            AND left(namespace, length('/role/' || role_id || '/')) = '/role/' || role_id || '/'
        )
        OR (
            scope_type = 'agent'
            AND agent_principal_id IS NOT NULL
            AND scope_id = agent_principal_id::text
            AND org_id IS NULL
            AND project_id IS NULL
            AND user_principal_id IS NULL
            AND role_id IS NULL
            AND left(namespace, length('/agent/' || agent_principal_id::text || '/')) = '/agent/' || agent_principal_id::text || '/'
        )
        OR (
            scope_type = 'session'
            AND scope_id <> 'global'
            AND org_id IS NULL
            AND project_id IS NULL
            AND user_principal_id IS NULL
            AND agent_principal_id IS NULL
            AND role_id IS NULL
            AND left(namespace, length('/session/' || scope_id || '/')) = '/session/' || scope_id || '/'
        )
    );

COMMENT ON CONSTRAINT ck_memory_facts_scope_owner_namespace_consistency ON memory_facts
    IS 'Keeps memory fact scope_type, canonical scope_id, namespace prefix, and typed owner columns aligned.';
