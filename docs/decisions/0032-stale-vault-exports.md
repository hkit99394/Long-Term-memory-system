# 0032 Stale Vault Exports

## Status

Accepted.

## Context

M7-04 needs vault exports to stop presenting deleted or redacted memory as current truth. M7-03 exported Markdown directly from active decision and summary facts. That is enough for first export, but it does not remember which vault path was written if the underlying memory later becomes inactive.

The vault remains export-only and non-authoritative. PostgreSQL must remain the source of truth for memory lifecycle state.

## Decision

Add `vault_exports` as export tracking metadata for Obsidian Markdown projections. Each row records:

- memory fact id
- export type
- vault-relative export path
- source event id
- export status: `current` or `stale`
- stale reason when applicable

`GET /api/vault/exports/obsidian` now returns:

- `documents`: current active decision and summary exports
- `staleDocuments`: audit-safe stale marker documents for previously exported memories whose authoritative memory fact is now superseded, contradicted, expired, deleted, or redacted

The API records current exports when it renders documents and marks export records stale when it returns stale documents. Stale marker Markdown uses the previously recorded export path and includes ids, source links, status, and reason, but not the original memory body.

`tools/vault-sync` writes both current documents and stale marker documents. A stale marker overwrites the previous path so the vault no longer displays old content as current memory.

## Consequences

- Deleted and redacted memory facts can invalidate previously exported Markdown without relying on current memory text to reconstruct the path.
- Stale export handling remains a projection workflow; it does not make Obsidian transactional storage.
- M7-05 archive export can build on the same distinction between current exports, stale markers, and readable archives.
