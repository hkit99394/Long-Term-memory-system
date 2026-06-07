# Agent Memory Client Wrapper IP-08

Status: implemented for improvement plan item IP-08.

Owner: Developer + CTO.

## Purpose

IP-08 adds a small repo-local wrapper for the required agent memory work loop.
The wrapper makes project work start with memory context and fact retrieval,
then keeps a pending packet until feedback is recorded.

Use it before planning, coding, reviewing, releasing, or answering
project-specific questions:

```bash
scripts/agent-memory-client.sh prework \
  --query "What should I know before working on IP-08?" \
  --scope-type project \
  --scope-id 9f8e7d6c-5b4a-4321-9123-abcdef123002 \
  --role-id developer
```

When the task is complete, record feedback:

```bash
scripts/agent-memory-client.sh feedback --feedback-type missing
```

For item-level feedback, use the returned item refs:

```bash
scripts/agent-memory-client.sh feedback \
  --feedback-type useful \
  --item-index 1
```

## Behavior

`scripts/agent-memory-client.sh prework` always performs both required read
steps before it writes pending state:

- `memory.getContext` through `GET /api/memory/context`
- `memory.queryFacts` through `POST /api/memory/query-facts`

The pending state stores only payload-safe retrieval metadata:

- `packetId`
- target scope type and id
- optional `roleId`
- context item count
- query-facts count
- context item refs with `itemId`, `sourceType`, and `sourceId`

It does not store raw query text. The default state file lives under the local
temporary directory and can be overridden with
`MEMORYSYSTEM_AGENT_MEMORY_STATE_FILE`.

If pending feedback exists, `prework` fails before calling the API. That makes
feedback an explicit closeout step instead of a best-effort habit. Use
`--replace-pending` only when the previous pending packet is intentionally
abandoned.

`scripts/agent-memory-client.sh feedback` records feedback through
`POST /api/memory/context/feedback` using the stored `packetId`. On success, it
clears the pending state. Packet-level `missing` feedback omits item/source
identifiers. Item-level `useful`, `stale`, `wrong`, `sensitive`,
`over_broad`, and `noisy` feedback requires either `--item-index` or explicit
`--item-id`, `--source-type`, and `--source-id`.

## Commands

Show pending state:

```bash
scripts/agent-memory-client.sh status
```

Run prework with optional fact filters:

```bash
scripts/agent-memory-client.sh prework \
  --query "Which release evidence matters for this change?" \
  --memory-type decision \
  --memory-type release_evidence \
  --namespace /project/9f8e7d6c-5b4a-4321-9123-abcdef123002/decisions
```

Record packet-level missing feedback:

```bash
scripts/agent-memory-client.sh feedback --feedback-type missing
```

Record item-level feedback:

```bash
scripts/agent-memory-client.sh feedback \
  --feedback-type stale \
  --item-id context-item-id \
  --source-type memory_fact \
  --source-id memory-fact-id
```

## Configuration

The wrapper reads:

| Variable | Purpose |
| --- | --- |
| `MEMORYSYSTEM_API_BASE_URL` | Optional API base URL override. |
| `MEMORYSYSTEM_API_KEY` | Preferred API key variable. |
| `MEMORYSYSTEM_OPERATOR_API_KEY` | Fallback operator key read from the environment or `.env.production`. |
| `MEMORYSYSTEM_LOCAL_ACCESS_PORT` | Fallback host-local port read from `.env.production`; defaults to `8081`. |
| `MEMORYSYSTEM_CANONICAL_PROJECT_ID` | Optional default project scope id override. |
| `MEMORYSYSTEM_AGENT_MEMORY_ROLE_ID` | Optional default role id. |
| `MEMORYSYSTEM_AGENT_MEMORY_STATE_FILE` | Optional pending state file path. |
| `MEMORYSYSTEM_AGENT_MEMORY_ENV_FILE` | Optional env file path; defaults to `.env.production`. |

The wrapper never prints API keys.

## Completion Evidence

IP-08 is complete when:

- `scripts/agent-memory-client.sh` exposes `prework`, `feedback`, and `status`.
- `prework` calls both `memory.getContext` and `memory.queryFacts`.
- pending feedback blocks another `prework` run unless `--replace-pending` is
  explicit.
- `feedback` records packet-id feedback and clears pending state after success.
- docs and tests cover the wrapper contract.
