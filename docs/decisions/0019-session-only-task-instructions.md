# 0019 Session-Only Task Instructions

## Status

Accepted.

## Context

M5-02 needs one-off task instructions to avoid durable storage even when a caller submits them through the memory proposal endpoint with a durable-looking memory type such as `preference`.

The M2 broker already treated explicit `session_instruction`, session scope, and `/session/...` namespace proposals as `session_only`. M5-02 extends that policy to obvious short-lived instructions such as "for this answer", "for this task", "use this temporary file", and "focus on this one bug today".

## Decision

Extend `MemoryProposalDecisionRules.IsSessionOnly` with deterministic one-off instruction detection over proposal subject, predicate, and object text.

The rule treats proposals as session-only when they contain short-lived markers such as:

- `for this answer`
- `for this response`
- `for this task`
- `current task`
- `temporary file`
- `one-off`
- `short-lived`
- `just this once`
- `this one bug`

The broker classifies these proposals as `session_only_instruction`, returns the existing `session_only` decision, and does not write memory facts, chunks, or outbox jobs.

The workflow uses the same session-only rule before durable write authorization, so one-off instructions do not require durable memory write access.

## Consequences

- One-off task instructions stay out of durable memory even when submitted under a user preference namespace.
- Long-term preferences still require durable phrasing and continue through the normal stored/review/rejected path.
- The rule is intentionally conservative and phrase-based until later broker intelligence introduces richer extraction or model-assisted classification.
