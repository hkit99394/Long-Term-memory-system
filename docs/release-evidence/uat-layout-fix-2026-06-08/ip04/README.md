# UAT Target Evidence IP-04

Release: `uat-layout-fix-2026-06-08`

Environment: `uat`

Status: verified GO for the accepted UAT target environment.

This release evidence package closes IP-04 for the UAT target. It records the
UAT production-compose stack as the target environment, verifies all required
target gates, and keeps raw payloads and secrets out of committed evidence.

## Verification

```text
Target environment evidence verification passed: releaseId=uat-layout-fix-2026-06-08 environment=uat artifacts=11
```

Verifier command:

```bash
./scripts/target-environment-evidence-verify.sh docs/release-evidence/uat-layout-fix-2026-06-08/ip04/target-environment-evidence-manifest.json
```

## Scope

This is a UAT target-environment completion. It does not claim that a separate
managed production cloud environment has been rehearsed. If production is later
treated as a distinct target environment, production should run its own target
evidence manifest and verifier pass before that production-specific claim is
made.

## Evidence

- [Target manifest](target-environment-evidence-manifest.json)
- [Verifier output](verifier-output.txt)
- [Environment preflight](evidence/environment-preflight.json)
- [Platform validation](evidence/platform-validation.json)
- [Deployment smoke](evidence/deployment-smoke.json)
- [Metrics and tracing](evidence/metrics-and-tracing.json)
- [Benchmark scorecards](evidence/benchmark-scorecards.json)
- [Governance smoke](evidence/governance-smoke.json)
- [Backup/restore](evidence/backup-restore.json)
- [Alert acknowledgement](evidence/alert-acknowledgement.json)
- [Evidence upload receipt](evidence/evidence-upload-receipt.json)
- [Rollback notes](evidence/rollback-notes.md)
- [Go/no-go](evidence/go-no-go.md)
