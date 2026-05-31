# Agent Memory v1 Client Examples

These examples show the LMSS v1 workflow from a client or agent-tool point of
view. They use the curated [OpenAPI contract](agent-memory-v1.openapi.json) and
the private-alpha Scenario 0001 local API setup. For field-level scope,
namespace, role, trust, retention, sensitivity, and evidence rules, see
[Policy Targeting For Agent Callers](policy-targeting-for-agent-callers.md).

## Prerequisites

Seed the private-alpha demo data:

```bash
./scripts/seed-private-alpha-demo.sh
```

Run the API with the seeded local principal:

```bash
Authentication__ApiKey__Keys__local_jack__Key=private-alpha-local-key \
Authentication__ApiKey__Keys__local_jack__PrincipalId=11111111-1111-4111-8111-111111111111 \
Authentication__ApiKey__Keys__local_jack__DisplayName="Jack Tam" \
dotnet run --project src/MemorySystem.Api --urls http://127.0.0.1:5099
```

## Runnable Curl Workflow

Run the end-to-end curl example:

```bash
bash docs/api/examples/agent-memory-v1-curl.sh
```

The script uses only `bash`, `curl`, and `python3`. It performs the v1 memory
workflow:

1. Append source evidence with `memory.appendEvent`.
2. Retry the same event append with the same `Idempotency-Key`.
3. Propose durable user-scoped memory with `memory.propose`.
4. Retry the same proposal with the same `Idempotency-Key`.
5. Read the source evidence with `memory.readEvidence`.
6. Read the stored memory fact with `memory.readFact` when the broker stores it.
7. Retrieve Project A CTO context with `memory.getContext`.
8. Query Project A facts with `memory.queryFacts`.
9. Record context feedback with `memory.recordContextFeedback`.

The feedback request can include the returned `packetId` and `itemId` instead
of resending the raw query. When clients do send the query for compatibility,
the server stores only `queryHash`.

## Environment Variables

| Variable | Default | Purpose |
| --- | --- | --- |
| `MEMORYSYSTEM_API_BASE_URL` | `http://127.0.0.1:5099` | API base URL. |
| `MEMORYSYSTEM_API_KEY` | `private-alpha-local-key` | Seeded local API key. |
| `MEMORYSYSTEM_PRINCIPAL_ID` | `11111111-1111-4111-8111-111111111111` | Seeded principal. |
| `MEMORYSYSTEM_PROJECT_A_ID` | `33333333-3333-4333-8333-333333333333` | Seeded Project A scope id. |
| `MEMORYSYSTEM_EXAMPLE_RUN_ID` | `lmss03-v1` | Prefix for stable idempotency keys. |

Use a new `MEMORYSYSTEM_EXAMPLE_RUN_ID` when intentionally creating a new set of
example evidence and memory. Reuse the same value when testing idempotent
replay.

## Minimal Curl Snippets

Append source evidence:

```bash
curl -sS -X POST "$MEMORYSYSTEM_API_BASE_URL/api/events" \
  -H "X-Api-Key: $MEMORYSYSTEM_API_KEY" \
  -H "Idempotency-Key: lmss03-event" \
  -H "Content-Type: application/json" \
  --data '{
    "principalId": "11111111-1111-4111-8111-111111111111",
    "eventType": "user_message",
    "scopeType": "user",
    "scopeId": "11111111-1111-4111-8111-111111111111",
    "trustLevel": "user_scoped",
    "retentionClass": "standard",
    "sensitivity": "none",
    "payload": {
      "text": "When showing memory-derived claims, preserve source ids."
    }
  }'
```

Propose memory from that event:

```bash
curl -sS -X POST "$MEMORYSYSTEM_API_BASE_URL/api/memory/proposals" \
  -H "X-Api-Key: $MEMORYSYSTEM_API_KEY" \
  -H "Idempotency-Key: lmss03-proposal" \
  -H "Content-Type: application/json" \
  --data '{
    "sourceEventId": "<event-id>",
    "memoryType": "preference",
    "scopeType": "user",
    "scopeId": "11111111-1111-4111-8111-111111111111",
    "namespace": "/user/11111111-1111-4111-8111-111111111111/preferences",
    "visibility": "private",
    "subject": "api client examples",
    "predicate": "should",
    "object": "preserve source ids when displaying memory-derived claims",
    "confidence": 0.9,
    "trustLevel": "user_scoped",
    "sensitivity": "none"
  }'
```

Retrieve scoped context:

```bash
curl -sS --get "$MEMORYSYSTEM_API_BASE_URL/api/memory/context" \
  -H "X-Api-Key: $MEMORYSYSTEM_API_KEY" \
  --data-urlencode "q=How should I write the next API client example for Project A?" \
  --data-urlencode "scopeType=project" \
  --data-urlencode "scopeId=33333333-3333-4333-8333-333333333333" \
  --data-urlencode "roleId=cto" \
  --data-urlencode "limit=12"
```

Each returned context item includes a structured `explanation` with
`primaryReason`, `matchedSignals`, `policyFit`, `lifecycleFit`,
`sourceEvidence`, and `reviewSuggestedActions`. Agents should prefer these
bounded fields over parsing free-form summary text when deciding how to cite,
review, or ignore memory-derived context.

Query facts with source links and policy metadata:

```bash
curl -sS -X POST "$MEMORYSYSTEM_API_BASE_URL/api/memory/query-facts" \
  -H "X-Api-Key: $MEMORYSYSTEM_API_KEY" \
  -H "Content-Type: application/json" \
  --data '{
    "query": "What migration strategy is accepted for Project A?",
    "targetScope": {
      "scopeType": "project",
      "scopeId": "33333333-3333-4333-8333-333333333333"
    },
    "roleId": "cto",
    "namespaces": ["/project/33333333-3333-4333-8333-333333333333/decisions"],
    "memoryTypes": ["decision"],
    "includeContradictions": true,
    "includeExcluded": true,
    "limit": 8
  }'
```

Record useful feedback for one returned item:

```bash
curl -sS -X POST "$MEMORYSYSTEM_API_BASE_URL/api/memory/context/feedback" \
  -H "X-Api-Key: $MEMORYSYSTEM_API_KEY" \
  -H "Content-Type: application/json" \
  --data '{
    "packetId": "<packet-id>",
    "itemId": "<item-id>",
    "targetScopeType": "project",
    "targetScopeId": "33333333-3333-4333-8333-333333333333",
    "roleId": "cto",
    "feedbackType": "useful"
  }'
```

## Retry Behavior

Use the same `Idempotency-Key` and exact same request body to safely retry:

- `POST /api/events`
- `POST /api/memory/proposals`

Reusing the same key with a different body returns an idempotency conflict.

`POST /api/memory/context/feedback` is append-only today. It accepts either the
original query or a `packetId` from `memory.getContext`, and stores only a hash.
It intentionally has no `Idempotency-Key` in the v1 contract, so clients should
avoid blind retries unless duplicate observations are acceptable.

`POST /api/memory/query-facts` is a read operation. It also has no
`Idempotency-Key`; callers can retry it like any other read.
