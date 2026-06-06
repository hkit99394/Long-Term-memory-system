ALTER TABLE role_assignments
    DROP CONSTRAINT IF EXISTS role_assignments_role_id_check,
    DROP CONSTRAINT IF EXISTS ck_role_assignments_role_id;

ALTER TABLE role_assignments
    ADD CONSTRAINT ck_role_assignments_role_id CHECK (role_id IN (
        'product_owner',
        'cto',
        'security_professional',
        'it_manager',
        'developer',
        'tester_qa',
        'release_manager',
        'knowledge_steward',
        'designer',
        'cfo',
        'coo',
        'ceo'
    ));

ALTER TABLE memory_access_grants
    DROP CONSTRAINT IF EXISTS memory_access_grants_role_id_check,
    DROP CONSTRAINT IF EXISTS ck_memory_access_grants_role_id;

ALTER TABLE memory_access_grants
    ADD CONSTRAINT ck_memory_access_grants_role_id CHECK (role_id IS NULL OR role_id IN (
        'product_owner',
        'cto',
        'security_professional',
        'it_manager',
        'developer',
        'tester_qa',
        'release_manager',
        'knowledge_steward',
        'designer',
        'cfo',
        'coo',
        'ceo'
    ));

ALTER TABLE events
    DROP CONSTRAINT IF EXISTS events_role_id_check,
    DROP CONSTRAINT IF EXISTS ck_events_role_id,
    DROP CONSTRAINT IF EXISTS ck_events_scope_role_id;

ALTER TABLE events
    ADD CONSTRAINT ck_events_role_id CHECK (role_id IS NULL OR role_id IN (
        'product_owner',
        'cto',
        'security_professional',
        'it_manager',
        'developer',
        'tester_qa',
        'release_manager',
        'knowledge_steward',
        'designer',
        'cfo',
        'coo',
        'ceo'
    )),
    ADD CONSTRAINT ck_events_scope_role_id CHECK (scope_role_id IS NULL OR scope_role_id IN (
        'product_owner',
        'cto',
        'security_professional',
        'it_manager',
        'developer',
        'tester_qa',
        'release_manager',
        'knowledge_steward',
        'designer',
        'cfo',
        'coo',
        'ceo'
    ));

ALTER TABLE memory_facts
    DROP CONSTRAINT IF EXISTS memory_facts_role_id_check,
    DROP CONSTRAINT IF EXISTS ck_memory_facts_role_id;

ALTER TABLE memory_facts
    ADD CONSTRAINT ck_memory_facts_role_id CHECK (role_id IS NULL OR role_id IN (
        'product_owner',
        'cto',
        'security_professional',
        'it_manager',
        'developer',
        'tester_qa',
        'release_manager',
        'knowledge_steward',
        'designer',
        'cfo',
        'coo',
        'ceo'
    ));

ALTER TABLE role_memory_lenses
    DROP CONSTRAINT IF EXISTS role_memory_lenses_role_id_check,
    DROP CONSTRAINT IF EXISTS ck_role_memory_lenses_role_id;

ALTER TABLE role_memory_lenses
    ADD CONSTRAINT ck_role_memory_lenses_role_id CHECK (role_id IN (
        'product_owner',
        'cto',
        'security_professional',
        'it_manager',
        'developer',
        'tester_qa',
        'release_manager',
        'knowledge_steward',
        'designer',
        'cfo',
        'coo',
        'ceo'
    ));

ALTER TABLE memory_retrieval_feedback
    DROP CONSTRAINT IF EXISTS memory_retrieval_feedback_role_id_check,
    DROP CONSTRAINT IF EXISTS ck_memory_retrieval_feedback_role_id;

ALTER TABLE memory_retrieval_feedback
    ADD CONSTRAINT ck_memory_retrieval_feedback_role_id CHECK (role_id IS NULL OR role_id IN (
        'product_owner',
        'cto',
        'security_professional',
        'it_manager',
        'developer',
        'tester_qa',
        'release_manager',
        'knowledge_steward',
        'designer',
        'cfo',
        'coo',
        'ceo'
    ));

ALTER TABLE memory_context_packets
    DROP CONSTRAINT IF EXISTS memory_context_packets_role_id_check,
    DROP CONSTRAINT IF EXISTS ck_memory_context_packets_role_id;

ALTER TABLE memory_context_packets
    ADD CONSTRAINT ck_memory_context_packets_role_id CHECK (role_id IS NULL OR role_id IN (
        'product_owner',
        'cto',
        'security_professional',
        'it_manager',
        'developer',
        'tester_qa',
        'release_manager',
        'knowledge_steward',
        'designer',
        'cfo',
        'coo',
        'ceo'
    ));

ALTER TABLE access_audit_events
    DROP CONSTRAINT IF EXISTS access_audit_events_role_id_check,
    DROP CONSTRAINT IF EXISTS ck_access_audit_events_role_id;

ALTER TABLE access_audit_events
    ADD CONSTRAINT ck_access_audit_events_role_id CHECK (role_id IS NULL OR role_id IN (
        'product_owner',
        'cto',
        'security_professional',
        'it_manager',
        'developer',
        'tester_qa',
        'release_manager',
        'knowledge_steward',
        'designer',
        'cfo',
        'coo',
        'ceo'
    ));
