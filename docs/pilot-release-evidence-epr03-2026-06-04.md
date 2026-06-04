# Pilot Release Evidence EPR-03

Date: 2026-06-04

Status: EPR-02 and EPR-03 local pilot-equivalent evidence attached; external
pilot invite remains blocked until EPR-04 is signed.

## Scope

This record captures the fresh local pilot-equivalent evidence generated for
EPR-02 and EPR-03. It proves the production-pilot deployment shape and release
gates against isolated local PostgreSQL targets in the current workspace. It is
payload-safe: raw smoke responses and database dumps stay in local ignored or
temporary evidence locations, while this document records commands, results,
paths, and hashes.

This is not yet a signed external-pilot go decision. The real pilot account,
controlled audit-store upload, real alert receiver acknowledgement, and named
rollback-owner signature remain EPR-04 inputs.

## Evidence Commands

| Evidence | Command | Result |
| --- | --- | --- |
| Deployment smoke | `MEMORYSYSTEM_PRODUCTION_PILOT_SMOKE_KEEP_WORK_DIR=true ./scripts/production-pilot-deployment-smoke.sh` | Passed |
| Benchmark release gate | `./scripts/benchmark-release-gate.sh --llm-scorecard benchmarks/release-gate/fixtures/llm-outcome-passing-scorecard.json --contract-scorecard benchmarks/release-gate/fixtures/agent-contract-passing-scorecard.json --agent-smoke benchmarks/release-gate/fixtures/agent-contract-smoke-passing.json --output-dir benchmarks/outputs/epr03-pilot-release` | Passed |
| Governance/compliance release smoke | `./scripts/governance-compliance-release-smoke.sh` | Passed |
| Observability artifact smoke | `./scripts/observability-artifacts-smoke.sh` | Passed |
| Live agent-contract smoke | `MEMORYSYSTEM_API_BASE_URL=http://127.0.0.1:5099 MEMORYSYSTEM_BENCHMARK_API_KEY=private-alpha-local-key python3 benchmarks/agent-contract-usefulness-v1/run_smoke.py --output benchmarks/outputs/epr03-pilot-release/agent-contract-live-smoke.run.json` | Passed |

## Deployment Smoke Result

| Gate | Result |
| --- | --- |
| Smoke database | `memorysystem_pilot_smoke_20260604200651_44471` |
| Restore database | `memorysystem_pilot_restore_20260604200651_44471` |
| Migrator role | Applied `28` migrations to the smoke database, then skipped `28` already-applied migrations after restore. |
| API role | `/health/live` and `/health/ready` passed before and after restore. |
| Worker role | Outbox unfinished jobs reached `0`; deterministic memory embeddings reached `6`; restored worker heartbeat reported `running`. |
| Authenticated paths | Operations summary, memory search for `SQL-first`, event write, and event read passed before and after restore. |
| Metrics | `scripts/operations-metrics-smoke.sh` passed before and after restore with required API alert-input metrics present. |
| Backup/restore | Custom-format PostgreSQL backup restored into a fresh database; restore-validation table counts matched; pgvector extension was verified. |
| Local evidence directory | `/var/folders/py/6g0jns_s78vg8tzpvpdtxn9c0000gn/T/memorysystem-pilot-smoke.8ICukJ` |

The local work directory contains payload-bearing smoke responses and local logs,
so it is not committed. Keep it only as temporary operator evidence.

## Release Gate Result

| Metric | Value |
| --- | ---: |
| Memory Lift | `+1.500` |
| Contract Lift | `+2.000` |
| Scoped-safety leak count | `0` |
| Stale-memory usage count | `0` |
| Source-link coverage | `1.000` |
| Fixture-backed agent-contract smoke | Passed, `2` tasks passed and `0` failed |
| Fresh live agent-contract smoke | Passed, `8` tasks passed and `0` failed |

The benchmark release-gate output is written to ignored local files:

- `benchmarks/outputs/epr03-pilot-release/latest.json`
- `benchmarks/outputs/epr03-pilot-release/latest.md`
- `benchmarks/outputs/epr03-pilot-release/agent-contract-live-smoke.run.json`

The release gate still uses checked-in scorecard fixtures for the LLM-scored
metrics. The live smoke is fresh tool-response evidence for the LMSS v1 contract,
including ACU-003 contradiction-overlay handling.

## Governance, Compliance, And Observability

| Gate | Result |
| --- | --- |
| Governance/compliance release smoke | Passed `GovernanceComplianceReleaseSmokeTests` in Release configuration. |
| Evidence package coverage | Policy config, permission drift, audit export, retention, legal hold, erasure replay, external payload checks, and strict compliance evidence packaging were exercised by the release smoke. |
| Observability artifact smoke | Passed with `43` API metric inputs, `51` external metric inputs, `43` alert rules, `3` alert routes, `13` dashboard panels, and `13` trace spans. |
| Alert receiver acknowledgement | Artifact route validation passed; real receiver acknowledgement is not available in this workspace and remains an EPR-04 go/no-go input. |

## Artifact Hashes

| Artifact | SHA-256 |
| --- | --- |
| `benchmarks/outputs/epr03-pilot-release/latest.json` | `827f18dc3615c2413398ff3ffe67700510c2b764b99dd08c95ca5cb6a2124110` |
| `benchmarks/outputs/epr03-pilot-release/latest.md` | `e4cf4adc58b6f7080691b150f8182a4ed960c193fc64a76c00b083f979990b59` |
| `benchmarks/outputs/epr03-pilot-release/agent-contract-live-smoke.run.json` | `8f50d4f212ace31acf8b4f0b61f8d4f5ee86a0c4470aeb2fbcf64ea3bbadfd04` |
| `/var/folders/py/6g0jns_s78vg8tzpvpdtxn9c0000gn/T/memorysystem-pilot-smoke.8ICukJ/memorysystem_pilot_smoke_20260604200651_44471.dump` | `73c6d02d8d2a7140c62ed294d64a9cb2553398b32cdaec4f40d379fb46d6d305` |

## EPR State

EPR-02 is complete for the local pilot-equivalent rehearsal: migrator, API,
worker, health, metrics, authenticated read/write, backup, restore, pgvector,
and rollback validation all passed.

EPR-03 is complete for local payload-safe release evidence: benchmark gate,
fresh live agent-contract smoke, governance/compliance release smoke,
backup/restore evidence, and alert artifact validation are attached here.

EPR-04 remains the external-pilot blocker. Before inviting an external user, the
release owner and rollback owner must sign the final go/no-go record with the
controlled evidence prefix, real alert receiver acknowledgement, rollback
boundary, and communication route.
