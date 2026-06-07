# Backlog And Roadmap Memory Sync IP-12

Status: implemented for improvement plan item IP-12.

Owner: Product Owner.

## Purpose

IP-12 keeps roadmap targets and backlog priorities anchored in committed
Markdown while still making high-value current state available through
source-backed project memory.

The rule is:

```text
Edit docs/roadmap.md and docs/backlog.md first. Then sync compact memory from
those files with source evidence and source hashes.
```

Run the sync report before reseeding roadmap or backlog memory:

```bash
scripts/backlog-roadmap-memory-sync.sh --dry-run
```

The script is payload-safe. It reads only committed Markdown and the local
knowledge seed manifest. It does not call the API and does not print raw memory
payloads beyond compact seed subjects, namespaces, hashes, and counts.

## Canonical Sources

| Source | Role |
| --- | --- |
| [Roadmap](roadmap.md) | Milestones, dependencies, decision gates, and current delivery target. |
| [Backlog](backlog.md) | Buildable work items, priorities, statuses, and acceptance criteria. |
| [Memory vs Markdown Policy](memory-vs-markdown-policy.md) | Rule that Markdown is canonical for roadmap/backlog state and memory is a source-linked retrieval projection. |
| `scripts/seed-production-knowledge-base.sh` | Source-backed bridge that pins `sourceSha256`, curated excerpts, and compact memory items. |

Current seeded roadmap/backlog memory covers:

- `roadmap current state`
- `production-shaped baseline`
- `external pilot readiness gates`
- `context productization backlog`
- `next backlog focus`

## Sync Report

`scripts/backlog-roadmap-memory-sync.sh` emits JSON with:

- `payloadSafe: true`
- `rawSourcePayloadsIncluded: false`
- `canonicalSources` for `docs/roadmap.md` and `docs/backlog.md`
- policy checks proving roadmap/backlog state belongs in Markdown first
- pinned and current SHA-256 values for both sources
- curated excerpt counts and stale excerpt counts
- seed memory item counts, subjects, namespaces, and memory types
- exact hygiene, dry-run seed, and live seed commands
- errors when a hash, excerpt, seed document, or policy check drifts

The report status is `clear` when the source hashes, excerpts, policy fragments,
and seed items are synchronized. It is `needs_sync` when Markdown changed but
the seed manifest has not been refreshed.

## Operating Procedure

When roadmap targets or backlog priorities change:

1. Update [Roadmap](roadmap.md) or [Backlog](backlog.md) first.
2. Recurate the roadmap/backlog excerpts in
   `scripts/seed-production-knowledge-base.sh`.
3. Refresh each changed `sourceSha256`:

   ```bash
   shasum -a 256 docs/roadmap.md docs/backlog.md
   ```

4. Validate the sync report and source-backed hygiene:

   ```bash
   scripts/backlog-roadmap-memory-sync.sh --dry-run
   scripts/source-backed-memory-hygiene.sh
   MEMORYSYSTEM_KNOWLEDGE_SEED_DRY_RUN=true scripts/seed-production-knowledge-base.sh
   ```

5. Seed the source-backed memory only after the dry run is clear:

   ```bash
   scripts/seed-production-knowledge-base.sh
   ```

6. If retrieved roadmap/backlog memory was stale, wrong, missing, or
   over-broad, record context feedback so the weekly review workflow can route
   the issue.

## Completion Evidence

IP-12 is complete when:

- `scripts/backlog-roadmap-memory-sync.sh` validates that roadmap and backlog
  Markdown remain canonical.
- the source-backed seed contains `roadmap` and `backlog` documents with pinned
  source hashes, curated excerpts, and compact high-value memory items.
- source-backed hygiene and seed dry run pass for the current roadmap/backlog.
- [Testing Commands](testing.md), [Project Memory Runbook](project-memory-runbook.md),
  and the documentation index point to the sync workflow.
- tests cover the documentation contract and dry-run output.
