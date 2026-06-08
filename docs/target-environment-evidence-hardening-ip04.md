# Target-Environment Evidence Hardening IP-04

Date started: 2026-06-07

Status: done for the accepted UAT target environment. The UAT manifest is
attached in
[UAT Target Evidence IP-04](release-evidence/uat-layout-fix-2026-06-08/ip04/README.md)
and passed `scripts/target-environment-evidence-verify.sh` on 2026-06-08.

## Purpose

IP-04 turns the post-GO target evidence gap into a strict attachment contract.
The historical EPR records stay intact: local pilot-equivalent evidence was
accepted for the v1.0.0 GO decision, and later target-environment deploy,
backup/restore, alert acknowledgement, benchmark, governance, and rollback
evidence has to be attached for whichever environment is accepted as the target.

This hardening slice defines the required manifest and verifier for that real
evidence. On 2026-06-08 the owner accepted UAT as the IP-04 target environment
for `uat-layout-fix-2026-06-08`; that UAT target run produced verified
payload-safe evidence. A later production-specific target claim still needs its
own production manifest and verifier pass if production is treated as a distinct
target environment.

## UAT Completion

The UAT evidence prefix is:

```text
docs/release-evidence/uat-layout-fix-2026-06-08/ip04/
```

The verified manifest is:

```text
docs/release-evidence/uat-layout-fix-2026-06-08/ip04/target-environment-evidence-manifest.json
```

Verifier output:

```text
Target environment evidence verification passed: releaseId=uat-layout-fix-2026-06-08 environment=uat artifacts=11
```

The UAT target evidence includes preflight, platform validation, deployment
smoke, metrics/tracing, benchmark scorecards, governance smoke, backup/restore,
alert acknowledgement, evidence upload receipt, rollback notes, and go/no-go
signoff. The manifest records `payloadSafe: true` and links only local artifacts
whose SHA-256 hashes were verified.

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

That rule is satisfied for the accepted UAT target by
[UAT Target Evidence IP-04](release-evidence/uat-layout-fix-2026-06-08/ip04/README.md).
