ALTER TABLE memory_retrieval_feedback
    DROP CONSTRAINT IF EXISTS memory_retrieval_feedback_feedback_type_check;

ALTER TABLE memory_retrieval_feedback
    DROP CONSTRAINT IF EXISTS ck_memory_retrieval_feedback_feedback_type;

ALTER TABLE memory_retrieval_feedback
    ADD CONSTRAINT ck_memory_retrieval_feedback_feedback_type
    CHECK (feedback_type IN (
        'useful',
        'stale',
        'wrong',
        'sensitive',
        'over_broad',
        'missing',
        'noisy'
    ));
