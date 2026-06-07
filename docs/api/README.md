# API Contracts

This folder contains curated API contracts for agent-facing and client-facing
use of the memory system.

Run the local API from the root [README](../../README.md) before using the curl
examples. The documented default API key is a local-demo value only; set
`MEMORYSYSTEM_API_KEY` for any non-local API.

## Contracts

| Contract | Purpose |
| --- | --- |
| [agent-memory-v1.openapi.json](agent-memory-v1.openapi.json) | LMSS v1 OpenAPI contract for the existing agent-facing memory workflow. |
| [agent-memory-v1-examples.md](agent-memory-v1-examples.md) | Client examples for the v1 memory workflow. |
| [../agent-memory-client-wrapper-ip08.md](../agent-memory-client-wrapper-ip08.md) | Repo-local wrapper that enforces `memory.getContext`, `memory.queryFacts`, and packet-id feedback around project work. |
| [context-product-v1-caller-guide.md](context-product-v1-caller-guide.md) | CP-10 caller guide for reading context explanations, safe exclusions, feedback actions, review handoff, and raw query hygiene. |
| [context-packet-product-v1.md](context-packet-product-v1.md) | CP-01 productized context packet response contract. |
| [context-packet-product-v1.schema.json](context-packet-product-v1.schema.json) | JSON Schema for the productized context packet response. |
| [policy-targeting-for-agent-callers.md](policy-targeting-for-agent-callers.md) | Caller-facing policy guide for principal, scope, namespace, role, trust, retention, sensitivity, and source evidence fields. |
| [memory-query-facts-implementation-plan.md](memory-query-facts-implementation-plan.md) | LMSS-04 implementation plan for the implemented `memory.queryFacts` endpoint. |

## Examples

The curl workflow lives at:

```text
docs/api/examples/agent-memory-v1-curl.sh
```

Run it from the repository root after starting the API:

```bash
bash docs/api/examples/agent-memory-v1-curl.sh
```

The workflow prints productized context signals and records item feedback using
`packetId`, `itemId`, `sourceType`, and `sourceId` so clients do not need to
resend raw query text.

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

Validate the productized context packet schema as JSON:

```bash
python3 -m json.tool docs/api/context-packet-product-v1.schema.json >/dev/null
```
