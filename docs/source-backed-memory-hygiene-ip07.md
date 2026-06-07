# Source-Backed Memory Hygiene Automation IP-07

Status: implemented for improvement plan item IP-07.

Owner: Knowledge Steward + Tester/QA.

## Purpose

Source-backed memory is useful only while agents can trace a compact memory
claim back to current committed source evidence. IP-07 adds a local hygiene
gate for the production knowledge seed so source-backed project memory cannot
silently drift away from Markdown truth.

Run the gate before reseeding production knowledge, after editing any seeded
source document, and before release evidence that depends on project memory:

```bash
./scripts/source-backed-memory-hygiene.sh
```

The script reads the `documents` manifest inside
`scripts/seed-production-knowledge-base.sh`. It does not call the API and does
not read or print source payloads beyond repository paths, SHA-256 hashes, and
counts.

## Automated Checks

- Source hash drift: every seeded document pins `sourceSha256`; the current
  file hash must match before the seed can write memory.
- Stale source links: seeded source paths must be clean repository-relative
  UTF-8 files tracked by git.
- Curated excerpt drift: every curated excerpt must still appear in the source
  document.
- Missing evidence: each document must carry source path, hash, title, summary,
  excerpts, and items; the seed payload must preserve source path, source hash,
  and curated excerpts on the source event.
- Duplicate memory identities: memory identity is checked by memory type,
  namespace, subject, predicate, and object; failures report a short identity
  hash rather than dumping memory text.
- Source-backed seed validation: memory types must use the canonical IP-06
  vocabulary and namespaces must stay under the canonical project boundary.
- Role lens source contract: future `role_lens` seed entries must use a canonical role
  namespace and include `roleId` plus `baseMemoryFactId` so role-lens claims
  remain interpretations of shared source-backed truth.

## Seed Contract

Each entry in `scripts/seed-production-knowledge-base.sh` must include:

- `path`
- `sourceSha256`
- `title`
- `summary`
- `excerpts`
- `items`

When a canonical source document changes, update Markdown first, recurate the
excerpt list, refresh the pinned hash with:

```bash
shasum -a 256 docs/path-to-source.md
```

Then run:

```bash
./scripts/source-backed-memory-hygiene.sh
MEMORYSYSTEM_KNOWLEDGE_SEED_DRY_RUN=true ./scripts/seed-production-knowledge-base.sh
```

The dry run and the standalone hygiene gate both fail on source hash drift and
curated excerpt drift. The standalone gate also checks duplicate identities,
missing evidence, canonical memory types, tracked source links, and future
role-lens seed hygiene.

## Completion Evidence

IP-07 is complete when:

- `scripts/source-backed-memory-hygiene.sh` passes against the repository seed.
- `scripts/seed-production-knowledge-base.sh` pins source hashes and refuses
  stale source inputs during dry run or live seed.
- [Testing Commands](testing.md) lists the hygiene gate and shell syntax check.
- Unit tests cover the documentation contract and execute the hygiene gate.
