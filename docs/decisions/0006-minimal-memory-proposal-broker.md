# 0006 Minimal Memory Proposal Broker

## Status

Accepted.

## Context

M2 needs `POST /api/memory/proposals` to return an idempotent broker decision before the full transactional memory write lands in M2-05.

The full Memory Broker will later classify candidates, deduplicate, detect contradictions, enforce write permissions, create facts and chunks, enqueue outbox jobs, and manage review workflows. M2-04 should prove the API contract and decision shape without pretending those later behaviors exist.

## Decision

`POST /api/memory/proposals` accepts a proposed memory candidate with:

- `sourceEventId`
- `memoryType`
- `scopeType`
- `scopeId`
- `namespace`
- `visibility`
- `subject`
- `predicate`
- `object`
- `confidence`
- `trustLevel`
- `sensitivity`

The endpoint requires `Idempotency-Key` and returns:

```json
{
  "decision": "stored",
  "reason": "The proposal is accepted for durable storage.",
  "memoryId": null,
  "sourceEventId": "66666666-6666-4666-8666-666666666666"
}
```

For M2-04, `memoryId` remains `null` even for `stored`. The decision means the candidate is accepted for durable storage. M2-05 owns the actual transaction that creates the memory fact, chunk, outbox job, and stored broker response with a real memory id.

## Minimal Rules

- Missing or unknown `sourceEventId` returns `rejected`.
- `session_instruction`, session scope, or `/session/...` namespace returns `session_only`.
- Unsupported memory types return `rejected`.
- Blank `subject`, `predicate`, or `object` returns `rejected`.
- Web or retrieved-untrusted content cannot write global, organization, or policy memory and returns `rejected`.
- Missing confidence, confidence below `0.700`, or `secret`/`regulated` sensitivity returns `review_required`.
- Supported durable proposals with source evidence, sufficient confidence, and non-sensitive content return `stored`.

## Consequences

- The proposal endpoint is retry-safe and returns stable decisions.
- The M2 broker decision is deterministic and intentionally conservative.
- M2-05 can reuse the same endpoint boundary and replace the accepted `stored` path with a transactional memory write.
- M3 still needs write-permission enforcement before scoped durable writes are authorization-complete.
