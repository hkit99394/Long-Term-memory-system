# 0007 Transactional Memory Proposal Write

## Status

Accepted.

## Context

M2-04 proved the `POST /api/memory/proposals` endpoint and deterministic broker decision rules. M2-05 needs the `stored` decision to become a durable write, while keeping retries stable through the existing API idempotency table.

The write must not leave partial state. A stored proposal needs its memory fact, searchable chunk, indexing outbox job, and idempotent API response to succeed or fail together.

## Decision

For broker decisions of `stored`, the proposal endpoint writes these records in one PostgreSQL transaction:

- `memory_facts`, with the proposal's structured subject, predicate, object, scope, namespace, source event, proposer, confidence, trust level, and active status.
- `memory_chunks`, linked to the memory fact and source event, with matching scope and namespace metadata.
- `outbox_jobs`, using `memory.index` so later workers can index or embed the new memory.
- `api_idempotency_keys`, completed with the stored broker response, `resource_type = 'memory_fact'`, and the created memory id.

The endpoint still runs the deterministic broker first. Non-stored decisions continue to complete through the generic idempotency path and do not write memory facts, chunks, or outbox jobs.

## Consequences

- A retry with the same idempotency key and request body replays the original stored response and memory id.
- Stored proposals now return a real `memoryId`.
- The database remains the transaction boundary for M2 writes; no in-memory broker state is authoritative.
- M3 still needs authorization-complete write permission checks before all scoped writes are production-ready.
