DO $$
DECLARE
    invalid_job_id UUID;
BEGIN
    SELECT job.id
    INTO invalid_job_id
    FROM outbox_jobs AS job
    WHERE job.status = 'processing'
        AND (
            job.locked_until IS NULL
            OR job.locked_by IS NULL
            OR btrim(job.locked_by) = ''
        )
    LIMIT 1;

    IF invalid_job_id IS NOT NULL THEN
        RAISE EXCEPTION 'existing outbox_jobs row % is processing without lease metadata', invalid_job_id;
    END IF;
END;
$$;

ALTER TABLE outbox_jobs
    ADD CONSTRAINT ck_outbox_jobs_processing_has_lease CHECK (
        status <> 'processing'
        OR (
            locked_until IS NOT NULL
            AND locked_by IS NOT NULL
            AND btrim(locked_by) <> ''
        )
    );
