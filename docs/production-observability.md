# Production Observability and Alerting

Last updated: 2026-05-30

## Purpose

This document defines the production-pilot observability and alerting contract
for the Long-Term Memory System. It describes the minimum signals an operator
needs to tell whether the migrator, API, worker, PostgreSQL, embedding provider,
review workflow, vault export workflow, and retrieval quality loop are healthy.

This is a pilot contract, not a vendor-specific dashboard spec. The same signals
can be implemented with OpenTelemetry, platform metrics, log queries, managed
database metrics, or a combination of those tools.

## Current Coverage

The current service already exposes:

- `/health/live` for process liveness.
- `/health/ready` for PostgreSQL, outbox backlog, worker heartbeat, and
  embedding-provider readiness.
- `/health` for aggregate health details.
- `/api/operations/summary` for authenticated operator status, including API
  reachability, worker heartbeat, outbox state, pending reviews, stale vault
  exports, and recent retrieval feedback counts.
- `/api/operations/metrics` for authenticated Prometheus-compatible pilot
  metrics covering API request health, readiness, outbox backlog, worker
  heartbeat, retrieval feedback, review/vault counts, and embedding-index
  failures.
- Structured operational logs for proposal, retrieval, review, and redaction
  decision points without logging raw proposal text, query text, review notes,
  memory body text, or raw event payloads.
- `scripts/operations-metrics-smoke.sh` for a local smoke check that verifies
  the first alert inputs are observable from a running API.

The current service does not yet ship distributed tracing, production
dashboards, alert rules as code, or a platform-specific metrics exporter.

## Signal Ownership

| Area | Primary owner | Source of truth | First check |
| --- | --- | --- | --- |
| API availability | API process | Health endpoint and ingress metrics | `/health/live` |
| API readiness | API process and dependencies | Health endpoint | `/health/ready` |
| Database health | PostgreSQL | Managed PostgreSQL metrics and health check | `postgres` health check |
| Outbox processing | Worker and PostgreSQL | `outbox_jobs`, worker heartbeat, operations summary | `/api/operations/summary` |
| Embedding provider | Worker and provider | Readiness check, worker errors, provider metrics | `embedding_provider` health check |
| Review workflow | PostgreSQL | `memory_reviews` and operations summary | Pending review count |
| Vault export workflow | PostgreSQL | `vault_exports` and operations summary | Stale export count |
| Retrieval quality | API and PostgreSQL | Retrieval feedback records and benchmark runs | Retrieval feedback summary |

## Required Metrics

### API

Minimum API metrics:

- request count by route, method, status code, and environment
- request duration p50, p95, and p99 by route
- 5xx rate by route
- 4xx rate by route, with authentication failures separated from validation
  failures when possible
- idempotency conflict count
- problem-details count by title
- `/health/live` status
- `/health/ready` status and failing check name

Initial alert signals:

- readiness failing for 5 minutes
- any route with sustained 5xx rate above 1 percent for 10 minutes
- p95 latency above the pilot threshold for 15 minutes
- repeated authentication failures above the expected caller baseline

### Memory Write Path

Minimum write-path metrics:

- event append count and failure count
- memory proposal count by decision: `stored`, `review_required`, `rejected`,
  and `session_only`
- idempotent replay count
- review-required rate
- broker validation failure count
- source event read failure count

Initial alert signals:

- stored proposal failures above zero for 10 minutes
- sudden drop to zero write traffic during an expected active window
- review-required rate materially above baseline after a release
- idempotency conflicts above baseline after a client rollout

### Retrieval and Agent Contract

Minimum retrieval metrics:

- context packet request count, latency, and result count
- query-facts request count, latency, fact count, contradiction count, exclusion
  count, warning count, and overall confidence distribution
- source evidence read count and failure count
- retrieval feedback count by type: `useful`, `stale`, `missing`, `noisy`
- retrieval feedback share by type over the recent operator window
- benchmark smoke result for the LMSS agent-contract suite

Initial alert signals:

- query-facts or context packet 5xx rate above 1 percent for 10 minutes
- source evidence read failures above zero for known-good source links
- `missing`, `stale`, or `noisy` retrieval feedback share materially above
  baseline
- agent-contract smoke fails in a release verification run

### Worker and Outbox

Minimum worker metrics:

- worker heartbeat age and status
- outbox ready pending count
- oldest ready pending job age
- delayed pending count
- processing count
- retrying failed count
- expired processing lease count
- dead-letter count
- job processing duration by job type
- embedding job failures by provider error class when available

Initial alert signals:

- worker heartbeat stale for more than 2 minutes in production-pilot
- oldest ready pending job age above 5 minutes
- dead-letter count above zero
- expired processing lease count above zero
- repeated embedding failures for 10 minutes

### PostgreSQL

Minimum database metrics:

- connection count and saturation
- CPU, memory, storage, and I/O pressure
- transaction rate
- lock waits and long-running queries
- backup success and backup age
- point-in-time recovery status when supported
- replication lag when replicas exist
- extension availability for `vector`

Initial alert signals:

- managed PostgreSQL availability event
- storage usage above 80 percent
- connection saturation above 80 percent for 10 minutes
- backup missing or older than the recovery policy
- lock waits or long-running queries that affect API readiness

### Governance and Human Workflow

Minimum governance metrics:

- pending review count and oldest pending review age
- stale vault export count
- redaction action count
- delete, expire, supersede, approve, reject, and edit review action counts
- retention minimization count and failure count

Initial alert signals:

- oldest pending review age exceeds the product policy threshold
- stale vault export count remains nonzero after export repair workflow
- retention minimization fails repeatedly
- redaction or delete workflow fails after an operator action

## Tracing Contract

When distributed tracing is added, the pilot should include spans for:

- API request entry and response
- API idempotency begin and complete
- event append workflow
- memory proposal broker decision
- memory proposal transactional write
- fact finding query
- context packet build
- source event read
- review action workflow
- vault export query
- outbox lease
- outbox job handler
- embedding provider call
- retention minimization batch

Trace attributes must avoid raw memory content, raw query text, raw event
payloads, proposal notes, review notes, and embedding input text. Prefer ids,
route names, counts, status values, scope type, role id, memory type, and hashed
query values.

## Log Contract

Production logs should keep the current payload-safe rule:

- log ids, counts, route names, statuses, normalized decision labels, lifecycle
  states, and durations
- do not log proposal content, raw query text, review notes, memory body text,
  raw event payloads, or embedding input text
- include correlation ids or trace ids once tracing is available
- include principal id only where it is already required for operator audit, and
  avoid logging API key values

## Alert Runbook

### Readiness Failure

First checks:

1. Open `/health/ready` and identify the failing check.
2. Check `/api/operations/summary` if authentication is still working.
3. Check recent deployment, migration, and secret rotation records.
4. Follow the failing dependency path: PostgreSQL, worker heartbeat, outbox, or
   embedding provider.

Initial actions:

- roll back API or worker code if the failure started with a deployment
- restore or rotate secrets if startup validation or provider credentials fail
- pause worker rollout if the failure is outbox or embedding related

### Worker Heartbeat Stale

First checks:

1. Confirm worker process count in the deployment platform.
2. Check worker logs for startup validation, embedding provider, or database
   connection errors.
3. Check outbox ready pending count and oldest ready pending age.
4. Confirm the worker uses the same PostgreSQL secret as the API.

Initial actions:

- restart one worker instance
- keep API running if read paths are healthy
- scale workers only after confirming leases are not stuck

### Outbox Backlog or Dead Letter

First checks:

1. Check `/api/operations/summary`.
2. Inspect dead-letter job type, aggregate type, attempts, and last error.
3. Check embedding provider health if jobs are memory-index jobs.
4. Compare the issue with the latest deployment or provider rotation.

Initial actions:

- pause new worker rollout if failures started after deployment
- fix provider or secret issues before retrying jobs
- use a forward code fix for unsupported job payloads

### Retrieval Quality Regression

First checks:

1. Compare retrieval feedback type shares with the previous baseline.
2. Run the LMSS agent-contract benchmark smoke.
3. Inspect recent changes to ranking, query-facts, access predicates, or seeded
   benchmark fixtures.
4. Check source-link coverage and contradiction/exclusion counts.

Initial actions:

- roll back ranking or retrieval code if benchmark smoke fails after release
- preserve feedback records for analysis
- add a benchmark fixture before changing ranking behavior again

### Database Backup or Restore Risk

First checks:

1. Confirm latest backup age and provider backup status.
2. Run restore validation in a separate database if the alert is persistent.
3. Check storage pressure and replication or point-in-time recovery status.

Initial actions:

- delay migrations until backup status is known
- restore to a new validation database before any production restore decision
- record backup id, operator, validation result, and data-loss window

## Dashboard Minimum

The first production-pilot dashboard should show:

- API request rate, p95 latency, and 5xx rate
- readiness status by check
- worker heartbeat age and status
- outbox ready pending, oldest ready age, retrying failed, expired processing,
  and dead-letter counts
- pending review count and oldest age
- stale vault export count
- retrieval feedback by type over the last 24 hours
- embedding provider failures
- latest benchmark smoke status
- latest backup age and restore validation status

## Implementation Notes

MR-06 adds the first in-process metrics export path:

- `GET /api/operations/metrics` requires API-key authentication and returns
  Prometheus text format.
- Request counters and duration sums are recorded by method, route, and status
  code for API routes.
- Readiness status is exported from the same health checks used by
  `/health/ready`.
- Worker and outbox metrics are read from PostgreSQL, including heartbeat age,
  stale state, ready backlog, oldest ready job age, dead letters, retrying
  failures, and expired processing leases.
- Retrieval feedback counts, shares, and rates use the same recent operator
  window as `/api/operations/summary`.
- Embedding failure metrics are memory-index outbox failure signals until a
  provider-specific error taxonomy exists.

Recommended follow-on order:

1. Add OpenTelemetry package wiring for ASP.NET Core, Npgsql, and runtime
   metrics.
2. Add application counters and histograms for memory proposals, context
   packets, query-facts, source reads, review actions, outbox jobs, and
   retrieval feedback.
3. Add a platform-specific exporter only after metrics names are stable.
4. Add alert definitions for readiness, worker heartbeat, outbox age, dead
   letters, embedding failures, and benchmark smoke failure.
5. Add dashboard panels using the same metrics.

## Pilot Acceptance

MR-05 is complete when:

- production-pilot metrics are named by subsystem
- alert thresholds and first-response actions are documented
- dashboard minimum panels are defined
- privacy-safe logging and trace attributes are documented
- the next implementation slice can add metrics/exporter code without debating
  what to measure first

MR-06 is complete when:

- `/api/operations/metrics` returns authenticated Prometheus-compatible metrics
- request health, readiness, outbox, worker heartbeat, retrieval feedback, and
  embedding-index failure signals are exported
- `scripts/operations-metrics-smoke.sh` verifies the key alert inputs locally
