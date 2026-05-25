CREATE OR REPLACE FUNCTION set_memory_chunk_search_vector()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    NEW.search_vector = to_tsvector('english', concat_ws(' ', NEW.title, NEW.content));
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_memory_chunks_set_search_vector ON memory_chunks;

CREATE TRIGGER trg_memory_chunks_set_search_vector
    BEFORE INSERT OR UPDATE OF title, content ON memory_chunks
    FOR EACH ROW
    EXECUTE FUNCTION set_memory_chunk_search_vector();

UPDATE memory_chunks
SET search_vector = to_tsvector('english', concat_ws(' ', title, content))
WHERE search_vector IS NULL;

ALTER TABLE memory_chunks
    ALTER COLUMN search_vector SET NOT NULL;

CREATE INDEX IF NOT EXISTS ix_memory_chunks_search_vector
    ON memory_chunks USING GIN (search_vector);

COMMENT ON COLUMN memory_chunks.search_vector
    IS 'Full-text search vector derived from memory chunk title and content.';
