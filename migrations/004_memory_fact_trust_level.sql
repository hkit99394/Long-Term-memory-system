ALTER TABLE memory_facts
    ADD COLUMN trust_level TEXT;

UPDATE memory_facts AS fact
SET trust_level = event.trust_level
FROM events AS event
WHERE fact.source_event_id = event.id;

UPDATE memory_facts
SET trust_level = 'user_scoped'
WHERE trust_level IS NULL;

ALTER TABLE memory_facts
    ALTER COLUMN trust_level SET NOT NULL,
    ADD CONSTRAINT ck_memory_facts_trust_level CHECK (trust_level IN (
        'system_trusted',
        'human_approved',
        'user_scoped',
        'agent_private',
        'tool_output',
        'retrieved_untrusted',
        'web_content'
    ));
