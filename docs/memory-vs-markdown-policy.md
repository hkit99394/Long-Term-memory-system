# Memory vs Markdown Policy

Date: 2026-06-06

Status: active project policy for improvement plan item IP-01.

Owners: Knowledge Steward and Product Owner.

## Purpose

This policy defines where project knowledge belongs before it becomes durable
memory, documentation, backlog state, release evidence, or a role-specific
lens. It exists because the project now uses its own memory system, and memory
must not become a hidden second roadmap.

Short rule:

```text
Markdown is the canonical source for project plans, policy, architecture, API
contracts, runbooks, release decisions, and backlog status.

Memory is a governed retrieval layer over source-backed facts, decisions,
rationale, risks, and role lenses.
```

For runtime memory records, PostgreSQL remains the source of truth. For project
planning and product meaning, committed Markdown and payload-safe release
evidence are the source that memory should cite.

## Authority Order

When project memory and repository documents disagree, resolve the conflict in
this order:

1. Current user instruction in the active task.
2. Committed source documents and release evidence in this repository.
3. Source events that cite those documents with hashes or stable evidence ids.
4. Active memory facts, decisions, and role lenses derived from that evidence.
5. Vault exports and generated reports.

Memory is useful because it retrieves the right thing quickly. It is not allowed
to make uncommitted plans, hidden policy, stale release claims, or role-specific
opinions authoritative by itself.

## Where Things Belong

| Knowledge type | Canonical home | Memory role |
| --- | --- | --- |
| Project goals, product principles, and durable policy | Markdown under `docs/` | Compact source-backed summary for retrieval. |
| Roadmap targets, priorities, status, and acceptance criteria | `docs/roadmap.md`, `docs/backlog.md`, and `docs/product-improvement-plan.md` | Current high-value state with source links, never the only copy. |
| Architecture decisions and implementation tradeoffs | Decision records under `docs/decisions/` plus architecture docs | Source-backed decision facts and rationale. |
| API contracts, caller rules, and examples | `docs/api/`, OpenAPI, schemas, and scripts | Searchable contract facts with policy metadata. |
| Source evidence | `POST /api/events` with source path, hash, event id, or payload-safe evidence record | Immutable evidence handle for memory proposals and review. |
| Durable memory facts and decisions | PostgreSQL through broker-approved memory proposals | Retrieval, fact finding, contradiction handling, and feedback loops. |
| Role-specific interpretation | Role-lens memory after source-backed proposal and review where needed | Perspective, attention, and risk framing for an authorized role. |
| Release decisions and pilot claims | Payload-safe release docs, readiness JSON, evidence bundles, and audit exports | Current status summaries with links to the evidence. |
| Human-readable memory exports | `vault/AI Memory System/` generated from authorized exports | Review convenience only; never authoritative state. |

## Promotion Rules

If a durable project claim matters for future agents, first put the source in
the right Markdown or release-evidence record, then seed or propose a compact
memory item with source evidence and a source hash.

Use memory directly for:

- compact facts that help agents find current project context
- decisions and rationale already backed by source evidence
- risks, constraints, requirements, and release evidence summaries
- role lenses that reframe shared truth for an authorized role
- feedback observations about useful, stale, wrong, sensitive, over-broad, or
  missing retrieval

Do not use memory as the only home for:

- the current roadmap or backlog
- product policy
- release GO or NO-GO decisions
- migration, deployment, or rollback instructions
- API contracts
- user secrets, raw credentials, or private tokens
- full document copies when a source path plus hash is enough
- facts that need review, owner approval, or source verification first

## Backlog And Improvement Plan Rule

The ordered improvement plan belongs in
[Product Improvement Plan](product-improvement-plan.md). Backlog execution
details belong in [Backlog](backlog.md). Memory may keep source-backed
summaries of the current priority, owner, and next action, but it must point
back to Markdown evidence.

If an operator gives an improvement plan through the memory API first, promote
the plan into Markdown before treating it as the durable project roadmap.

## Role Lens Rule

Role lenses are role-specific interpretations of shared truth, not separate
truth stores. A role lens may emphasize risks, priorities, acceptance concerns,
or review habits for a role. It must not invent policy, override current
Markdown, or expose facts outside the caller's authorization boundary.

## Release Evidence Rule

Release decisions and pilot claims require committed, payload-safe evidence.
Use release docs, readiness JSON, compliance evidence packages, benchmark
reports, backup/restore evidence, and audit exports as the canonical record.
Memory can summarize the current release posture only when it cites those
records.

## Agent Workflow

Before planning, coding, reviewing, or releasing project work:

1. Retrieve task-scoped memory context.
2. Query facts when decisions, contradictions, policy metadata, lifecycle
   state, or source links matter.
3. Verify any memory-derived claim against the linked source document when it
   changes code, tests, policy, release posture, or backlog status.

After the task:

1. Record feedback for useful, stale, wrong, sensitive, over-broad, or missing
   retrieved memory.
2. Update Markdown first when the work changes durable project meaning.
3. Add source-backed memory only after there is evidence worth citing.

## Seed And Hygiene Rule

`scripts/seed-production-knowledge-base.sh` is the source-backed bridge from
Markdown to project memory. Seed entries must:

- cite a committed source path
- validate curated excerpts against the current file
- record the source SHA-256
- store compact memory objects instead of full documents
- fail when curated excerpts drift

Follow-on hygiene automation should detect source hash drift, stale source
links, duplicate memory, missing evidence, and role-lens claims that no longer
match their source documents.

For roadmap and backlog changes, run
`scripts/backlog-roadmap-memory-sync.sh --dry-run` before reseeding so
`docs/roadmap.md` and `docs/backlog.md` remain canonical while memory receives
only compact source-backed summaries.
