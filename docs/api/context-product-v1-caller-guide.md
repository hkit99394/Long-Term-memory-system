# Context Product v1 Caller Guide

Date: 2026-05-31

Status: CP-10 caller guidance

Related contracts:

- [Agent Memory OpenAPI v1](agent-memory-v1.openapi.json)
- [Context Packet Product v1 Contract](context-packet-product-v1.md)
- [Policy Targeting For Agent Callers](policy-targeting-for-agent-callers.md)

## Purpose

This guide shows how an agent or client should use productized context packets:

- read structured inclusion explanations instead of guessing why memory was
  included
- handle safe exclusion summaries without inferring hidden memory content
- submit reviewer feedback actions with packet, item, and source identifiers
- avoid storing or replaying raw query text when `packetId` is available

## Retrieve Context

```bash
curl -sS --get "$MEMORYSYSTEM_API_BASE_URL/api/memory/context" \
  -H "X-Api-Key: $MEMORYSYSTEM_API_KEY" \
  --data-urlencode "q=Project A CTO guidance migration risk delivery sequencing" \
  --data-urlencode "scopeType=project" \
  --data-urlencode "scopeId=33333333-3333-4333-8333-333333333333" \
  --data-urlencode "roleId=cto" \
  --data-urlencode "limit=12"
```

The response is grouped into:

- `userPreferences`
- `projectMemory`
- `roleMemory`
- `relevantDecisions`
- `excluded`
- `sourceEvents`

Each returned item has an `itemId`, `sourceType`, `sourceId`, `sourceEventId`,
optional `sourceLink`, and structured `explanation`.

## Read Explanations

Agents should use bounded explanation fields when deciding how to use memory:

| Field | Caller behavior |
| --- | --- |
| `explanation.primaryReason` | Use as the short reason the item was included, such as `role_match`, `recent_decision`, or `scope_match`. |
| `explanation.matchedSignals` | Use to understand which retrieval signals applied. Do not parse free-form content to infer policy. |
| `explanation.components.feedbackAdjustment` | Treat nonzero values as evidence that prior feedback affected ranking. |
| `explanation.policyFit` | Use as policy metadata. Do not override it client-side. |
| `explanation.lifecycleFit` | Prefer active/current evidence; treat anything else as audit or correction context. |
| `explanation.sourceEvidence` | Preserve source links or source event ids when making memory-derived claims. |
| `explanation.reviewSuggestedActions` | Pick the closest feedback action when the item is useful, stale, wrong, sensitive, or over-broad. |

Minimal parser:

```python
groups = ["userPreferences", "projectMemory", "roleMemory", "relevantDecisions"]
items = [item for group in groups for item in packet.get(group, [])]

for item in items:
    explanation = item["explanation"]
    source = explanation["sourceEvidence"]
    print({
        "itemId": item["itemId"],
        "sourceType": item["sourceType"],
        "sourceId": item["sourceId"],
        "primaryReason": explanation["primaryReason"],
        "matchedSignals": explanation["matchedSignals"],
        "sourceLinked": source["sourceLinked"],
        "reviewSuggestedActions": explanation["reviewSuggestedActions"]
    })
```

## Handle Exclusions

`excluded` is a payload-safe summary of omitted candidates. It is not a list of
hidden facts.

| `reason` | Caller behavior |
| --- | --- |
| `inactive` | Treat disclosed counts as stale or non-current memory metadata. Do not use inactive content as current truth. |
| `not_authorized` | Treat as an authorization boundary. Do not infer source ids, namespaces, projects, or content. |
| `scope_mismatch` | Consider whether the caller chose the right `scopeType` and `scopeId`. |
| `role_mismatch` | Consider whether the requested `roleId` matches the task role. Withheld counts are intentional. |
| `below_rank_cutoff` | Consider increasing `limit` only when the task genuinely needs more context. |
| `source_unavailable` | Ask for review if source evidence should exist. |
| `sensitive` | Treat as a policy boundary; do not ask the model to reconstruct the hidden content. |

Count rules:

- `countDisclosure: "disclosed"` means `count` can be shown to the caller.
- `countDisclosure: "withheld"` means `count` is intentionally `null`.
- Withheld counts must not be converted into guesses such as "at least one
  hidden memory exists".

Minimal parser:

```python
for exclusion in packet.get("excluded", []):
    if exclusion["countDisclosure"] == "withheld":
        print(f"{exclusion['reason']}: count withheld")
    else:
        print(f"{exclusion['reason']}: {exclusion['count']} omitted item(s)")
```

## Submit Feedback Actions

Use `POST /api/memory/context/feedback` for the action chosen from an item or
packet.

| Feedback type | Level | Required identifiers | Typical use |
| --- | --- | --- | --- |
| `useful` | Item | `packetId`, `itemId`, `sourceType`, `sourceId` | The memory helped the answer. |
| `stale` | Item | `packetId`, `itemId`, `sourceType`, `sourceId` | The memory may be outdated. |
| `wrong` | Item | `packetId`, `itemId`, `sourceType`, `sourceId` | The memory conflicts with trusted evidence. |
| `sensitive` | Item | `packetId`, `itemId`, `sourceType`, `sourceId` | The memory should not appear in normal context. |
| `over_broad` | Item | `packetId`, `itemId`, `sourceType`, `sourceId` | The memory is valid but too broadly applied. |
| `missing` | Packet | `packetId` plus target scope and role metadata | Important memory was absent. Omit item and source identifiers. |
| `noisy` | Item | `packetId`, `itemId`, `sourceType`, `sourceId` | Legacy compatibility only; prefer `over_broad`. |

Item-level feedback:

```bash
curl -sS -X POST "$MEMORYSYSTEM_API_BASE_URL/api/memory/context/feedback" \
  -H "X-Api-Key: $MEMORYSYSTEM_API_KEY" \
  -H "Content-Type: application/json" \
  --data '{
    "packetId": "context-packet-id",
    "itemId": "context-item-id",
    "targetScopeType": "project",
    "targetScopeId": "33333333-3333-4333-8333-333333333333",
    "roleId": "cto",
    "sourceType": "memory_fact",
    "sourceId": "source-memory-id",
    "feedbackType": "stale"
  }'
```

Packet-level missing feedback:

```bash
curl -sS -X POST "$MEMORYSYSTEM_API_BASE_URL/api/memory/context/feedback" \
  -H "X-Api-Key: $MEMORYSYSTEM_API_KEY" \
  -H "Content-Type: application/json" \
  --data '{
    "packetId": "context-packet-id",
    "targetScopeType": "project",
    "targetScopeId": "33333333-3333-4333-8333-333333333333",
    "roleId": "cto",
    "feedbackType": "missing"
  }'
```

The feedback response returns `queryHash`. It does not return raw query text.

## Review Handoff

Agent clients record feedback. Operators can then inspect reviewable context
observations through the review API:

```bash
curl -sS --get "$MEMORYSYSTEM_API_BASE_URL/api/reviews/context-observations" \
  -H "X-Api-Key: $MEMORYSYSTEM_API_KEY" \
  --data-urlencode "feedbackType=stale" \
  --data-urlencode "limit=25"
```

For `stale`, `wrong`, and `sensitive` observations, an operator can open or
reuse a pending memory review:

```bash
curl -sS -X POST "$MEMORYSYSTEM_API_BASE_URL/api/reviews/context-observations/$FEEDBACK_ID/review" \
  -H "X-Api-Key: $MEMORYSYSTEM_API_KEY" \
  -H "Idempotency-Key: review-context-feedback-$FEEDBACK_ID" \
  -H "Content-Type: application/json" \
  --data '{
    "notes": "Context feedback says this memory may be stale."
  }'
```

The observation and review responses expose query hashes, packet ids, item ids,
source ids, and source links. They do not expose raw query text.

## Raw Query Hygiene

Caller rules:

- Prefer `packetId` over `query` when recording feedback.
- When `query` is omitted, `packetId` must come from a context packet returned
  to the same caller so the server can reuse the packet query hash.
- Do not store `packet.query` or `currentTask.query` in durable client logs.
- Do not place raw query text in review notes.
- Do not use `Idempotency-Key` with context feedback; the endpoint is
  append-only today.
- If a retry may create duplicate feedback, retry only when duplicate
  observations are acceptable.

## Runnable Example

The end-to-end curl workflow now prints context-product caller signals and
records item feedback with packet, item, source type, and source id:

```bash
bash docs/api/examples/agent-memory-v1-curl.sh
```
