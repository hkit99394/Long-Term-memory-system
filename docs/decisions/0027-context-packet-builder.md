# 0027 Context Packet Builder

## Status

Accepted.

## Context

M6-05 turns authorized hybrid search results into the first context packet shape for agent reads. M6-04 already centralizes ranking and returns score components, so the packet builder should consume that path instead of duplicating authorization, retrieval, or ranking policy.

The packet must stay compact, carry source links, and explain why each memory was selected.

## Decision

Add a context packet endpoint:

```text
GET /api/memory/context?q={query}&limit={1..12}&scopeType={optional}&scopeId={optional}&roleId={optional}
```

The endpoint builds packets through an application-level `IContextPacketBuilder`. The builder calls authorized hybrid search, caps packet size at 12 memories, compacts each memory content string, groups memories into user preferences, project memory, role memory, and relevant decisions, and emits unique source event links.

Each memory item includes:

- source id, source type, namespace, scope, source event id, and source link
- compact content
- final rank
- rank components for relevance, confidence, recency, authority, and scope match
- a short explanation summary

Role-lens items include the base memory fact id when available.

## Consequences

- Context packets are permission-aware by construction because the builder only consumes authorized hybrid search results.
- Packets are source-linked through stable `/api/events/{sourceEventId}` links even before a dedicated event-read endpoint exists.
- Ranking remains explainable because packet items preserve the M6-04 component breakdown.
- M6-06 retrieval evaluation tests can target the packet surface directly.
