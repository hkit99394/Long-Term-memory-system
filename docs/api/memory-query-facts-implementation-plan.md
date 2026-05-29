# `memory.queryFacts` Implementation Plan

Last reviewed: 2026-05-29

## Purpose

This document is the LMSS-04 implementation plan for `memory.queryFacts`.

The endpoint should let agents ask what the memory system knows about a question
without receiving a prose answer. It should return authorized facts, source
evidence, confidence, lifecycle status, contradiction summaries, and safe policy
metadata.

This is a design artifact only. LMSS-05 will implement the endpoint.

## Product Contract

`memory.queryFacts` should answer:

```text
What facts does the governed memory system know for this task, and why are they
safe to use?
```

It should not:

- build a final LLM answer
- return raw unauthorized memory
- expose exact unauthorized row existence as a side channel
- include deleted or redacted memory content
- write retrieval feedback

The planned agent-facing route is:

```text
POST /api/memory/query-facts
```

This is a read operation. It requires `X-Api-Key` authentication but does not use
`Idempotency-Key`.

## Request Shape

Add a request DTO under `src/MemorySystem.Api/MemoryFacts/`:

```json
{
  "query": "What migration strategy is accepted for Project A?",
  "targetScope": {
    "scopeType": "project",
    "scopeId": "33333333-3333-4333-8333-333333333333"
  },
  "roleId": "cto",
  "namespaces": ["/project/33333333-3333-4333-8333-333333333333/decisions"],
  "memoryTypes": ["decision", "preference", "fact"],
  "includeContradictions": true,
  "includeExcluded": true,
  "limit": 8
}
```

Validation rules:

- `query` is required and trimmed.
- `targetScope.scopeType` and `targetScope.scopeId` are optional together.
- `targetScope.scopeType` must pass `MemoryScopePolicy.TryNormalizeTargetScope`.
- `roleId` must pass `MemoryScopePolicy.TryNormalizeRoleId`.
- `namespaces` are optional, must start with `/`, and should be capped at 10.
- `memoryTypes` are optional and must use supported memory fact types.
- `limit` defaults to 8 and should be capped at 20 for v1.
- Request bodies should remain small enough for normal API JSON handling.

## Response Shape

Add response DTOs under `src/MemorySystem.Api/MemoryFacts/`:

```json
{
  "query": "What migration strategy is accepted for Project A?",
  "targetScope": {
    "scopeType": "project",
    "scopeId": "33333333-3333-4333-8333-333333333333"
  },
  "roleId": "cto",
  "facts": [
    {
      "id": "memory-fact-id",
      "claim": "Project A schema migrations use SQL-first migrations plus raw Npgsql.",
      "memoryType": "decision",
      "status": "active",
      "confidence": 0.95,
      "scopeType": "project",
      "scopeId": "33333333-3333-4333-8333-333333333333",
      "namespace": "/project/33333333-3333-4333-8333-333333333333/decisions",
      "sourceEventIds": ["source-event-id"],
      "sourceLinks": ["/api/events/source-event-id"],
      "policy": {
        "authorized": true,
        "trustLevel": "user_scoped",
        "sensitivity": "none",
        "lifecycleStatus": "active",
        "evidenceCurrent": true
      }
    }
  ],
  "contradictions": [],
  "excluded": [
    {
      "reason": "inactive",
      "count": 2
    },
    {
      "reason": "not_authorized",
      "count": null,
      "countDisclosure": "withheld"
    }
  ],
  "warnings": [],
  "overallConfidence": 0.95
}
```

Response rules:

- `facts` contains active, authorized memory facts only.
- `claim` is deterministic: subject + predicate + object.
- `sourceLinks` are built with the existing `ISourceEventLinkBuilder`.
- `policy.authorized` is always `true` for returned facts.
- `policy.evidenceCurrent` is true only when the source event is not redacted
  and not `erasure_requested`.
- `contradictions` may include non-active authorized facts only when
  `includeContradictions` is true and content is safe to show.
- `excluded` may include exact counts for post-authorization filters such as
  inactive, type-filtered, namespace-filtered, and over-limit results.
- `excluded` must not include exact unauthorized counts. Use
  `count: null` and `countDisclosure: "withheld"` for `not_authorized`.
- `overallConfidence` is the maximum confidence of returned active facts, or
  `0` when no facts are returned.

## Architecture Mapping

### API Layer

Update [MemoryFactEndpointExtensions](../../src/MemorySystem.Api/MemoryFacts/MemoryFactEndpointExtensions.cs):

- map `POST /api/memory/query-facts`
- require authorization
- read JSON with `ApiRequestHelpers.ReadJsonBodyAsync`
- normalize principal with `ApiRequestHelpers.TryReadPrincipalId`
- return `ProblemDetails` for validation failures
- log metadata only: principal id, query length, target scope, role id, limit,
  fact count, contradiction count, and warning count
- do not log query text, claim text, subject, predicate, or object

Add DTO files:

- `MemoryQueryFactsRequest.cs`
- `MemoryQueryFactsResponse.cs`

Register the application service in
[ApiMemoryFactServiceCollectionExtensions](../../src/MemorySystem.Api/MemoryFacts/ApiMemoryFactServiceCollectionExtensions.cs).

### Application Layer

Add a new application boundary under `src/MemorySystem.Application/MemoryFacts/`:

- `IMemoryFactFindingService`
- `MemoryFactFindingService`
- `IMemoryFactFindingStore`
- `MemoryFactFindingQuery`
- `MemoryFactFindingResult`
- `MemoryFactFindingRecord`
- `MemoryFactContradictionRecord`
- `MemoryFactExclusionSummary`

The service should own:

- request validation that belongs to the use case
- target scope normalization
- role normalization
- supported memory type filtering
- deterministic claim formatting
- overall confidence calculation
- warning construction
- mapping store results into the response model

The store should own:

- SQL query shape
- authorization predicates
- lifecycle filters
- source event joins
- contradiction candidates
- exclusion summaries that are safe to compute in SQL

Do not place this in `MemorySystem.Domain` for LMSS-05. The current project does
not yet use that layer for these concepts, and the fact-finding endpoint should
follow existing Application/Infrastructure boundaries first.

### Infrastructure Layer

Add `PostgresMemoryFactFindingStore` under
`src/MemorySystem.Infrastructure/MemoryFacts/`.

It should not reuse `PostgresMemoryFactRepository.SearchAsync` for the main
query because that repository:

- searches one resolved scope at a time
- does not apply `PostgresMemoryAccessSql.BuildReadPredicate`
- does not expose source event redaction state
- does not include contradiction or exclusion summaries

The new store should reuse the same authorization SQL primitive used by hybrid
search:

- `PostgresMemoryAccessSql.AddReadParameters(command)`
- `PostgresMemoryAccessSql.BuildReadPredicate("candidate")`

Use a candidate CTE that projects the columns expected by
`BuildReadPredicate`:

```text
scope_type
scope_id
namespace
scope_org_id
scope_project_id
required_role_id
```

For `required_role_id`, use the existing PostgreSQL function:

```sql
memory_required_role_id(fact.namespace, fact.scope_type, fact.scope_id, fact.role_id)
```

Join `events` for:

- source event id
- trust level
- sensitivity
- retention class
- redaction status

Join `projects` for project organization lookup, matching the hybrid search
approach.

## SQL Query Shape

The v1 SQL should follow this sequence:

1. Build `candidate_facts` from `memory_facts` plus source `events` and optional
   `projects`.
2. Apply query matching, target scope, role, namespace, memory type, and status
   filters where they do not require authorization.
3. Apply `PostgresMemoryAccessSql.BuildReadPredicate("candidate")` before any
   row content is returned.
4. Split authorized rows into:
   - active facts for `facts`
   - safe inactive rows for `contradictions`
   - post-authorization exclusion counts
5. Order active facts by:
   - exact subject/predicate/object text match score
   - full-text rank over subject/predicate/object
   - confidence
   - source recency
   - id for deterministic ties

Implementation can start without embeddings. `memory.queryFacts` is fact
finding, not semantic context packing. If semantic matching is later required,
it should be added behind the same authorization-before-ranking boundary used by
hybrid search.

## Matching Rules

Initial v1 matching should use deterministic PostgreSQL text search:

- Build a `tsvector` from `subject`, `predicate`, and `object`.
- Use `websearch_to_tsquery('english', @query)`.
- Accept rows where the vector matches the query.
- Boost exact or partial subject matches.
- Require active status for returned `facts`.

If `targetScope` is supplied:

- exact target scope facts are strongest
- organization-level facts for the target project may be included when
  authorized
- global facts may be included when authorized
- unrelated project facts must not be included

If `roleId` is supplied:

- role-specific namespaces must match that role
- non-role facts may still be returned when otherwise authorized and relevant
- role-lens records remain out of v1 fact results unless LMSS-05 explicitly adds
  a `sourceType` union

## Contradiction Handling

Contradiction summaries should be conservative in LMSS-05.

Start with memory fact rows only:

- same `scope_type`, `scope_id`, `memory_type`, normalized `subject`, and
  normalized `predicate`
- different `object`
- status in `active`, `tentative`, `superseded`, `contradicted`, or `expired`
- authorized by the same read predicate
- source event not redacted and not `erasure_requested`

Do not show content for `deleted` or `redacted` rows. Those may contribute only
to safe excluded summaries such as `redacted_or_deleted`.

Use the existing contradiction rule as guidance:

- [MemoryProposalContradictionRules](../../src/MemorySystem.Application/MemoryProposals/MemoryProposalContradictionRules.cs)

The first implementation can return contradiction records when:

- an active fact has another authorized active fact with a contradictory object
- an active fact replaced an authorized superseded fact
- a contradicted fact exists for the same subject/predicate pair

Response fields for a contradiction record:

```json
{
  "subject": "schema migrations",
  "predicate": "use",
  "currentFactId": "active-fact-id",
  "relatedFactId": "inactive-or-conflicting-fact-id",
  "relatedStatus": "superseded",
  "summary": "An older authorized fact was superseded by the current fact.",
  "sourceLinks": ["/api/events/source-event-id"]
}
```

## Exclusion Handling

`includeExcluded` is useful, but it is also a possible side channel.

LMSS-05 should return:

- exact counts for facts excluded after authorization
- exact counts for inactive authorized facts
- exact counts for over-limit authorized facts
- exact counts for namespace/type filters that were applied after
  authorization

LMSS-05 should not return:

- exact counts of unauthorized facts
- unauthorized source ids
- unauthorized namespaces
- unauthorized subjects, predicates, objects, or source links

Use this shape for unauthorized exclusion:

```json
{
  "reason": "not_authorized",
  "count": null,
  "countDisclosure": "withheld"
}
```

## Files To Add Or Change In LMSS-05

API:

- `src/MemorySystem.Api/MemoryFacts/MemoryQueryFactsRequest.cs`
- `src/MemorySystem.Api/MemoryFacts/MemoryQueryFactsResponse.cs`
- `src/MemorySystem.Api/MemoryFacts/MemoryFactEndpointExtensions.cs`
- `src/MemorySystem.Api/MemoryFacts/ApiMemoryFactServiceCollectionExtensions.cs`

Application:

- `src/MemorySystem.Application/MemoryFacts/IMemoryFactFindingService.cs`
- `src/MemorySystem.Application/MemoryFacts/MemoryFactFindingService.cs`
- `src/MemorySystem.Application/MemoryFacts/IMemoryFactFindingStore.cs`
- `src/MemorySystem.Application/MemoryFacts/MemoryFactFindingQuery.cs`
- `src/MemorySystem.Application/MemoryFacts/MemoryFactFindingResult.cs`

Infrastructure:

- `src/MemorySystem.Infrastructure/MemoryFacts/PostgresMemoryFactFindingStore.cs`

Tests:

- `tests/MemorySystem.UnitTests/MemoryFactFindingServiceTests.cs`
- `tests/MemorySystem.IntegrationTests/ApiMemoryFactFindingTests.cs`

Docs:

- update [Agent Memory OpenAPI v1](agent-memory-v1.openapi.json)
- update [Agent Memory v1 Client Examples](agent-memory-v1-examples.md)
- update [Agent-Facing Memory Contract](../agent-facing-memory-contract.md) if
  response shape changes during implementation

## Database-Backed Test Plan

Add tests in `ApiMemoryFactFindingTests`.

Required tests:

1. `POST /api/memory/query-facts` requires authentication.
2. Invalid request body returns `ProblemDetails`.
3. Active authorized Project A facts return claims, source ids, source links,
   confidence, lifecycle status, and policy metadata.
4. Project B facts do not appear in Project A results or response body.
5. Deleted and redacted memory facts do not return content.
6. Superseded or contradicted authorized facts appear only when
   `includeContradictions` is true.
7. `includeExcluded` does not return exact unauthorized counts.
8. Namespace filters restrict returned facts without bypassing authorization.
9. Memory type filters restrict returned facts.
10. Role-specific namespaces require the matching `roleId` and role assignment.
11. Response ordering is deterministic for equal scores.
12. The endpoint logs counts and query length but not raw query or fact content.

Unit tests should cover:

- request normalization
- deterministic claim formatting
- overall confidence calculation
- warning construction
- contradiction classification from store records

## OpenAPI Update For LMSS-05

After implementation, add this path to
[Agent Memory OpenAPI v1](agent-memory-v1.openapi.json):

```text
POST /api/memory/query-facts
operationId: memory_queryFacts
x-agent-tool-name: memory.queryFacts
```

The OpenAPI contract should document that this is a read operation and should
not include `Idempotency-Key`.

## Rollout Order

1. Add Application contracts and unit tests.
2. Add `PostgresMemoryFactFindingStore` with authorization-before-return SQL.
3. Add API DTOs and endpoint mapping.
4. Add database-backed API tests.
5. Update OpenAPI and client examples.
6. Run fast tests and database tests for the fact-finding slice.

## Acceptance Criteria

LMSS-04 is complete when this plan exists and links from the backlog. LMSS-05 is
complete when the endpoint, tests, OpenAPI, and examples implement this plan.
