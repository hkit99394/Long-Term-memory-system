CREATE TABLE identity_bindings (
    id UUID PRIMARY KEY,
    provider TEXT NOT NULL,
    issuer TEXT NOT NULL,
    subject TEXT NOT NULL,
    principal_id UUID NOT NULL REFERENCES principals(id),
    status TEXT NOT NULL DEFAULT 'active',
    external_display_name TEXT,
    external_email TEXT,
    external_tenant_id TEXT,
    provider_metadata JSONB NOT NULL DEFAULT '{}'::jsonb,
    last_seen_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CHECK (provider = btrim(provider) AND provider = lower(provider) AND provider <> ''),
    CHECK (issuer = btrim(issuer) AND issuer <> ''),
    CHECK (subject = btrim(subject) AND subject <> ''),
    CHECK (status IN ('active', 'disabled', 'deleted')),
    CHECK (external_display_name IS NULL OR (external_display_name = btrim(external_display_name) AND external_display_name <> '')),
    CHECK (external_email IS NULL OR (external_email = btrim(external_email) AND external_email <> '')),
    CHECK (external_tenant_id IS NULL OR (external_tenant_id = btrim(external_tenant_id) AND external_tenant_id <> '')),
    CHECK (jsonb_typeof(provider_metadata) = 'object')
);

CREATE TRIGGER trg_identity_bindings_set_updated_at
    BEFORE UPDATE ON identity_bindings
    FOR EACH ROW
    EXECUTE FUNCTION set_updated_at();

CREATE UNIQUE INDEX ux_identity_bindings_active_subject
    ON identity_bindings (provider, issuer, subject)
    WHERE status = 'active';

CREATE INDEX ix_identity_bindings_principal_status
    ON identity_bindings (principal_id, status);

CREATE INDEX ix_identity_bindings_subject_status
    ON identity_bindings (provider, issuer, subject, status);
