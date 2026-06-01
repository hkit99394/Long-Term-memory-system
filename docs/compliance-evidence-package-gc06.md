# GC-06 Compliance Evidence Package

Date: 2026-06-01

Status: Implemented

## Purpose

GC-06 adds a repeatable operator command that packages compliance evidence into
one payload-safe manifest. The package links the evidence produced by
governance, backup/restore, platform release, benchmark, and observability
workflows without copying raw payload content.

This is an evidence aggregation job, not a new authorization path. It does not
read source event payloads, memory bodies, review notes, queries, embeddings,
provider payload bytes, API keys, or secret values.

## Command

Create a draft package first:

```bash
MEMORYSYSTEM_COMPLIANCE_EVIDENCE_PACKAGE_MODE=draft \
./scripts/platform-compliance-evidence-package.sh
```

Run strict mode before pilot or production promotion:

```bash
MEMORYSYSTEM_COMPLIANCE_EVIDENCE_PACKAGE_MODE=strict \
MEMORYSYSTEM_COMPLIANCE_OPERATOR_ID=platform-release-owner \
MEMORYSYSTEM_COMPLIANCE_RELEASE_ID=pilot-2026-06-01 \
./scripts/platform-compliance-evidence-package.sh
```

The multi-role container exposes the same job at:

```text
/app/scripts/platform-compliance-evidence-package.sh
```

## Linked Evidence

The package links these required artifacts:

- audit export
- retention report
- legal hold summary
- permission-drift report
- retention minimization evidence
- external payload retention evidence
- erasure replay evidence
- backup export evidence
- restore validation evidence
- release checklist evidence
- benchmark release-gate report
- alert-route smoke result

Each artifact entry records:

- stable artifact id
- artifact type
- required flag
- present or missing status
- file path
- byte count
- SHA-256 hash when the artifact is present

The command never embeds artifact file contents in the package manifest.

## Modes

Draft mode:

- creates the manifest even when required artifacts are missing
- records missing counts and hashes for present files
- is useful for local, CI, or preflight evidence review

Strict mode:

- requires every listed evidence artifact to exist
- fails when any required artifact is missing
- still writes a payload-safe failed manifest and metrics

## Outputs

The command writes:

- JSON manifest:
  `/tmp/memorysystem-compliance-evidence/<package-id>.json`
- NDJSON artifact index:
  `/tmp/memorysystem-compliance-evidence/<package-id>-artifacts.ndjson`
- deterministic SHA-256 sidecar:
  `/tmp/memorysystem-compliance-evidence/<package-id>.json.sha256`
- Prometheus-compatible metrics:
  `/tmp/memorysystem-compliance-evidence/compliance-evidence-package-metrics.prom`

The manifest uses:

```text
kind = memorysystem.compliance_evidence_package
```

The evidence omits raw source payloads, memory bodies, review notes, raw
queries, embedding inputs, provider credentials, and payload bytes.

## Metrics

The job emits Prometheus-compatible metrics:

- `memorysystem_compliance_evidence_package_success`
- `memorysystem_compliance_evidence_package_artifacts`
- `memorysystem_compliance_evidence_package_present_artifacts`
- `memorysystem_compliance_evidence_package_missing_artifacts`
- `memorysystem_compliance_evidence_package_missing_required_artifacts`
- `memorysystem_compliance_evidence_package_manifest_bytes`
- `memorysystem_compliance_evidence_package_timestamp_seconds`

Each metric is labeled with `environment` and `mode`.

## Promotion Rule

Pilot or production promotion requires strict mode to pass with:

- zero missing required artifacts
- a non-empty JSON manifest
- a non-empty NDJSON artifact index
- a SHA-256 sidecar for the JSON manifest
- metrics emitted to the platform evidence path

Operators should archive the package manifest, artifact index, hash sidecar,
and metrics file with the release evidence.

## Remaining Work

GC-07 should add a governance/compliance admin console view that links to the
payload-safe evidence package, retention, erasure replay, legal hold,
permission-drift, and external payload check status.
