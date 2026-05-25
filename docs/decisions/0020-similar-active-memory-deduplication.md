# 0020 Similar Active Memory Deduplication

## Status

Accepted.

## Context

M5-03 needs memory proposal writes to avoid blindly inserting a second active memory when a similar active fact already exists. The database already has an exact active-memory dedupe index for identical scope, type, subject, predicate, and object values, and the transactional write store reuses the existing memory fact when that exact duplicate path is hit.

Similar but non-identical memories need an application policy. For this milestone, the conservative option is review rather than automatic update or supersession.

## Decision

Add a workflow-level active-memory dedupe check after the broker accepts a durable proposal and before the write store inserts it.

The workflow searches active memory facts in the resolved scope and memory type. If it finds a memory with the same normalized subject and predicate but a different normalized object, the proposal is routed to `review_required` with the same candidate kind.

Exact duplicates continue to use the existing write-store/index path and return `stored` with the existing memory id.

The M5-03 rule does not update or supersede existing facts. M5-04 and later review workflows will handle contradiction, supersession, and human approval paths.

## Consequences

- Similar active memories are reviewed instead of blindly duplicated.
- Exact duplicate retries or duplicate submissions continue to be idempotent and reuse the existing memory fact.
- The dedupe rule stays deterministic and explainable while avoiding premature automatic merge behavior.
