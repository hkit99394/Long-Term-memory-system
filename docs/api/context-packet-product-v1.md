# Context Packet Product v1 Contract

Date: 2026-05-31

Status: Draft contract for CP-01

Schema: [context-packet-product-v1.schema.json](context-packet-product-v1.schema.json)

## Purpose

This document defines the first productized context packet response contract.
It is the CP-01 deliverable for the LR-02 context productization gate.

The current `GET /api/memory/context` response remains unchanged until runtime
implementation slices start. This contract defines the target additive response
shape that later CP slices will implement and verify.

## Version

The contract version is:

```text
context-packet.product.v1
```

Every response that claims this contract must include:

```json
{
  "schemaVersion": "context-packet.product.v1"
}
```

Breaking response-shape changes must create a new schema version instead of
silently changing this one.

## Compatibility Rule

The productized response preserves the current grouped item arrays:

- `userPreferences`
- `projectMemory`
- `roleMemory`
- `relevantDecisions`
- `sourceEvents`

Existing item fields remain present. The productized contract adds stable packet
and item identifiers, richer explanations, safe exclusions, review actions,
feedback policy, and evaluation hints around those existing groups.

## Top-Level Fields

| Field | Purpose |
| --- | --- |
| `schemaVersion` | Contract version, currently `context-packet.product.v1`. |
| `packetId` | Stable id for feedback, review actions, and trace correlation. |
| `generatedAt` | Server timestamp used for recency and lifecycle explanations. |
| `principalId` | Authenticated principal that requested the packet. |
| `query` | Caller-supplied query used for retrieval. Feedback storage still stores only a hash. |
| `targetScope` | Optional target scope used to bound retrieval. |
| `roleId` | Optional role perspective used to bound role memory. |
| `currentTask` | Existing current-task summary. |
| `policy` | Payload-safe summary of authorization and retrieval filters. |
| Group arrays | Existing grouped context packet items. |
| `excluded` | Safe summaries for omitted candidates. |
| `reviewActions` | Packet-level actions the caller may take. |
| `feedbackPolicy` | Explains allowed feedback actions and raw-query storage rules. |
| `evaluationHints` | Payload-safe counters and ids for benchmarks and dashboards. |

## Item Explanations

Each grouped item keeps the existing fields and adds:

- `itemId`
- structured `explanation.primaryReason`
- bounded `explanation.matchedSignals`
- existing rank components
- `explanation.policyFit`
- `explanation.lifecycleFit`
- `explanation.sourceEvidence`
- `reviewActions`

Explanations must be payload-safe. They can describe the policy checks that were
applied, but they must not reveal unauthorized candidate namespaces, hidden
counts, redacted content, deleted content, or source payloads the caller has not
read through an authorized evidence endpoint.

## Exclusion Summaries

Allowed exclusion reasons are:

- `inactive`
- `not_authorized`
- `scope_mismatch`
- `role_mismatch`
- `below_rank_cutoff`
- `source_unavailable`
- `sensitive`

Unauthorized exclusions default to:

```json
{
  "reason": "not_authorized",
  "count": null,
  "countDisclosure": "withheld"
}
```

The contract permits counts only when the count cannot reveal hidden projects,
roles, namespaces, or sensitive content.

## Feedback Policy

The first productized action set is:

- `useful`
- `stale`
- `wrong`
- `sensitive`
- `over_broad`
- `missing`

The legacy `noisy` feedback type remains a compatibility alias for
`over_broad` until the runtime migration is complete.

Item-level feedback must identify the returned item or source. Packet-level
`missing` feedback may omit source identifiers because it describes absent
memory.

## Evaluation Hints

`evaluationHints` is intentionally payload-safe. It may include counts,
coverage ratios, item ids, withheld-exclusion presence, and benchmark tags. It
must not include raw memory text, raw source payloads, raw query hashes that are
not already part of feedback storage, or unauthorized source identifiers.

## Runtime Migration

CP-01 defines the contract only. Follow-on slices should implement it in this
order:

1. Done: CP-02 adds packet and item identifiers to context responses and
   feedback.
2. Done: CP-03 fills structured inclusion explanations.
3. CP-04 adds safe exclusion summaries.
4. CP-05 expands feedback actions.
5. CP-08 adds benchmark checks against this contract.
