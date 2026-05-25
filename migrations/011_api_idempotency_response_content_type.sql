ALTER TABLE api_idempotency_keys
    ADD COLUMN response_content_type TEXT;

UPDATE api_idempotency_keys
SET response_content_type = CASE
    WHEN response_body IS NULL THEN NULL
    WHEN response_status >= 400 THEN 'application/problem+json; charset=utf-8'
    ELSE 'application/json; charset=utf-8'
END
WHERE response_content_type IS NULL;

ALTER TABLE api_idempotency_keys
    ADD CONSTRAINT chk_api_idempotency_response_content_type_not_blank
        CHECK (response_content_type IS NULL OR btrim(response_content_type) <> '');
