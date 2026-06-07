# Target-Environment Evidence Hardening IP-04

Date started: 2026-06-07

Status: started; target evidence manifest contract is implemented, but real
target-environment evidence is not attached in this workspace yet.

## Purpose

IP-04 turns the post-GO target evidence gap into a strict attachment contract.
The historical EPR records stay intact: local pilot-equivalent evidence was
accepted for the v1.0.0 GO decision, while real target-environment deploy,
backup/restore, alert acknowledgement, benchmark, governance, and rollback
evidence still has to be attached after a target run.

This hardening slice defines the required manifest and verifier for that real
evidence. It does not claim target evidence exists before the target run has
produced it.

## Required Manifest

Use [target-environment-evidence-manifest.schema.json](target-environment-evidence-manifest.schema.json)
for the payload-safe manifest shape. A copyable starting point is
[target-environment-evidence-manifest.example.json](target-environment-evidence-manifest.example.json).

The manifest kind is:

```text
memorysystem.target_environment_evidence
```

Required top-level fields:

- release id, environment, target environment, image digest, and generated time
- managed database target reference without raw connection strings
- controlled `release_evidence_bucket` prefix or equivalent audit-store prefix
- `payloadSafe: true`
- release owner, rollback owner, alert-route owner, evidence owner, benchmark
  scorer, and governance reviewer

Required gates:

- `environment_preflight`
- `terraform_validation`
- `deployment_smoke`
- `metrics_and_tracing`
- `benchmark_scorecards`
- `governance_smoke`
- `backup_restore`
- `alert_receiver_acknowledgement`
- `evidence_upload`
- `rollback_notes`
- `go_no_go`

Each gate must have status `passed` and at least one local artifact with a
description, `payloadSafe: true`, and a `sha256:<64 hex chars>` hash.

## Verifier

Run the verifier before accepting target evidence:

```bash
./scripts/target-environment-evidence-verify.sh /path/to/target-environment-evidence-manifest.json
```

The verifier checks:

- manifest JSON, schema version, kind, and `payloadSafe`
- required target environment, database, evidence prefix, and owner fields
- every required gate appears exactly once with status `passed`
- alert receiver acknowledgement includes receiver, acknowledger, timestamp,
  and runbook
- rollback notes include previous image digest, rollback boundary,
  communication route, and rollback-owner signature
- final go/no-go includes `GO` or `NO-GO` plus release-owner and rollback-owner
  signatures
- every artifact path is local to the evidence bundle and its SHA-256 hash
  matches the manifest

The verifier never reads source event payloads, memory bodies, review notes,
queries, embeddings, provider payload bytes, API keys, raw database connection
strings, or secret values.

## Completion Rule

IP-04 can move from `Doing` to `Done` only after a real target-environment run
produces a payload-safe manifest that passes
`scripts/target-environment-evidence-verify.sh`, and the verified evidence
prefix is linked from the release evidence record.

Until then, product planning should describe IP-04 as in progress: the gate is
hardened, but the real target evidence is not attached.
