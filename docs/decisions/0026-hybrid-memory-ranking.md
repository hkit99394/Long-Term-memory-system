# 0026 Hybrid Memory Ranking

## Status

Accepted.

## Context

M6-04 combines keyword and semantic retrieval into a single ranking surface. M6-01 and M6-03 already prove that full-text and vector search run inside authorization predicates; hybrid ranking must preserve that rule while adding non-relevance signals used by the MVP context builder.

The long-term plan defines the MVP score shape:

```text
final_score =
  relevance * 0.40
+ confidence * 0.25
+ recency * 0.15
+ authority * 0.15
+ scope_match * 0.05
```

## Decision

Add a PostgreSQL-backed hybrid search endpoint:

```text
GET /api/memory/search/hybrid?q={query}&limit={1..50}&scopeType={optional}&scopeId={optional}
```

The endpoint embeds the query, builds an authorized candidate set, computes full-text and semantic relevance, and returns final-score ordering plus component scores.

The query applies scope, membership, role assignment, namespace grant, active-status, and redaction filters before scoring. Only the authorized CTE feeds relevance, confidence, recency, authority, scope-match, final score, ordering, and limiting.

Ranking components:

- `relevance`: max of capped full-text rank and capped pgvector cosine similarity.
- `confidence`: source memory fact or role-lens confidence.
- `recency`: age-decayed score from source creation time with a 30-day denominator.
- `authority`: trust-level weight, highest for system/human-approved memory and lowest for retrieved-untrusted memory.
- `scopeMatch`: exact target scope match when `scopeType` and `scopeId` are supplied; otherwise a default proximity score by candidate scope.

## Consequences

- Hybrid search can feed M6-05 context packet construction without recomputing core ranking logic.
- Score components are visible in the response, making ranking behavior testable and explainable.
- Unauthorized rows still do not contribute to candidate counts, component scores, final ranking, or limits.
- The formula is intentionally simple and deterministic for the MVP; later evaluation work can tune weights without changing the authorization boundary.
