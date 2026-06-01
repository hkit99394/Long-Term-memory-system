CREATE TABLE access_audit_events (
    id UUID PRIMARY KEY,
    action_type TEXT NOT NULL,
    outcome TEXT NOT NULL,
    actor_principal_id UUID REFERENCES principals(id),
    target_principal_id UUID REFERENCES principals(id),
    principal_type TEXT,
    auth_method TEXT,
    credential_id TEXT,
    identity_binding_id UUID REFERENCES identity_bindings(id),
    scope_type TEXT,
    scope_id TEXT,
    role_id TEXT,
    namespace_prefix TEXT,
    permission TEXT,
    resource_type TEXT,
    resource_id TEXT,
    reason_code TEXT,
    request_method TEXT,
    request_path TEXT,
    correlation_id TEXT,
    audit_metadata JSONB NOT NULL DEFAULT '{}'::jsonb,
    occurred_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CHECK (action_type IN (
        'authentication',
        'authorization_denied',
        'principal_change',
        'identity_binding_change',
        'organization_membership_change',
        'project_membership_change',
        'role_assignment_change',
        'namespace_grant_change',
        'service_credential_change',
        'audit_export'
    )),
    CHECK (outcome IN ('succeeded', 'failed', 'denied')),
    CHECK (principal_type IS NULL OR principal_type IN ('human', 'agent', 'service')),
    CHECK (auth_method IS NULL OR auth_method IN ('api_key', 'oidc', 'service_account')),
    CHECK (
        (scope_type IS NULL AND scope_id IS NULL)
        OR (scope_type IS NOT NULL AND scope_id IS NOT NULL)
    ),
    CHECK (scope_type IS NULL OR scope_type IN ('global', 'org', 'user', 'project', 'role', 'agent', 'session')),
    CHECK (role_id IS NULL OR role_id IN ('designer', 'developer', 'cto', 'cfo', 'coo', 'ceo')),
    CHECK (namespace_prefix IS NULL OR namespace_prefix LIKE '/%'),
    CHECK (permission IS NULL OR permission IN ('read', 'write', 'review', 'admin')),
    CHECK (credential_id IS NULL OR (credential_id = btrim(credential_id) AND credential_id <> '')),
    CHECK (resource_type IS NULL OR (resource_type = btrim(resource_type) AND resource_type <> '')),
    CHECK (resource_id IS NULL OR (resource_id = btrim(resource_id) AND resource_id <> '')),
    CHECK (reason_code IS NULL OR (reason_code = btrim(reason_code) AND reason_code <> '')),
    CHECK (request_method IS NULL OR (request_method = btrim(request_method) AND request_method = upper(request_method) AND request_method <> '')),
    CHECK (request_path IS NULL OR request_path LIKE '/%'),
    CHECK (correlation_id IS NULL OR (correlation_id = btrim(correlation_id) AND correlation_id <> '')),
    CHECK (jsonb_typeof(audit_metadata) = 'object'),
    CHECK (NOT (audit_metadata ?| ARRAY[
        'rawPayload',
        'raw_payload',
        'payload',
        'content',
        'sourcePayload',
        'source_payload',
        'memoryText',
        'memory_text',
        'memoryObject',
        'memory_object',
        'eventContent',
        'event_content'
    ]))
);

CREATE INDEX ix_access_audit_events_occurred_at
    ON access_audit_events (occurred_at DESC);

CREATE INDEX ix_access_audit_events_actor_occurred_at
    ON access_audit_events (actor_principal_id, occurred_at DESC)
    WHERE actor_principal_id IS NOT NULL;

CREATE INDEX ix_access_audit_events_target_occurred_at
    ON access_audit_events (target_principal_id, occurred_at DESC)
    WHERE target_principal_id IS NOT NULL;

CREATE INDEX ix_access_audit_events_action_outcome_occurred_at
    ON access_audit_events (action_type, outcome, occurred_at DESC);

CREATE INDEX ix_access_audit_events_scope_occurred_at
    ON access_audit_events (scope_type, scope_id, occurred_at DESC)
    WHERE scope_type IS NOT NULL;

CREATE INDEX ix_access_audit_events_resource_occurred_at
    ON access_audit_events (resource_type, resource_id, occurred_at DESC)
    WHERE resource_type IS NOT NULL;

CREATE INDEX ix_access_audit_events_auth_credential_occurred_at
    ON access_audit_events (auth_method, credential_id, occurred_at DESC)
    WHERE auth_method IS NOT NULL;
