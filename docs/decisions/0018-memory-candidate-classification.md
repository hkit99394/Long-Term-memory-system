# 0018 Memory Candidate Classification

## Status

Accepted.

## Context

M5-01 starts broker intelligence by making candidate classification explicit. Earlier broker rules accepted supported memory types, but the decision result did not say which candidate family was recognized. Later M5 work needs a stable classification before adding session-only rejection rules, deduplication, contradiction checks, and confidence scoring.

## Decision

Add a deterministic candidate classifier in the application broker path.

The broker now distinguishes:

- `preference`
- `project_fact`
- `decision`
- `role_lens`
- `agent_private`
- `session_only_instruction`
- `unsupported`

Classification is derived from the normalized proposal fields the workflow already validates, primarily memory type plus scope. Stored, duplicate-stored, review-required, rejected, and session-only decisions now include `candidateKind` in the decision payload.

Unsupported memory type and scope combinations are rejected by the broker with `candidateKind = "unsupported"`. Session-only candidates retain the existing `session_only` decision behavior; M5-02 will refine one-off instruction handling beyond explicit session scope/type.

## Consequences

- Broker decisions can be inspected and tested by candidate family without inferring from raw memory type.
- API responses and idempotency records preserve the recognized candidate kind.
- Later M5 broker rules can switch on candidate kind instead of duplicating memory type and scope checks.
