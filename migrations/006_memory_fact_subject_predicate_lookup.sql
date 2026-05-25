CREATE INDEX IF NOT EXISTS ix_memory_facts_active_subject_predicate
    ON memory_facts (scope_type, scope_id, memory_type, lower(btrim(subject)), lower(btrim(predicate)))
    WHERE status = 'active';
