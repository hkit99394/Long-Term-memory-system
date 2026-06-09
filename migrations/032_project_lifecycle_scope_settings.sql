CREATE TABLE IF NOT EXISTS project_scope_settings (
    project_id UUID PRIMARY KEY REFERENCES projects(id) ON DELETE CASCADE,
    default_namespace_prefix TEXT NOT NULL,
    source_hash_required BOOLEAN NOT NULL DEFAULT true,
    memory_retention_class TEXT NOT NULL DEFAULT 'standard',
    review_cadence_days INTEGER NOT NULL DEFAULT 7,
    updated_by_principal_id UUID REFERENCES principals(id),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CHECK (default_namespace_prefix = btrim(default_namespace_prefix)),
    CHECK (default_namespace_prefix LIKE '/project/%/%'),
    CHECK (memory_retention_class IN ('ephemeral', 'standard', 'audit', 'legal_hold')),
    CHECK (review_cadence_days BETWEEN 1 AND 365)
);

CREATE TRIGGER trg_project_scope_settings_set_updated_at
    BEFORE UPDATE ON project_scope_settings
    FOR EACH ROW
    EXECUTE FUNCTION set_updated_at();

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
        'project_lifecycle_change',
        'project_scope_settings_change',
        'project_role_definition_change',
        'role_assignment_change',
        'namespace_grant_change',
        'service_credential_change',
        'audit_export'
    ));
