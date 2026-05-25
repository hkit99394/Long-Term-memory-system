# 0034 Operational Health Checks

## Status

Accepted.

## Context

M8-01 expands the original API health endpoint from a simple process/database probe into an operational readiness surface. The API already exposes liveness and readiness endpoints, and the worker now owns background indexing through the outbox.

Operators need to know whether the API can reach PostgreSQL, whether outbox work is accumulating, whether a worker is alive, and whether semantic indexing/search has a usable embedding provider for the current environment.

## Decision

Keep the existing endpoint split:

- `GET /health/live` only reports API process liveness through the `self` check.
- `GET /health/ready` reports readiness checks and maps degraded or unhealthy readiness to `503`.
- `GET /health` reports all checks and keeps ASP.NET Core's default degraded response behavior.

Readiness now includes:

- `postgres`: executes a short `SELECT 1`.
- `outbox`: summarizes ready, delayed, processing, failed, retrying, dead-letter, expired lease, and oldest ready-pending backlog signals.
- `worker`: reads the freshest `worker_heartbeats` row for the outbox worker type and degrades when no heartbeat exists, the heartbeat is stale, or the latest status is `error` or `stopped`.
- `embedding_provider`: validates embedding provider configuration and environment usability without sending a live embedding request to the external provider.

Outbox workers record best-effort heartbeats with status `starting`, `running`, `error`, and `stopped`. Running heartbeats update `last_success_at`; error heartbeats retain the last successful heartbeat and store a truncated error message.

## Consequences

- API readiness can fail before user-facing API calls fail when the outbox worker stops or semantic indexing is not safely configured.
- Health checks remain fast and avoid calling OpenAI or any other billable embedding API.
- A newly migrated database without a running worker reports degraded readiness until an outbox worker records its first heartbeat.
