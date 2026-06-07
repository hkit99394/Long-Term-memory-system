# Canonical Memory Types IP-06

Date completed: 2026-06-07

Status: implemented

## Purpose

IP-06 defines the durable memory type vocabulary that agents and operators can
use when proposing or filtering source-backed memory.

The canonical durable memory types are:

- `goal`
- `target`
- `fact`
- `decision`
- `rationale`
- `risk`
- `assumption`
- `constraint`
- `requirement`
- `release_evidence`
- `role_lens`

## Runtime Contract

`MemorySystem.Domain.MemoryTypes.MemoryType` is the source of truth for memory
type normalization.

Proposal validation accepts the canonical durable types above. It also keeps
existing runtime compatibility for:

- `preference`
- `role_principle`
- `project_role_lens`
- `agent_private`
- `session_instruction`

Query-facts filtering accepts the canonical durable types and legacy queryable
rows that may already exist in PostgreSQL, including `principle` and `summary`.

## Storage Rules

- `goal`, `target`, `fact`, `rationale`, `risk`, `assumption`, `constraint`,
  `requirement`, and `release_evidence` are durable project memory fact types.
- `decision` remains its own project decision candidate kind.
- `role_lens` is the canonical proposal type for shared and project role lenses;
  it writes to `role_memory_lenses`, not `memory_facts`.
- Legacy `role_principle` and `project_role_lens` proposal types remain accepted
  aliases for compatibility.
- `session_instruction` remains session-only and is not stored as durable memory.

## Agent Contract

The agent-facing OpenAPI `MemoryType` enum includes the canonical durable types
and the legacy compatibility values. New callers should prefer `role_lens` over
`role_principle` or `project_role_lens`.
