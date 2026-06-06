# API Workflows

Use the configured base URL and API key:

```text
MEMORYSYSTEM_API_BASE_URL
MEMORYSYSTEM_API_KEY
```

For this repository's host-local production memory system, prefer already
exported canonical variables. If they are absent, inspect the project boundary
and parse `.env.production` without printing secret values:

```bash
port=$(awk -F= '$1=="MEMORYSYSTEM_LOCAL_ACCESS_PORT"{print substr($0,index($0,"=")+1)}' .env.production)
key=$(awk -F= '$1=="MEMORYSYSTEM_OPERATOR_API_KEY"{print substr($0,index($0,"=")+1)}' .env.production)
base="http://127.0.0.1:${port:-8081}"
```

Use `base` as the effective `MEMORYSYSTEM_API_BASE_URL` and `key` as the
effective `MEMORYSYSTEM_API_KEY` for operator workflows. Do not echo either
variable. Do not shell-source `.env.production`; some values can contain
unquoted spaces.

Every authenticated request must include:

```http
X-Api-Key: ${MEMORYSYSTEM_API_KEY}
```

Use `Content-Type: application/json` for JSON `POST` requests.

## Required Before-Work Loop

Before planning, coding, reviewing, or releasing project work:

1. Call `GET /api/memory/context` with the task query, target scope, optional `roleId`, and a practical limit.
2. Use `POST /api/memory/query-facts` when the task depends on decisions, facts, contradictions, source links, lifecycle status, or policy metadata.
3. Keep the returned `packetId`, item `itemId`, `sourceType`, and `sourceId` for feedback.
4. After the task, record `useful`, `stale`, `wrong`, `sensitive`, `over_broad`, or `missing` feedback with `POST /api/memory/context/feedback`.

If retrieval fails and the task must continue, state the retrieval failure and avoid claiming stored memory support.

## Health

```bash
curl -sS "$MEMORYSYSTEM_API_BASE_URL/health/ready"
```

## Retrieve Context

Use for answer preparation when relevant durable memory may exist:

```bash
curl -sS --get "$MEMORYSYSTEM_API_BASE_URL/api/memory/context" \
  -H "X-Api-Key: $MEMORYSYSTEM_API_KEY" \
  --data-urlencode "q=What should I know before answering?" \
  --data-urlencode "scopeType=project" \
  --data-urlencode "scopeId=<project-id>" \
  --data-urlencode "roleId=cto" \
  --data-urlencode "limit=12"
```

Returned context items can include explanation, source evidence, lifecycle fit, policy fit, and review-suggested actions. Prefer those structured fields over free-form inference.

## Query Facts

Use for structured facts, contradictions, evidence links, or policy metadata:

```bash
curl -sS -X POST "$MEMORYSYSTEM_API_BASE_URL/api/memory/query-facts" \
  -H "X-Api-Key: $MEMORYSYSTEM_API_KEY" \
  -H "Content-Type: application/json" \
  --data '{
    "query": "What should I know before answering?",
    "targetScope": {
      "scopeType": "project",
      "scopeId": "<project-id>"
    },
    "roleId": "cto",
    "includeContradictions": true,
    "includeExcluded": true,
    "limit": 8
  }'
```

Use `namespaces` and `memoryTypes` only when they narrow the request without hiding relevant facts.

## Append Source Evidence

Append evidence before proposing durable memory:

```bash
curl -sS -X POST "$MEMORYSYSTEM_API_BASE_URL/api/events" \
  -H "X-Api-Key: $MEMORYSYSTEM_API_KEY" \
  -H "Idempotency-Key: <stable-event-key>" \
  -H "Content-Type: application/json" \
  --data '{
    "principalId": "<principal-id>",
    "eventType": "user_message",
    "scopeType": "user",
    "scopeId": "<principal-id>",
    "trustLevel": "user_scoped",
    "retentionClass": "standard",
    "sensitivity": "none",
    "payload": {
      "text": "The user preference or source evidence to preserve."
    }
  }'
```

Use an exact replay with the same `Idempotency-Key` and body for retries.

## Propose Durable Memory

Use the returned `sourceEventId` from the append call:

```bash
curl -sS -X POST "$MEMORYSYSTEM_API_BASE_URL/api/memory/proposals" \
  -H "X-Api-Key: $MEMORYSYSTEM_API_KEY" \
  -H "Idempotency-Key: <stable-proposal-key>" \
  -H "Content-Type: application/json" \
  --data '{
    "sourceEventId": "<event-id>",
    "memoryType": "preference",
    "scopeType": "user",
    "scopeId": "<principal-id>",
    "namespace": "/user/<principal-id>/preferences",
    "visibility": "private",
    "subject": "answer style",
    "predicate": "prefers",
    "object": "short, direct production-operation guidance",
    "confidence": 0.9,
    "trustLevel": "user_scoped",
    "sensitivity": "none"
  }'
```

The broker may store, reject, or route the proposal for review. Do not assume storage succeeded unless the response says so.

## Record Feedback

For item-level feedback, keep `packetId`, `itemId`, `sourceType`, and `sourceId` from the context packet:

```bash
curl -sS -X POST "$MEMORYSYSTEM_API_BASE_URL/api/memory/context/feedback" \
  -H "X-Api-Key: $MEMORYSYSTEM_API_KEY" \
  -H "Content-Type: application/json" \
  --data '{
    "packetId": "<packet-id>",
    "itemId": "<item-id>",
    "targetScopeType": "project",
    "targetScopeId": "<project-id>",
    "roleId": "cto",
    "sourceType": "memory_fact",
    "sourceId": "<source-id>",
    "feedbackType": "useful"
  }'
```

For missing-memory feedback, omit item and source identifiers:

```json
{
  "packetId": "<packet-id>",
  "targetScopeType": "project",
  "targetScopeId": "<project-id>",
  "roleId": "cto",
  "feedbackType": "missing"
}
```

## Admin And Reviews

Host-local browser paths:

```text
http://127.0.0.1:8081/admin/
http://127.0.0.1:8081/reviews/
```

In each page, set:

```text
API: http://127.0.0.1:8081
Key: MEMORYSYSTEM_API_KEY or a configured operator key
```
