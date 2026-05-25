# 0028 Retrieval Evaluation Tests

## Status

Accepted.

## Context

M6-06 needs a repeatable way to measure retrieval behavior after M6-01 through M6-05 introduced authorized full-text search, semantic search, hybrid ranking, and context packets. The evaluation needs to cover packet quality and memory-write quality without depending on a non-deterministic model score.

The first evaluation path should run in ordinary test commands and should be narrow enough to use as a regression guard while later offline evaluation datasets are still deferred.

## Decision

Add an application-level `MemoryRetrievalEvaluator` scorecard that evaluates observed retrieval items and write observations against an explicit test case.

The scorecard reports:

- `relevance`: expected relevant source ids retrieved divided by expected relevant source ids.
- `compactness`: the lower score of packet item-count adherence and per-item content-length adherence.
- `writePrecision`: expected durable writes divided by observed durable writes.
- false positives: retrieved forbidden or non-allowed source ids plus unexpected durable writes.
- `contradictionQuality`: contradicted memories excluded from retrieval and contradiction write candidates routed away from durable storage.

The database-backed evaluation test feeds the scorecard from the real context-packet API response. Unit tests cover non-perfect score behavior so misses, oversize items, false positives, low write precision, and contradiction gaps are visible as metric changes.

## Consequences

- Retrieval evaluation is deterministic and CI-friendly.
- Context packet regressions can be detected as score changes without adding another API surface.
- Future evaluation datasets can reuse the scorecard with larger scenario fixtures or offline runners.
