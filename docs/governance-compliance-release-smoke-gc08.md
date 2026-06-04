# GC-08 Governance/Compliance Release Smoke

GC-08 closes the governance/compliance implementation track with one repeatable
release smoke command:

```bash
./scripts/governance-compliance-release-smoke.sh
```

The smoke runs a database-backed integration test against an isolated
PostgreSQL database. It creates a small governed project fixture, emits the
required payload-safe evidence inputs, runs the existing platform evidence
scripts, then builds the GC-06 compliance evidence package in strict mode.

## Covered Checks

| Check | Evidence |
| --- | --- |
| Policy config | Generates an environment governance policy evidence record with `local_access_records_only` as the authorization boundary. |
| Permission drift | Calls `POST /api/admin/access/permission-drift` through the authenticated API and writes `permission-drift-report.json`. |
| Audit export | Calls `POST /api/admin/audit-exports` and writes scoped NDJSON audit evidence. |
| Retention report | Calls `GET /api/admin/governance/retention-report` for the isolated project. |
| Legal hold summary | Calls `GET /api/admin/governance/legal-holds?status=active`. |
| Retention dry run | Runs `scripts/platform-retention-minimization.sh` in `dry-run` mode against the isolated database. |
| External payload retention | Runs `scripts/platform-external-payload-retention-check.sh` in payload-safe audit mode. |
| Erasure replay | Runs `scripts/platform-erasure-replay-ledger-export.sh` and verifies replay ledger evidence exists. |
| Evidence manifest | Runs `scripts/platform-compliance-evidence-package.sh` in `strict` mode and requires every GC-06 artifact to be present. |

The smoke uses existing application authorization and governance paths. It does
not read or print raw event payloads, memory fact bodies, review notes, external
payload URIs, API keys, identity subjects, issuer values, or service credential
fingerprints.

## Operator Inputs

The command uses the same database test discovery as the rest of the integration
suite:

- `MEMORYSYSTEM_TEST_POSTGRES_CONNECTION_STRING`, when set
- otherwise the local Docker Compose PostgreSQL on `127.0.0.1:55432`, when
  reachable

Set `MEMORYSYSTEM_GOVERNANCE_COMPLIANCE_SMOKE_CONFIGURATION` to override the
test configuration. The default is `Release`.

## Verification

GC-08 is covered by:

- `GovernanceComplianceReleaseSmokeContractTests`
- `GovernanceComplianceReleaseSmokeTests`
- shell syntax validation for
  `scripts/governance-compliance-release-smoke.sh`

Run:

```bash
bash -n scripts/governance-compliance-release-smoke.sh
./scripts/governance-compliance-release-smoke.sh
```

## Next Move

With GC-08 complete, the governance/compliance gate has executable local proof
for policy, drift, retention, erasure replay, audit export, and strict evidence
packaging. The 2026-06-01 pilot readiness evidence review records a no-go for
external invite until target-environment evidence is produced. EPR-02 and
EPR-03 now attach local pilot-equivalent evidence, and EPR-04 records the current
external-pilot decision as NO-GO until controlled evidence, real alert
acknowledgement, rollback boundary, communication route, and owner signatures
are attached.
