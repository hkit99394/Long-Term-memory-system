# 0021 Memory Proposal Contradiction Detection

## Status

Accepted.

## Context

M5-04 needs accepted durable memory proposals to detect conflicts with active memory before a new fact is inserted. M5-03 already routes similar active memories to review when the same scope, memory type, subject, and predicate have a different object. Contradictions need a more specific review reason so later review, supersession, and audit workflows can distinguish ordinary overlap from direct conflict.

For this milestone, automatic supersession would be premature because review APIs and human approval flows are not implemented yet.

## Decision

Add a workflow-level contradiction check after the broker accepts a durable proposal and before the similar active-memory dedupe check.

The workflow searches active memory facts in the resolved scope and memory type. If it finds a memory with the same normalized subject and predicate, a different object, and a deterministic contradiction between the existing and proposed object values, the proposal returns `review_required` with a conflict-specific reason and the same candidate kind.

The initial deterministic contradiction rule covers clear opposite object terms such as enabled/disabled, yes/no, true/false, allowed/disallowed, approved/rejected, and use/do-not-use phrasing. Non-identical active memories that do not match the contradiction rule still use the M5-03 similar-memory review path.

The workflow does not mark existing memory as `contradicted` and does not supersede it automatically. Those lifecycle transitions remain explicit review-workflow responsibilities.

## Consequences

- Clear conflicts are flagged before a second durable fact is written.
- Ordinary similar memories continue to be reviewed without being mislabeled as contradictions.
- The rule is explainable and deterministic while leaving room for later broker scoring or human review workflows to add broader semantic contradiction detection.
