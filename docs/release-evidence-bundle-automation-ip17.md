# Release Evidence Bundle Automation IP-17

Status: implemented for improvement plan item IP-17.

Owner: Release Manager + Ops.

## Purpose

IP-17 creates one payload-safe release evidence bundle per release. The bundle
links evidence for tests, migration status, health, operations summary,
benchmark gate, backup/restore, and rollback without embedding raw artifact
contents.

This is a release-manager packaging command. It does not run migrations, call
health endpoints, read memory payloads, or copy secret-bearing logs. It records
artifact paths, byte counts, SHA-256 hashes, gate status, release identity,
operator id, and rollback owner.

## Command

Create a draft bundle while evidence is still being collected:

```bash
scripts/release-evidence-bundle.sh \
  --mode draft \
  --release-id local-2026-06-07
```

Create a strict bundle before pilot or production promotion:

```bash
scripts/release-evidence-bundle.sh \
  --mode strict \
  --release-id pilot-2026-06-07 \
  --environment pilot \
  --operator-id release-manager \
  --rollback-owner ops-rollback-owner \
  --tests-report /path/to/tests.json \
  --migration-status /path/to/migration-status.json \
  --health-report /path/to/health.json \
  --operations-summary /path/to/operations-summary.json \
  --benchmark-report /path/to/benchmark-release-gate.json \
  --backup-restore-report /path/to/backup-restore.json \
  --rollback-report /path/to/rollback-plan.json
```

Use `--dry-run` to print the manifest without writing output files.

## Evidence Gates

| Gate | Required artifact | Typical source |
| --- | --- | --- |
| Tests | `test_report` | Unit/integration/TypeScript/CI test report or command transcript. |
| Migration status | `migration_status` | Migrator output, migration table check, or explicit no-migration evidence. |
| Health | `health_report` | `/health/live`, `/health/ready`, worker heartbeat, and read/write smoke output. |
| Operations summary | `operations_summary` | `/api/operations/summary`, `/api/operations/metrics`, or operations metrics smoke output. |
| Benchmark | `benchmark_release_gate` | `scripts/benchmark-release-gate.sh` JSON report or release-manager skip record. |
| Backup/restore | `backup_restore_evidence` | Backup export, backup freshness, restore validation, or local backup/restore smoke output. |
| Rollback | `rollback_plan` | Named rollback owner, rollback boundary, command/procedure, and decision deadline. |

Strict mode fails when any required artifact is missing or the rollback owner is
not named. Draft mode writes an incomplete bundle so the release manager can
see the remaining evidence gaps.

## Outputs

The default output directory is `/tmp/memorysystem-release-evidence`. Override
it with `--output-dir` or `MEMORYSYSTEM_RELEASE_EVIDENCE_DIR`.

Each run writes:

- JSON manifest: `<bundle-id>.json`
- NDJSON artifact index: `<bundle-id>-artifacts.ndjson`
- Markdown summary: `<bundle-id>.md`
- deterministic SHA-256 sidecar: `<bundle-id>.json.sha256`

The manifest uses:

```text
kind = memorysystem.release_evidence_bundle
schemaVersion = 1
payloadSafe = true
rawArtifactPayloadsIncluded = false
rawSourcePayloadsIncluded = false
```

Important manifest fields include `artifactIndexPath`, `sha256SidecarPath`,
`rollbackOwner`, `requiredGates`, `artifacts`, `counts`, `bundleStatus`, and
`promotionRule`.

The bundle explicitly omits raw artifact contents, source event payloads,
memory bodies, review notes, raw queries, embedding inputs, provider payload
bytes, API keys, and secret values.

## Promotion Rule

Pilot or production promotion requires:

- strict mode exits successfully
- zero missing required artifacts
- named rollback owner
- archived JSON manifest, NDJSON artifact index, Markdown summary, and SHA-256
  sidecar
- links from release evidence or the target-environment evidence manifest when
  the release is for a real target environment

## Verification

IP-17 is complete when:

- `scripts/release-evidence-bundle.sh --dry-run` emits the payload-safe bundle
  contract.
- strict mode generates the JSON, NDJSON, Markdown, and SHA-256 files when all
  seven required artifacts are present.
- [Production Release Checklists PI-07](production-release-checklists-pi07.md),
  [Testing Commands](testing.md), [Documentation Index](README.md), and
  [Folder Structure](folder-structure.md) point to the workflow.
- Unit tests cover the documentation wiring and generated bundle shape.
