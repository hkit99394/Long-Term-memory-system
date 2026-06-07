# Target-Environment Pilot Rehearsal P0

Date created: 2026-06-04

Status: local pilot-equivalent evidence accepted for version 1.0.0 GO; target
evidence remains post-GO hardening.

## Purpose

This runbook turns pilot readiness into an executable P0 release gate. The
technical MVP and local production-pilot rehearsal are complete, and version
1.0.0 is owner-approved for external pilot. Target-environment evidence for the
migrator, API, worker, benchmark, governance/compliance, backup/restore, alert
routing, audit evidence, and rollback paths remains post-GO hardening work.

This document is the acceptance record for that target-environment rehearsal. It
must be filled during the run and linked from the final go/no-go record before
the first external pilot user is invited.

## Required Inputs

| Input | Required value |
| --- | --- |
| Pilot environment | Environment name, account or subscription id, region, and network boundary. |
| Image artifact | Immutable OCI image digest for the API, worker, migrator, seeder, and platform jobs. |
| Database target | Managed PostgreSQL endpoint, database name, pgvector validation path, backup/PITR settings, and restore-validation target. |
| Secret references | Authentication, PostgreSQL, restore-validation, embedding provider, and any platform job secret references. |
| Evidence location | `release_evidence_bucket` prefix or equivalent controlled audit-store location. |
| Owners | Release owner, rollback owner, alert-route owner, evidence owner, benchmark scorer, and governance reviewer. |
| Benchmark inputs | Pilot model name, fixture set, LLM outcome scorecard path, agent-contract scorecard path, and agent-contract smoke output path. |
| Alert route | Receiver destination, route-test metric, runbook link, owner acknowledgement path, and silence policy. |

## Target Evidence Manifest

[Target-Environment Evidence Hardening IP-04](target-environment-evidence-hardening-ip04.md)
adds the machine-readable manifest and verifier for the real target run. Before
accepting target evidence, create a
`memorysystem.target_environment_evidence` manifest that follows
[target-environment-evidence-manifest.schema.json](target-environment-evidence-manifest.schema.json)
and run:

```bash
./scripts/target-environment-evidence-verify.sh /path/to/target-environment-evidence-manifest.json
```

The verifier requires every deploy smoke, metrics/tracing, benchmark,
governance, backup/restore, alert acknowledgement, evidence upload, rollback,
and go/no-go artifact to be listed as payload-safe local evidence with a
matching SHA-256 hash before the controlled audit-store prefix is accepted.

## Execution Checklist

| Step | Gate | Required action | Evidence |
| --- | --- | --- | --- |
| 1 | Environment preflight | Confirm target environment, VPC/subnet or equivalent network inputs, secret references, image digest, evidence prefix, alert receiver, and named rollback owner. | Preflight record in the evidence prefix. |
| 2 | Terraform validation | Run `terraform -chdir=infra/terraform/environments/pilot validate` or the platform equivalent for the exact pilot inputs. | Validation output and Terraform input summary with no secret values. |
| 3 | Deployment smoke | Run `scripts/production-pilot-deployment-smoke.sh` or the platform equivalent against the intended pilot account/database, using separate migrator, API, and worker roles. | Migrator task output, API/worker task ids, health output, authenticated read/write smoke, and worker heartbeat. |
| 4 | Metrics and tracing | Verify `/api/operations/summary`, `/api/operations/metrics`, OpenTelemetry exporter status, platform metrics, and required alert inputs. | Metrics snapshot, exporter status, dashboard or trace coverage evidence. |
| 5 | Benchmark gate | Run `scripts/benchmark-release-gate.sh` with fresh scorecards for the intended pilot model and fixture set. | Benchmark JSON/Markdown report showing Memory Lift, Contract Lift, scoped-safety leaks, stale-memory usage, source-link coverage, and agent-contract smoke. |
| 6 | Governance/compliance | Run `scripts/governance-compliance-release-smoke.sh` or the platform equivalent against the pilot database and evidence target. | Strict compliance evidence package, artifact index, hash sidecar, policy evidence, audit export, retention report, erasure replay, and permission-drift output. |
| 7 | Backup/restore | Attach managed backup/PITR status or run backup/export plus restore-to-new-database validation. | Backup evidence JSON, restore validation evidence JSON, backup/restore metrics, and pgvector validation output. |
| 8 | Alert receiver acknowledgement | Fire or simulate the pilot route test and confirm the real receiver acknowledges it. | Alert route-test result, receiver acknowledgement, owner, destination, and runbook link. |
| 9 | Evidence upload | Upload migration, health, metrics, benchmark, governance/compliance, backup/restore, alert routing, approval, and rollback-owner evidence to the controlled audit store; verify the target evidence manifest before accepting the prefix. | Immutable evidence prefix, retention policy, target evidence manifest, and `target-environment-evidence-verify.sh` output. |
| 10 | Go/no-go | Have the release owner and named rollback owner sign the final decision. | Signed go/no-go record with decision, timestamp, rollback boundary, communication route, and verified manifest path. |

## Pass Criteria

The target-environment rehearsal passes only when all of these are true:

- migrator, API, and worker roles run in the intended pilot environment
- readiness, worker heartbeat, authenticated read, and write smoke pass
- benchmark gate passes with zero scoped-safety leaks and zero stale-memory usage
- governance/compliance smoke or platform equivalent produces strict evidence
- backup/PITR or backup/export plus restore validation evidence is attached
- alert route test has real receiver acknowledgement
- evidence is stored under `release_evidence_bucket` or equivalent controlled audit store
- named rollback owner signs the go/no-go record

## Fail And Rollback Criteria

The rehearsal fails and external invite remains blocked when any of these occur:

- target environment cannot be identified or evidence location is missing
- migration fails, skips unexpectedly, or leaves schema state unclear
- API readiness, worker heartbeat, authenticated smoke, or write path fails
- benchmark gate fails any safety threshold
- governance/compliance evidence package is missing required artifacts
- backup/restore validation cannot prove a recoverable database state
- alert route test does not reach a real receiver
- evidence cannot be uploaded to the controlled audit store
- rollback owner does not sign the go/no-go record

## Go/No-Go Record Template

```text
Target-environment pilot rehearsal id:
Date/time UTC:
Pilot environment:
Image digest:
Database target:
Evidence prefix:
Evidence manifest:
Release owner:
Rollback owner:
Alert-route owner:
Benchmark scorer:
Governance reviewer:

Gate results:
- Environment preflight:
- Terraform/platform validation:
- Deployment smoke:
- Metrics and tracing:
- Benchmark gate:
- Governance/compliance:
- Backup/restore:
- Alert receiver acknowledgement:
- Evidence upload:

Decision: GO | NO-GO
Decision reason:
Rollback boundary:
Communication route:
Release owner signature:
Rollback owner signature:
```

## Evidence Attached

[Pilot Release Evidence EPR-03](pilot-release-evidence-epr03-2026-06-04.md)
records the 2026-06-04 local pilot-equivalent evidence for EPR-02 and EPR-03:

- `scripts/production-pilot-deployment-smoke.sh` passed with separate migrator,
  API, worker, backup/restore, pgvector, metrics, authenticated read/write, and
  rollback validation.
- `scripts/benchmark-release-gate.sh` passed with Memory Lift `+1.500`,
  Contract Lift `+2.000`, zero scoped-safety leaks, zero stale-memory usage, and
  source-link coverage `1.000`.
- `benchmarks/agent-contract-usefulness-v1/run_smoke.py` passed all `8` live
  LMSS v1 tool-response tasks, including ACU-003 contradiction-overlay handling.
- `scripts/governance-compliance-release-smoke.sh` passed the strict
  governance/compliance evidence smoke.
- `scripts/observability-artifacts-smoke.sh` passed artifact validation for
  metric inputs, alert rules, alert routes, dashboard panels, and trace spans.

This evidence is payload-safe and local. The raw smoke responses, generated
benchmark outputs, and backup dump are local or ignored artifacts; they are not
committed.

## Go/No-Go Attached

[External Pilot GO EPR-04 v1.0.0](external-pilot-go-epr04-v1.0.0-2026-06-04.md)
records the current external-pilot decision as GO. The historical
[External Pilot Go/No-Go EPR-04](external-pilot-go-no-go-epr04-2026-06-04.md)
record remains the prior NO-GO audit trail.

## Machine-Readable Status

[Release Readiness Status Contract P2](release-readiness-status-contract-p2.md)
and `external-pilot-readiness-status.json` centralize the current EPR gate
statuses, evidence links, current GO decision, payload-safety state, and next
recommended work for tests, future CI, and the P3 operator cockpit.

## Current P0 State

EPR-02 and EPR-03 have local pilot-equivalent evidence attached. EPR-04 now has
an owner-approved version 1.0.0 GO replacement. Controlled target evidence, real
alert receiver acknowledgement, rollback boundary, and communication route
remain post-GO hardening work.
[Target-Environment Evidence Hardening IP-04](target-environment-evidence-hardening-ip04.md)
now defines the manifest and checksum verifier for attaching that real target
evidence, but the target evidence bundle is not attached in this workspace yet.
