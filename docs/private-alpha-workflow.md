# Private Alpha Workflow

## Purpose

This workflow is the first private-alpha product path for the long-term memory system. It gives reviewers one repeatable story that proves the system can accept evidence, decide what should be remembered, let a human correct it, retrieve scoped context, and expose operational state.

Use [Scenario 0001](scenarios/0001-user-preference-project-decision-cto-context.md) as the seed story.

## Alpha Outcome

At the end of the workflow, a reviewer should be able to say:

- which source event justified a memory
- why the memory was stored, reviewed, or rejected
- who can retrieve it
- how the context packet used it
- whether the worker and projections are healthy

## Setup

Create the repeatable private-alpha demo database state:

```bash
./scripts/seed-private-alpha-demo.sh
```

The command starts local PostgreSQL if needed, applies migrations, and upserts the Scenario 0001 actors, memberships, role assignments, grants, source events, memory facts, role lenses, chunks, outbox jobs, and deterministic embeddings.

Run the API:

```bash
dotnet run --project src/MemorySystem.Api
```

Run the worker in a second terminal:

```bash
dotnet run --project src/MemorySystem.Worker
```

The worker processes outbox jobs and runs the first retention automation. By default it minimizes unreferenced `ephemeral` source-event payloads after seven days, once on startup and then hourly.

For authenticated API calls, configure an API key mapped to the seeded principal:

```text
11111111-1111-4111-8111-111111111111
```

Example local API configuration:

```bash
Authentication__ApiKey__Keys__local_jack__Key=private-alpha-local-key \
Authentication__ApiKey__Keys__local_jack__PrincipalId=11111111-1111-4111-8111-111111111111 \
Authentication__ApiKey__Keys__local_jack__DisplayName="Jack Tam" \
dotnet run --project src/MemorySystem.Api
```

## Workflow

### 1. Append Source Evidence

Append source events for the user preference, project decision, shared CTO principle, and project CTO lens. The payloads are defined in [Scenario 0001](scenarios/0001-user-preference-project-decision-cto-context.md#source-events).

The seed runner has already inserted those events so reviewers can start from the full scenario immediately. Re-appending through the API is still useful when testing idempotency and write authorization.

Expected result:

- `POST /api/events` returns an event id.
- Retrying with the same idempotency key and identical body returns the same response.
- `GET /api/events/{id}` returns the event only to an authorized principal.

### 2. Propose Durable Memory

Submit memory proposals that reference those source events.

Expected result:

- The Memory Broker returns `stored`, `review_required`, `rejected`, or `session_only`.
- Stored proposals create memory facts, chunks, and outbox jobs transactionally.
- Similar or conflicting active memory routes to review rather than silently duplicating truth.

### 3. Review Human-Governed Memory

Open the review dashboard:

```text
/reviews/
```

Complete at least one pending review with approve, edit, delete, or supersede.

Expected result:

- `GET /api/reviews/pending` only returns reviews the principal can review.
- Review actions require source evidence and idempotency.
- Edits and supersessions update memory while preserving audit history.
- Deletes and redactions invalidate derived chunks, embeddings, and exports.

Open the admin console:

```text
/admin/
```

Expected result:

- Authorized memory facts are visible with lifecycle state, confidence, scope,
  source links, and safe source policy metadata.
- Source payloads are not included in the memory list.
- Opening evidence uses the authorized event-read path.

### 4. Retrieve Context

Request a context packet for the Project A CTO scenario.

Expected result:

- Retrieval ranks only authorized candidate rows.
- The context packet includes source links.
- Project B memory does not appear.
- The packet stays compact enough for an agent prompt.

Record retrieval feedback after a reviewer inspects the packet:

```text
POST /api/memory/context/feedback
```

Use `feedbackType` values `useful`, `stale`, `missing`, or `noisy`. The API stores a hash of the query, not the raw query text.

### 5. Export Human-Readable Memory

Call the Obsidian export endpoint for approved decision or summary memory.

Expected result:

- Exported Markdown includes source ids.
- Deleted, redacted, expired, superseded, or contradicted memory produces stale markers instead of active documents.
- Archive exports include readable inactive memory only when it is safe to show.

### 6. Check Operations

Check the operational summary:

```text
GET /api/operations/summary
```

Expected result:

- API status is visible.
- Latest outbox worker heartbeat is visible.
- Outbox backlog and dead-letter counts are visible.
- Pending review count is visible.
- Stale vault export count is visible.
- Retrieval feedback totals, shares, and per-hour rates are visible for the
  recent operator window.

Also check:

```text
GET /health/live
GET /health/ready
```

## Alpha Acceptance Checklist

- A new reviewer can run the service locally from the top-level README.
- The scenario can be explained without reading code.
- Each durable memory has a source event link.
- The review workflow can correct or remove memory.
- Context retrieval is scoped and source-linked.
- The operator can see whether the worker, outbox, pending reviews, and stale exports need attention.

## Follow-Up After Alpha

Record what helped the agent make a better decision and what did not. The next product iteration should improve the weakest observed point: write precision, review usability, retrieval relevance, stale-memory handling, or operator confidence.
