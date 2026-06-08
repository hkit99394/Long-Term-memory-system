ALTER TABLE projects
    DROP CONSTRAINT IF EXISTS projects_status_check,
    DROP CONSTRAINT IF EXISTS ck_projects_status;

ALTER TABLE projects
    ADD CONSTRAINT ck_projects_status CHECK (status IN ('planned', 'active', 'archived', 'deleted'));

ALTER TABLE access_audit_events
    DROP CONSTRAINT IF EXISTS access_audit_events_action_type_check,
    DROP CONSTRAINT IF EXISTS ck_access_audit_events_action_type;

ALTER TABLE access_audit_events
    ADD CONSTRAINT ck_access_audit_events_action_type CHECK (action_type IN (
        'authentication',
        'authorization_denied',
        'principal_change',
        'identity_binding_change',
        'organization_membership_change',
        'project_membership_change',
        'project_registration',
        'project_role_definition_change',
        'role_assignment_change',
        'namespace_grant_change',
        'service_credential_change',
        'audit_export'
    ));
