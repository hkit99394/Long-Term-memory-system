# API Contracts

This folder contains curated API contracts for agent-facing and client-facing
use of the memory system.

## Contracts

| Contract | Purpose |
| --- | --- |
| [agent-memory-v1.openapi.json](agent-memory-v1.openapi.json) | LMSS v1 OpenAPI contract for the existing agent-facing memory workflow. |

## Authentication

All documented LMSS v1 operations require API key authentication through the
`X-Api-Key` header.

## Idempotency

Current retry-safe durable write endpoints require the `Idempotency-Key` header:

- `POST /api/events`
- `POST /api/memory/proposals`

`POST /api/memory/context/feedback` records append-only retrieval observations
and is not currently idempotent. Callers should avoid blind retries for feedback
unless they can tolerate duplicate observations.

## Validation

Validate the OpenAPI document as JSON:

```bash
python3 -m json.tool docs/api/agent-memory-v1.openapi.json >/dev/null
```
