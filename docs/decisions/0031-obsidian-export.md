# 0031 Obsidian Export

## Status

Accepted.

## Context

M7-03 needs approved summaries and decisions to leave PostgreSQL as human-readable Obsidian Markdown while preserving source IDs. The vault must remain export-only: it is useful for reading, review, and local notes, but it must not become permission enforcement or transactional memory storage.

M7-04 will handle stale exports after deletion and redaction. The M7-03 export therefore needs stable paths and source metadata that a later stale-export workflow can inspect.

## Decision

Add an authenticated export endpoint:

```text
GET /api/vault/exports/obsidian?limit={1..100}&scopeType={optional}&scopeId={optional}
```

The endpoint returns Obsidian-ready Markdown documents as JSON. Export candidates are active memory facts with `memory_type` of `decision` or `summary` and either:

- `trust_level` of `system_trusted` or `human_approved`
- an approved `memory_reviews` row

The application service filters every candidate through the existing `read` permission check before rendering it. Each Markdown document includes frontmatter and body metadata for:

- memory fact id
- source event id
- source event link
- memory type
- scope
- namespace
- trust level
- confidence

Add `tools/vault-sync`, a dependency-free Node/TypeScript CLI that calls the export endpoint and writes the returned Markdown files into `vault/AI Memory System`. The tool validates returned paths before writing so API output cannot escape the configured vault root.

## Consequences

- Approved decisions and summaries can be exported without making the vault authoritative.
- Source IDs are embedded in every exported document for future stale, deletion, and redaction workflows.
- The first export path remains simple: no new database table is required until M7-04 defines stale-export tracking.
