# Target-Environment Pilot Rehearsal P0

Date created: 2026-06-04

Status: planned; execution is pending the intended pilot environment.

## Purpose

This runbook turns the pilot readiness no-go into an executable P0 release
gate. The technical MVP and local production-pilot rehearsal are complete, but
the first external pilot invite remains blocked until target-environment
evidence proves the migrator, API, worker, benchmark, governance/compliance,
backup/restore, alert routing, audit evidence, and rollback paths in the real
pilot environment.

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
| 9 | Evidence upload | Upload migration, health, metrics, benchmark, governance/compliance, backup/restore, alert routing, approval, and rollback-owner evidence to the controlled audit store. | Immutable evidence prefix and retention policy. |
| 10 | Go/no-go | Have the release owner and named rollback owner sign the final decision. | Signed go/no-go record with decision, timestamp, rollback boundary, and communication route. |

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

## Current P0 State

This runbook completes the repo-side planning artifact for P0. The external
pilot invite remains blocked until the checklist is executed against the
intended pilot environment and the go/no-go record is signed.
