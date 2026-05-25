CREATE TABLE worker_heartbeats (
    worker_type TEXT NOT NULL,
    worker_id TEXT NOT NULL,
    status TEXT NOT NULL,
    last_seen_at TIMESTAMPTZ NOT NULL,
    last_success_at TIMESTAMPTZ NULL,
    last_error TEXT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (worker_type, worker_id),
    CONSTRAINT ck_worker_heartbeats_worker_type_not_blank CHECK (btrim(worker_type) <> ''),
    CONSTRAINT ck_worker_heartbeats_worker_id_not_blank CHECK (btrim(worker_id) <> ''),
    CONSTRAINT ck_worker_heartbeats_status CHECK (status IN ('starting', 'running', 'error', 'stopped'))
);

CREATE INDEX ix_worker_heartbeats_type_last_seen_at
    ON worker_heartbeats (worker_type, last_seen_at DESC);
