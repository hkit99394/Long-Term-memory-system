CREATE TABLE service_accounts (
    principal_id UUID PRIMARY KEY REFERENCES principals(id),
    owner_org_id UUID REFERENCES organizations(id),
    owner_project_id UUID REFERENCES projects(id),
    owner_principal_id UUID REFERENCES principals(id),
    admin_contact TEXT,
    allowed_auth_method TEXT NOT NULL,
    status TEXT NOT NULL DEFAULT 'active',
    review_due_at TIMESTAMPTZ,
    expires_at TIMESTAMPTZ,
    created_by_principal_id UUID REFERENCES principals(id),
    disabled_by_principal_id UUID REFERENCES principals(id),
    disabled_at TIMESTAMPTZ,
    disable_reason TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CHECK (
        (owner_org_id IS NOT NULL AND owner_project_id IS NULL)
        OR (owner_org_id IS NULL AND owner_project_id IS NOT NULL)
    ),
    CHECK (owner_principal_id IS NOT NULL OR (admin_contact IS NOT NULL AND admin_contact = btrim(admin_contact) AND admin_contact <> '')),
    CHECK (admin_contact IS NULL OR (admin_contact = btrim(admin_contact) AND admin_contact <> '')),
    CHECK (allowed_auth_method IN ('api_key', 'oidc', 'service_account')),
    CHECK (status IN ('active', 'disabled', 'deleted')),
    CHECK (review_due_at IS NOT NULL OR expires_at IS NOT NULL),
    CHECK (expires_at IS NULL OR review_due_at IS NULL OR expires_at >= review_due_at),
    CHECK (
        (status = 'active' AND disabled_at IS NULL AND disabled_by_principal_id IS NULL AND disable_reason IS NULL)
        OR (status IN ('disabled', 'deleted') AND disabled_at IS NOT NULL AND disabled_by_principal_id IS NOT NULL AND disable_reason IS NOT NULL)
    ),
    CHECK (disable_reason IS NULL OR (disable_reason = btrim(disable_reason) AND disable_reason <> ''))
);

CREATE TABLE service_account_credentials (
    id UUID PRIMARY KEY,
    service_principal_id UUID NOT NULL REFERENCES service_accounts(principal_id),
    credential_label TEXT NOT NULL,
    auth_method TEXT NOT NULL,
    credential_fingerprint TEXT NOT NULL,
    status TEXT NOT NULL DEFAULT 'active',
    issued_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    expires_at TIMESTAMPTZ,
    review_due_at TIMESTAMPTZ,
    last_used_at TIMESTAMPTZ,
    rotated_from_credential_id UUID REFERENCES service_account_credentials(id),
    created_by_principal_id UUID REFERENCES principals(id),
    disabled_by_principal_id UUID REFERENCES principals(id),
    disabled_at TIMESTAMPTZ,
    disable_reason TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CHECK (credential_label = btrim(credential_label) AND credential_label <> ''),
    CHECK (auth_method IN ('api_key', 'oidc', 'service_account')),
    CHECK (credential_fingerprint = btrim(credential_fingerprint) AND credential_fingerprint <> ''),
    CHECK (status IN ('active', 'rotated', 'disabled', 'expired')),
    CHECK (review_due_at IS NOT NULL OR expires_at IS NOT NULL),
    CHECK (expires_at IS NULL OR review_due_at IS NULL OR expires_at >= review_due_at),
    CHECK (
        (status = 'active' AND disabled_at IS NULL AND disabled_by_principal_id IS NULL AND disable_reason IS NULL)
        OR (status IN ('rotated', 'disabled', 'expired') AND disabled_at IS NOT NULL AND disabled_by_principal_id IS NOT NULL AND disable_reason IS NOT NULL)
    ),
    CHECK (disable_reason IS NULL OR (disable_reason = btrim(disable_reason) AND disable_reason <> ''))
);

CREATE OR REPLACE FUNCTION validate_service_account_principal()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
DECLARE
    target_principal_type TEXT;
BEGIN
    SELECT principal_type
    INTO target_principal_type
    FROM principals
    WHERE id = NEW.principal_id;

    IF target_principal_type IS NULL THEN
        RAISE EXCEPTION 'service account principal % does not exist', NEW.principal_id;
    END IF;

    IF target_principal_type <> 'service' THEN
        RAISE EXCEPTION 'service account principal % must have principal_type service, not %',
            NEW.principal_id,
            target_principal_type;
    END IF;

    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_service_accounts_validate_principal
    BEFORE INSERT OR UPDATE OF principal_id ON service_accounts
    FOR EACH ROW
    EXECUTE FUNCTION validate_service_account_principal();

CREATE TRIGGER trg_service_accounts_set_updated_at
    BEFORE UPDATE ON service_accounts
    FOR EACH ROW
    EXECUTE FUNCTION set_updated_at();

CREATE TRIGGER trg_service_account_credentials_set_updated_at
    BEFORE UPDATE ON service_account_credentials
    FOR EACH ROW
    EXECUTE FUNCTION set_updated_at();

CREATE INDEX ix_service_accounts_owner_org
    ON service_accounts (owner_org_id, status)
    WHERE owner_org_id IS NOT NULL;

CREATE INDEX ix_service_accounts_owner_project
    ON service_accounts (owner_project_id, status)
    WHERE owner_project_id IS NOT NULL;

CREATE INDEX ix_service_accounts_review_due
    ON service_accounts (review_due_at, status)
    WHERE review_due_at IS NOT NULL;

CREATE UNIQUE INDEX ux_service_account_credentials_active_label
    ON service_account_credentials (service_principal_id, credential_label)
    WHERE status = 'active';

CREATE UNIQUE INDEX ux_service_account_credentials_active_fingerprint
    ON service_account_credentials (credential_fingerprint)
    WHERE status = 'active';

CREATE INDEX ix_service_account_credentials_service_status
    ON service_account_credentials (service_principal_id, status, created_at DESC);

CREATE INDEX ix_service_account_credentials_review_due
    ON service_account_credentials (review_due_at, status)
    WHERE review_due_at IS NOT NULL;
