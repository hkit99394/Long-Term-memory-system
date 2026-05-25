# 0035 Structured Operational Logging

## Status

Accepted.

## Context

M8-02 adds operational logs for decision points that operators need during debugging: memory proposal broker decisions, retrieval decisions, and review-driven redaction actions.

These paths handle user memory content, search text, review notes, and replacement memory text. Logging those payloads would make logs another sensitive data store, so the logging contract must be structured and audit-safe.

## Decision

Add structured API logs at the decision boundary for:

- memory proposal decisions from `POST /api/memory/proposals`
- retrieval outcomes from full-text, semantic, hybrid, and context-packet endpoints
- unavailable semantic retrieval configuration
- memory review action completion and failure
- explicit review-driven redaction actions for `delete` and `expire`

Logs may include operational metadata:

- principal id
- source event id
- review id
- memory fact id
- replacement memory fact id
- decision/action/status names
- candidate kind
- memory type
- scope type and scope id
- namespace
- visibility, trust level, and sensitivity labels
- confidence
- retrieval mode, limit, query length, and result/category counts
- embedding provider/model/dimension when semantic retrieval is unavailable

Logs must not include:

- proposal subject, predicate, or object
- search query text
- context packet content
- review notes
- edited or superseding replacement content
- memory body text
- raw event payload content

## Consequences

- Operators can trace important decisions without turning logs into a second copy of user memory.
- Query text is represented by length and result counts, not by the text itself.
- Review delete and expire actions are visible as redaction actions even before a fuller retention/redaction policy is implemented.
