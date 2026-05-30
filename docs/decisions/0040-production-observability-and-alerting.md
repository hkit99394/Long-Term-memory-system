# 0040 Production Observability and Alerting

## Status

Accepted.

## Context

The production-pilot deployment shape now separates the migrator, API, worker,
managed PostgreSQL, secret store, rollback procedure, and restore validation
path. The next risk is operational visibility: an operator needs to know
whether the service is reachable, ready, processing outbox work, preserving
source-linked retrieval quality, and recoverable from backup.

The current implementation already exposes health checks, structured logs,
worker heartbeat checks, outbox backlog checks, and an authenticated operations
summary. It does not yet include a metrics exporter, distributed tracing,
dashboards, or alert rules as code.

## Decision

Adopt [Production Observability and Alerting](../production-observability.md)
as the production-pilot observability contract.

The pilot will observe these areas first:

- API traffic, errors, latency, readiness, and authentication failure patterns
- memory write-path decisions and idempotency outcomes
- query-facts, context packet, evidence read, and retrieval feedback behavior
- worker heartbeat, outbox backlog, processing leases, retries, and dead letters
- PostgreSQL availability, saturation, storage, backups, and restore validation
- pending review, stale vault export, redaction, and retention workflow state

Alerting starts with a small, high-signal set:

- readiness failure
- stale worker heartbeat
- oldest outbox ready job age above threshold
- dead-letter count above zero
- repeated embedding/provider failures
- database backup or restore risk
- agent-contract benchmark smoke failure during release verification

Trace and log attributes must remain payload-safe. Operators can log ids,
counts, statuses, scope types, route names, lifecycle states, and durations, but
must not log raw proposal text, raw query text, review notes, memory body text,
raw event payloads, or embedding input text.

## Consequences

- The next implementation slice can add OpenTelemetry and application metrics
  against a named signal contract.
- Alerting is intentionally narrow for the production pilot, which should reduce
  noise while catching the highest-risk failure modes.
- Retrieval quality becomes part of operational health through retrieval
  feedback and benchmark smoke status, not only through unit or integration
  tests.
- Dashboards and alert rules can be built later without changing the signal
  ownership model.

