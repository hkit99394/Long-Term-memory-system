# Pilot Readiness Evidence Review

Date: 2026-06-01

Status: Local evidence reviewed; external pilot invite is not approved yet.

## Decision

Go for a target-environment pilot rehearsal.

No-go for inviting the first external pilot user until the release evidence is
rerun against the intended pilot environment and stored in the controlled
release evidence location.

## Review Scope

This review checks whether the project has enough evidence to invite the first
external pilot user. It uses the current local pilot-shaped evidence, the fresh
benchmark release gate run, the fresh GC-08 governance/compliance smoke, and the
PI-07 release checklist.

The review does not claim that AWS ECS, RDS, ECR, Secrets Manager or SSM,
platform alert receivers, or `release_evidence_bucket` have been exercised in a
real account.

## Fresh Evidence Run

| Evidence | Command | Result |
| --- | --- | --- |
| Benchmark release gate | `./scripts/benchmark-release-gate.sh --llm-scorecard benchmarks/release-gate/fixtures/llm-outcome-passing-scorecard.json --contract-scorecard benchmarks/release-gate/fixtures/agent-contract-passing-scorecard.json --agent-smoke benchmarks/release-gate/fixtures/agent-contract-smoke-passing.json --output-dir benchmarks/outputs/pilot-readiness` | Passed |
| Governance/compliance release smoke | `./scripts/governance-compliance-release-smoke.sh` | Passed |

The benchmark output was written to ignored local evidence files:

- `benchmarks/outputs/pilot-readiness/latest.json`
- `benchmarks/outputs/pilot-readiness/latest.md`

## Benchmark Gate Result

The fresh benchmark release gate passed with:

| Metric | Value |
| --- | ---: |
| Memory Lift | `+1.500` |
| Contract Lift | `+2.000` |
| Scoped-safety leak count | `0` |
| Stale-memory usage count | `0` |
| Source-link coverage | `1.000` |
| Agent-contract smoke | Passed, `2` tasks passed and `0` failed |

Caveat: this readiness pass used the checked-in release-gate fixtures, not a
fresh scorecard from the intended pilot model. The earlier PI-08 rehearsal used
filled local LR-03 scorecards and a full eight-task agent-contract smoke, but a
real external invite still needs fresh scorecards for the actual pilot model and
fixture set.

## Governance/Compliance Result

The fresh GC-08 smoke passed as a database-backed release smoke. It verifies:

- environment governance policy evidence
- permission-drift report generation
- audit export generation
- retention report and legal-hold summary generation
- retention minimization dry run
- external payload retention check
- erasure replay ledger export
- strict compliance evidence package creation

Caveat: the smoke used an isolated local PostgreSQL database and temporary local
evidence directories. The external pilot release still needs the same evidence
or platform equivalent from the intended pilot database and controlled evidence
storage.

## Release Evidence Bundle Review

| Release gate | Evidence reviewed | Result | Caveat |
| --- | --- | --- | --- |
| Migration | [Production Platform Rehearsal PI-08](production-platform-rehearsal-pi08.md) applied migrations through the migrator role. | Passed locally | Not yet proven in the real pilot account. |
| Deployment roles | PI-08 ran separate migrator, API, worker, and seeder roles. | Passed locally | Not yet proven on ECS/Fargate or equivalent. |
| Health | PI-08 verified `/health/live`, `/health/ready`, worker heartbeat, authenticated read, and write smoke before and after restore. | Passed locally | Needs target-environment health output. |
| Metrics | PI-08 ran operations metrics smoke before and after restore. | Passed locally | Needs target OpenTelemetry/exporter confirmation. |
| Benchmark gate | Fresh fixture-backed release gate passed in `benchmarks/outputs/pilot-readiness`. | Passed | Needs fresh pilot-model scorecards before external invite. |
| Governance/compliance | Fresh GC-08 smoke passed. | Passed locally | Needs target database/evidence-store equivalent. |
| Backup/restore | PI-08 created a custom-format backup, restored to a fresh database, validated table counts, and checked pgvector. | Passed locally | Needs managed backup/PITR or platform backup evidence. |
| Rollback | PI-08 stopped API/worker, restored backup, and restarted against the restored database. | Passed locally | Needs named rollback owner and target rollback runbook evidence. |
| Alert routing | PI-08 validated alert artifacts and route-test definitions. | Passed as artifact smoke | Needs real receiver acknowledgement for the pilot route. |
| Audit evidence | PI-08 retained local command output and work directory notes; GC-08 generated strict local compliance evidence during the test. | Partial | Needs upload to `release_evidence_bucket` or equivalent controlled audit store. |

## Go/No-Go

No-go for inviting the first external pilot user today.

The project is technically close: core runtime, enterprise access, context
productization, platform rehearsal, benchmark gates, and governance/compliance
smoke all have local proof. The remaining blocker is not a code feature; it is
target-environment evidence.

External invite can move to go when all of these are true:

1. The production-pilot deployment smoke or platform equivalent runs against
   the intended pilot account/database.
2. The benchmark release gate uses fresh scorecards for the intended pilot model
   and fixture set.
3. The GC-08 governance/compliance smoke or platform equivalent produces strict
   evidence from the intended pilot database and evidence location.
4. Alert route tests have real receiver acknowledgement for the pilot route.
5. Backup/PITR or backup/export plus restore validation evidence is attached.
6. The release evidence bundle is uploaded to `release_evidence_bucket` or an
   equivalent controlled audit store.
7. A named rollback owner signs the pilot go/no-go record.

## Next Move

Run a target-environment pilot rehearsal. Use the
[Target-Environment Pilot Rehearsal P0](target-environment-pilot-rehearsal-p0.md)
as the final pre-invite release candidate: deploy the migrator/API/worker shape
to the intended pilot environment, rerun the benchmark gate with fresh
pilot-model scorecards, rerun GC-08 or its platform equivalent, upload the
evidence bundle, verify alert receiver acknowledgement, and then make the
external-user go/no-go decision.
