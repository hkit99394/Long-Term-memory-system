# 0022 Memory Proposal Confidence Scoring

## Status

Accepted.

## Context

M5-05 needs the broker to assign an effective confidence score instead of treating the request confidence as the final durable-memory confidence. Earlier proposal writes required a confidence value, and the broker routed low or missing confidence to review. That was safe, but it did not account for source evidence trust level or allow the broker to assign a score when the request omitted one.

The source event remains required evidence for durable memory. The proposal workflow already derives the proposal trust level from the source event before invoking the broker.

## Decision

Add deterministic broker confidence scoring for durable proposal candidates.

The broker now returns `confidence` on proposal decisions that reach the scoring stage. If the request provides confidence, the broker caps it by the derived source trust level. If the request omits confidence, the broker assigns a trust-level default.

Initial trust-level defaults and caps:

| Trust level | Default | Maximum |
| --- | --- | --- |
| `human_approved` | `0.950` | `1.000` |
| `system_trusted` | `0.950` | `0.980` |
| `user_scoped` | `0.850` | `0.900` |
| `agent_private` | `0.800` | `0.850` |
| `tool_output` | `0.720` | `0.800` |
| `web_content` | `0.550` | `0.650` |
| `retrieved_untrusted` | `0.500` | `0.600` |

The durable storage threshold remains `0.700`. Proposals below that effective confidence return `review_required`. `secret` and `regulated` sensitivity also continue to return `review_required`.

When a proposal is stored, the write store persists the broker-assigned effective confidence rather than the raw request confidence.

## Consequences

- Missing request confidence no longer forces review when trusted source evidence can assign a safe default.
- Low-trust evidence such as web content cannot self-promote into durable storage by sending a high request confidence.
- Proposal API responses expose the effective confidence used for review and storage decisions.
- The scoring table is intentionally deterministic and can be replaced or extended by later evidence-aware broker scoring.
