# 0033 Archive Vault Exports

## Status

Accepted.

## Context

M7-05 needs old or superseded memory to remain readable without returning it to normal retrieval or current Obsidian export. M7-04 already marks previously exported deleted and redacted memory as stale, but stale markers intentionally omit the original memory body.

Archives are different from stale markers: they are readable historical records for memory that is inactive but still safe to inspect, such as superseded, expired, or contradicted memory.

## Decision

Add an authenticated archive endpoint:

```text
GET /api/vault/exports/obsidian/archive?limit={1..100}&scopeType={optional}&scopeId={optional}
```

The endpoint exports memory facts with statuses:

- `superseded`
- `expired`
- `contradicted`

It does not export `deleted` or `redacted` memory bodies. Those states continue to use stale markers from the current Obsidian export endpoint.

Archive documents are Obsidian-compatible Markdown under `90 Archive/`. They include frontmatter and body metadata for memory fact id, source event id, source link, memory type, scope, namespace, status, trust level, and confidence, followed by the archived memory body.

The endpoint filters every archive candidate through the same read authorization check used by current exports. Archive writes are tracked in `vault_exports` with `export_type = 'obsidian_archive'`.

`tools/vault-sync` adds `--include-archive`, which calls the archive endpoint and writes archive documents in the same path-safe way as current and stale export documents.

## Consequences

- Old inactive memory remains available for human inspection without entering normal retrieval.
- Redacted and deleted memory is not reintroduced through the archive path.
- M7 review and vault workflow now has current exports, stale markers, and readable archives as separate projections from PostgreSQL truth.
