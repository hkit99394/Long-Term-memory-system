ALTER TABLE memory_retrieval_feedback
    ADD COLUMN packet_id UUID,
    ADD COLUMN item_id UUID,
    ADD CONSTRAINT ck_memory_retrieval_feedback_item_requires_packet
        CHECK (item_id IS NULL OR packet_id IS NOT NULL);

CREATE INDEX ix_memory_retrieval_feedback_packet
    ON memory_retrieval_feedback (packet_id)
    WHERE packet_id IS NOT NULL;

CREATE INDEX ix_memory_retrieval_feedback_item
    ON memory_retrieval_feedback (item_id)
    WHERE item_id IS NOT NULL;
