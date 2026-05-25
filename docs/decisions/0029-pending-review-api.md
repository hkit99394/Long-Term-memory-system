# 0029 Pending Review API

## Status

Accepted.

## Context

M7-01 starts the human review workflow. The database already has `memory_reviews`, and the access model already has a `review` permission. The first API should expose pending review records without changing the broker's current `review_required` response behavior.

Pending review rows reference memory facts, so the endpoint can list tentative, active, or otherwise reviewable memory records that already exist in the review queue.

## Decision

Add:

```text
GET /api/reviews/pending?limit={1..50}
```

The endpoint returns pending `memory_reviews` joined with their `memory_facts`. It filters every candidate through the existing fine-grained `review` permission check for the memory fact scope and namespace.

Each pending review item includes:

- review id, status, reviewer id, notes, created and updated timestamps
- review source event id and a stable `/api/events/{id}` source link
- nested memory fact fields
- memory fact source event id and a stable `/api/events/{id}` source link

## Consequences

- The review dashboard can start from an authorized pending queue.
- A principal with read access but no review permission receives an empty pending queue for those memories.
- Approval, rejection, edit, expire, delete, and supersede actions remain separate M7 workflow endpoints.
