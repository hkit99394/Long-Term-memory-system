# 0030 Review Dashboard

## Status

Accepted.

## Context

M7-02 needs a TypeScript review dashboard that can move pending reviews through the human workflow. M7-01 already exposes the authorized pending queue. The dashboard also needs concrete API actions so approve, reject, edit, expire, delete, and supersede controls are not only local UI state.

The repository does not yet have a frontend toolchain, and the MVP should avoid adding a framework or package install just to render the first operational review queue.

## Decision

Add a dependency-free TypeScript dashboard under `tools/ui`, built into API static assets at:

```text
src/MemorySystem.Api/wwwroot/reviews/
```

The API serves the dashboard at `/reviews/`. The dashboard loads `GET /api/reviews/pending`, accepts the local API key in the page, and calls action endpoints:

```text
POST /api/reviews/{id}/approve
POST /api/reviews/{id}/reject
POST /api/reviews/{id}/edit
POST /api/reviews/{id}/expire
POST /api/reviews/{id}/delete
POST /api/reviews/{id}/supersede
```

Each action requires a `sourceEventId` and optional notes. Edit and supersede also require replacement subject, predicate, and object fields. The workflow enforces existing `review` permission and verifies that the action source event belongs to the memory scope.

Lifecycle behavior:

- Approve activates the reviewed memory and indexes it when needed.
- Reject marks the reviewed memory deleted.
- Edit updates the memory content, activates it, and indexes it.
- Expire marks the memory expired.
- Delete marks the memory deleted.
- Supersede marks the reviewed memory superseded and writes a new active replacement memory.

## Consequences

- Reviewers have a first usable UI for the pending review queue.
- Review actions remain provenance-backed through source events.
- The frontend stays framework-free until the dashboard grows enough to justify a larger toolchain.
