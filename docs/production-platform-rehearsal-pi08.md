# Production Platform Rehearsal PI-08

Date: 2026-06-01

Status: Passed local isolated rehearsal

## Purpose

PI-08 runs the first production-pilot platform rehearsal against an isolated
local PostgreSQL database. The rehearsal proves that the documented
migrator/API/worker split can deploy, pass health checks, process Scenario 0001,
collect required metrics, validate backup/restore, and restart against a
restored database as a rollback rehearsal.

This is a local rehearsal of the production-pilot shape. It is not a real AWS
pilot deployment and does not replace a later account-level rehearsal with ECS,
RDS, ECR, Secrets Manager or SSM, and platform alert receivers.

## Command

The first sandboxed attempt could not reach the Docker daemon. The rehearsal was
rerun outside the sandbox because Docker access is required.

```bash
MEMORYSYSTEM_PRODUCTION_PILOT_SMOKE_KEEP_WORK_DIR=true \
  ./scripts/production-pilot-deployment-smoke.sh
```

Additional release-gate and alert-routing evidence:

```bash
./scripts/benchmark-release-gate.sh \
  --llm-scorecard benchmarks/outputs/llm-outcome-v0/scorecard-dry-run.json \
  --contract-scorecard benchmarks/outputs/agent-contract-usefulness-v1/scorecard-lr03-local.json \
  --agent-smoke benchmarks/outputs/agent-contract-usefulness-v1/latest-smoke.run.json \
  --output-dir benchmarks/outputs/release-gates

./scripts/observability-artifacts-smoke.sh
```

## Rehearsal Inputs

| Input | Value |
| --- | --- |
| Scenario | Scenario 0001 private-alpha demo data |
| Pilot database | `memorysystem_pilot_smoke_20260601003823_9418` |
| Restore database | `memorysystem_pilot_restore_20260601003823_9418` |
| API base URL | `http://127.0.0.1:5199` |
| Work directory | `/var/folders/py/6g0jns_s78vg8tzpvpdtxn9c0000gn/T//memorysystem-pilot-smoke.wbSY5C` |
| Backup file | `/var/folders/py/6g0jns_s78vg8tzpvpdtxn9c0000gn/T//memorysystem-pilot-smoke.wbSY5C/memorysystem_pilot_smoke_20260601003823_9418.dump` |
| Rollback owner | Local operator for this Codex run |

The kept work directory contains payload-bearing smoke responses and local logs,
so it is evidence for this local run only. A real pilot release should upload
payload-safe evidence to `release_evidence_bucket` or another controlled audit
store.

## Checklist Result

| Gate | Result | Evidence |
| --- | --- | --- |
| Migration | Passed | Migrator role applied 25 migrations to the isolated pilot database. Restore rerun skipped the same 25 already-applied migrations. |
| Deployment roles | Passed | Published and ran separate migrator, API, worker, and demo seeder outputs from the same working tree. |
| Health | Passed | API `/health/live` and `/health/ready` returned success before and after restore; restored worker heartbeat reported `running`. |
| Worker processing | Passed | Scenario 0001 seeded 6 pending outbox jobs; worker processed them to zero unfinished jobs and created 6 memory embeddings. |
| Authenticated smoke | Passed | Authenticated operations summary, memory search for `SQL-first`, event write, and event read passed on the pilot database and again on the restored database. |
| Metrics | Passed | `scripts/operations-metrics-smoke.sh` passed before and after restore, proving all required API alert-input metrics were present. |
| Alert routing | Passed | `scripts/observability-artifacts-smoke.sh` passed with 43 API metric inputs, 14 external metric inputs, 36 alert rules, 3 alert routes, 13 dashboard panels, and 13 trace spans. |
| Benchmark gate | Passed | Release gate passed with Memory Lift `+1.575`, Contract Lift `+2.025`, scoped-safety leak count `0`, stale-memory usage `0`, and source-link coverage `1.000`. |
| Backup/restore | Passed | Custom-format backup was created, inspected with `pg_restore --list`, restored into a fresh rollback database, and validated by table counts plus pgvector extension check. |
| Rollback rehearsal | Passed | API and worker were stopped, the backup was restored into a fresh database, then API and worker restarted successfully against the restored database. |
| Audit evidence | Partial local evidence | Command output, kept work directory, benchmark outputs, and observability smoke output exist locally. External pilot still needs durable evidence upload. |

## Restore Validation Snapshot

The restore validation compared source and restored row counts for the central
restore manifest:

| Table | Restored row count |
| --- | ---: |
| `schema_migrations` | 25 |
| `principals` | 2 |
| `organizations` | 1 |
| `projects` | 2 |
| `organization_memberships` | 1 |
| `project_memberships` | 1 |
| `role_assignments` | 2 |
| `memory_access_grants` | 7 |
| `events` | 6 |
| `memory_facts` | 4 |
| `role_memory_lenses` | 2 |
| `memory_chunks` | 6 |
| `memory_embeddings` | 6 |
| `memory_reviews` | 0 |
| `memory_redactions` | 0 |
| `api_idempotency_keys` | 1 |
| `outbox_jobs` | 6 |
| `vault_exports` | 0 |
| `worker_heartbeats` | 1 |
| `memory_retrieval_feedback` | 0 |
| `governance_legal_holds` | 0 |
| `governance_legal_hold_events` | 0 |
| `memory_context_packets` | 0 |

`pgvector` extension validation passed.

## Release Decision

PI-08 passes for the local production-pilot rehearsal. The platform shape is
credible enough to move out of the `PI-*` implementation track and start the
enterprise access implementation track.

## Caveats

- This did not create or mutate real AWS resources.
- The benchmark gate used the existing filled local scorecards and existing
  full agent-contract smoke artifact, not a newly scored external pilot model.
- Alert routing was validated as checked-in artifacts and route tests, not by a
  real alert receiver acknowledgement.
- The local evidence directory includes smoke payload responses and should not
  be treated as production audit storage.

## Next Move

Start `EA-01`: add the identity-binding schema. That is the next highest-value
pilot blocker because the platform can now rehearse deployment and rollback,
but external pilot users still need governed identity mapping before OIDC or SSO
can be enabled.

Recommended sequence:

1. `EA-01`: add identity-binding schema, uniqueness/status constraints, lookup
   repository, and migration tests.
2. `EA-02`: introduce shared principal resolution so API keys and future OIDC
   produce the same internal result.
3. `EA-03`: add payload-safe access audit events before enabling new auth
   methods.
4. Keep `DM-06` as a cleanup slice after the enterprise access foundation is
   stable.
