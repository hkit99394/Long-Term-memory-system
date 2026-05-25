ALTER TABLE vault_exports
    DROP CONSTRAINT IF EXISTS vault_exports_export_type_check;

ALTER TABLE vault_exports
    DROP CONSTRAINT IF EXISTS ck_vault_exports_export_type;

ALTER TABLE vault_exports
    ADD CONSTRAINT ck_vault_exports_export_type CHECK (
        export_type IN ('obsidian_markdown', 'obsidian_archive')
    );
