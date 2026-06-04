# GC-05 External Payload Retention Check

Date: 2026-06-01

Status: Implemented

## Purpose

GC-05 adds the first external payload-store retention check for source events
that carry `events.external_payload_uri`.

This is an operator evidence job, not a new payload storage feature. It does
not grant runtime access to external stores, download payload bytes, log object
URIs, delete provider objects, or clear database pointers. It checks whether
external payload pointers agree with PostgreSQL lifecycle state and emits
payload-safe evidence for operators.

## Command

Run audit-only inventory mode first:

```bash
MEMORYSYSTEM_EXTERNAL_PAYLOAD_CHECK_MODE=audit-only \
MEMORYSYSTEM_EXTERNAL_PAYLOAD_POLICY_MODE=disabled \
./scripts/platform-external-payload-retention-check.sh
```

Run provider verification only after the environment policy allows a provider
scheme and the runtime has read-only provider credentials:

```bash
MEMORYSYSTEM_EXTERNAL_PAYLOAD_CHECK_MODE=verify \
MEMORYSYSTEM_EXTERNAL_PAYLOAD_POLICY_MODE=allowed \
MEMORYSYSTEM_EXTERNAL_PAYLOAD_ALLOWED_SCHEMES=file,s3 \
./scripts/platform-external-payload-retention-check.sh
```

The multi-role container exposes the same job at:

```text
/app/scripts/platform-external-payload-retention-check.sh
```

## Selection Rules

The job reads only source events with a non-null `external_payload_uri`.
It does not select or export `events.content`.

For each target, it computes an expected external object state:

- active legal hold: expected present
- live, unexpired source event: expected present
- erased, redacted, minimized, or retention-expired source event: expected
  absent
- disabled policy mode: violation for every external payload pointer

Retention-expired checks use the configured ephemeral, standard, and audit
payload windows. Legal hold overrides retention expiry until the hold is
released.

## Provider Checks

The script contains provider-specific probes for:

- `file://` objects, using a local existence check
- `s3://` objects, using `aws s3api head-object` when the AWS CLI is available

Unsupported schemes, missing provider tools, unknown provider responses, policy
violations, and state mismatches are counted as failures in verify mode.

Audit-only mode records the inventory and policy outcome without probing
providers. This is the default runtime mode while external payload storage
remains disabled by the environment governance policy.

## Evidence

The job writes payload-safe evidence JSON with:

- `kind = memorysystem.external_payload_retention_check`
- `runId`
- `mode`
- `policyMode`
- `allowedSchemes`
- `targetEvents`
- `expectedPresent`
- `expectedAbsent`
- `verifiedPresent`
- `verifiedAbsent`
- `unverifiedTargets`
- `policyViolations`
- `unsupportedScheme`
- `stateMismatches`
- `probeFailures`
- `failureCount`
- `targetSetHash`

Default evidence path:

```text
/tmp/memorysystem-governance-evidence/external-payload-retention-evidence.json
```

The evidence omits raw external payload URIs, event payloads, provider
credentials, and payload bytes.

## Metrics

The job emits Prometheus-compatible metrics:

- `memorysystem_external_payload_retention_check_success`
- `memorysystem_external_payload_retention_check_targets`
- `memorysystem_external_payload_retention_expected_present`
- `memorysystem_external_payload_retention_expected_absent`
- `memorysystem_external_payload_retention_verified_present`
- `memorysystem_external_payload_retention_verified_absent`
- `memorysystem_external_payload_retention_unverified_targets`
- `memorysystem_external_payload_retention_policy_violations`
- `memorysystem_external_payload_retention_unsupported_scheme`
- `memorysystem_external_payload_retention_mismatches`
- `memorysystem_external_payload_retention_probe_failures`
- `memorysystem_external_payload_retention_failures`
- `memorysystem_external_payload_retention_timestamp_seconds`

Each metric is labeled with `environment`, `mode`, and `policy_mode`.

## Promotion Rule

Pilot or production may allow external payload stores only when:

- the environment governance policy is changed from `disabled`
- allowed schemes are explicitly listed
- provider credentials are read-only for verification jobs
- verify mode produces zero policy violations, probe failures, unsupported
  schemes, and state mismatches
- deletion evidence for expected-absent objects is retained in governance
  evidence

## Remaining Work

GC-06 now generates the compliance evidence package that links this evidence
with audit export, retention, erasure replay, backup/restore, release checklist,
benchmark, alert-route, and permission-drift evidence. GC-07 now adds the
governance/compliance admin console view over the package and related status
artifacts. GC-08 now adds the release smoke that exercises the full evidence
chain.
