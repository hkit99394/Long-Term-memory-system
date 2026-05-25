CREATE TABLE vault_exports (
    id UUID PRIMARY KEY,
    memory_fact_id UUID NOT NULL REFERENCES memory_facts(id),
    export_type TEXT NOT NULL,
    export_path TEXT NOT NULL,
    source_event_id UUID NOT NULL REFERENCES events(id),
    status TEXT NOT NULL,
    stale_reason TEXT,
    exported_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    stale_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (export_type, memory_fact_id),
    CHECK (export_type IN ('obsidian_markdown')),
    CHECK (status IN ('current', 'stale')),
    CHECK (btrim(export_path) = export_path),
    CHECK (export_path <> ''),
    CHECK (export_path NOT LIKE '/%')
);

CREATE TRIGGER trg_vault_exports_set_updated_at
    BEFORE UPDATE ON vault_exports
    FOR EACH ROW
    EXECUTE FUNCTION set_updated_at();

CREATE INDEX ix_vault_exports_status
    ON vault_exports (export_type, status, updated_at);

CREATE INDEX ix_vault_exports_memory_fact_id
    ON vault_exports (memory_fact_id);

CREATE INDEX ix_vault_exports_source_event_id
    ON vault_exports (source_event_id);
