# GC-01 Environment Governance Policy Contract

Date: 2026-06-01

Status: Implemented

## Goal

GC-01 defines the first environment governance policy contract for `local`,
`ci`, `pilot`, and `production`. The contract records where governed data may
live, how long payloads and evidence are retained, whether external payload
stores are allowed, how exceptions are approved, and where payload-safe evidence
is stored.

This slice does not add runtime enforcement, schema changes, endpoints,
workers, or admin UI. Runtime authorization remains unchanged and must keep
using local memberships, role assignments, namespace grants, and
effective-access previews.

The contract is represented in code by
`MemorySystem.Application.Governance.EnvironmentGovernancePolicy`.

## Authorization Boundary

Every policy must set:

```text
authorizationBoundary = local_access_records_only
```

The governance policy can constrain deployment evidence and future compliance
automation. It must not grant memory read, write, review, or admin permission.
Any later enforcement work must still call the existing authorizer and access
management stores.

## Required Environments

| Environment | Purpose | Evidence location rule |
| --- | --- | --- |
| `local` | Developer workstation verification. | Local artifact paths are allowed. |
| `ci` | Reproducible automated checks. | CI artifact locations are allowed. |
| `pilot` | First production-shaped customer or operator environment. | Evidence must use controlled object or audit stores, not local artifact paths. |
| `production` | Production service environment. | Evidence must use controlled object or audit stores, not local artifact paths. |

## Required Policy Shape

| Field | Required shape |
| --- | --- |
| `environment` | One of `local`, `ci`, `pilot`, or `production`. |
| `version` | Stable contract version such as `governance-policy.v1`. |
| `authorizationBoundary` | Must be `local_access_records_only`. |
| `allowedRegions` | Runtime, database, backups, telemetry, and evidence locations. |
| `dataClasses` | Residency mapping for governed data classes. |
| `retentionWindows` | Environment retention windows for all retention classes. |
| `externalPayloadPolicy` | External payload-store mode and evidence reference. |
| `exceptionRecords` | Payload-safe, expiring exception records; present even when empty. |
| `evidenceLocations` | Payload-safe evidence targets for release, governance, backup/restore, and audit export. |

## Residency Areas

Policies must include these residency areas:

| Area | Meaning |
| --- | --- |
| `runtime` | API, worker, migrator, and scheduled job runtime location. |
| `database` | PostgreSQL primary data location. |
| `backups` | Backup export, PITR, restore-validation, and backup evidence location. |
| `telemetry` | Logs, metrics, traces, dashboards, and alert-route evidence location. |
| `evidence` | Release, governance, compliance, benchmark, and audit evidence location. |

## Data Classes

Policies must map all governed data classes to allowed residency areas and
regions:

| Data class | Typical residency areas |
| --- | --- |
| `event_payloads` | `database`, `backups` |
| `memory_facts` | `database`, `backups` |
| `embeddings` | `database`, `backups` |
| `exports` | `database`, `evidence` |
| `audit_rows` | `database`, `evidence` |
| `backups` | `backups` |
| `telemetry` | `telemetry` |
| `benchmark_evidence` | `evidence` |

Regions must be a subset of the regions declared for the referenced residency
areas.

## Retention Windows

Every environment policy must include all retention classes:

| Retention class | Payload retention | Metadata/evidence retention | Required behavior |
| --- | --- | --- | --- |
| `ephemeral` | Positive day count. | Positive day count at least as long as payload retention. | Delete payload and preserve minimal metadata. |
| `standard` | Positive day count. | Positive day count at least as long as payload retention. | Minimize payload and preserve source/evidence links. |
| `audit` | Positive day count. | Positive day count at least as long as payload retention. | Preserve payload-safe audit record. |
| `legal_hold` | `null`. | Positive day count. | Preserve until an audited hold-release action. |
| `erasure_requested` | `0`. | Positive day count. | Erase payload while preserving audit evidence. |

`stricterSensitivityClasses` can name `personal`, `secret`, or `regulated`
when an environment uses stricter windows for sensitive content.

## External Payload Policy

`externalPayloadPolicy.mode` must be one of:

| Mode | Meaning |
| --- | --- |
| `disabled` | External payload stores are not allowed; `allowedStoreKinds` must be empty. |
| `audit_only` | External payload references may be recorded only for auditing or migration analysis. |
| `allowed` | External payload stores are allowed only when deletion evidence is required. |

The first pilot policy should keep external payload stores `disabled` until
GC-05 adds provider-specific existence, deletion, and evidence checks.

## Exception Records

Exception records are payload-safe and must expire. Each record needs:

| Field | Rule |
| --- | --- |
| `id` | Unique stable identifier. |
| `areas` | One or more residency areas affected by the exception. |
| `scope` | Environment, organization, project, or operational scope. |
| `approvedBy` | Operator or owner approving the exception. |
| `expiresOn` | Future date. |
| `evidenceLocationId` | Reference to a configured evidence location. |
| `reason` | Payload-safe reason text. |

Exceptions explain compliance drift. They do not create authorization grants and
they do not bypass erasure or legal-hold workflows.

## Evidence Locations

Every policy must include these evidence ids:

| Evidence id | Purpose |
| --- | --- |
| `release_evidence` | Release checklist, go/no-go, rollback, and approval evidence. |
| `governance_evidence` | Legal hold, erasure, retention, exception, and compliance evidence. |
| `backup_restore_evidence` | Backup export, restore validation, and future erasure replay evidence. |
| `audit_export_evidence` | Access audit export manifests and row-hash evidence. |

Each evidence location must be payload-safe, have a positive retention period,
name an access owner, and avoid secret values in the URI.

## Example Shape

```json
{
  "environment": "pilot",
  "version": "governance-policy.v1",
  "authorizationBoundary": "local_access_records_only",
  "allowedRegions": [
    { "area": "runtime", "regions": ["eu-west-2"] },
    { "area": "database", "regions": ["eu-west-2"] },
    { "area": "backups", "regions": ["eu-west-2"] },
    { "area": "telemetry", "regions": ["eu-west-2"] },
    { "area": "evidence", "regions": ["eu-west-2"] }
  ],
  "dataClasses": [
    {
      "dataClass": "event_payloads",
      "residencyAreas": ["database", "backups"],
      "regions": ["eu-west-2"]
    },
    {
      "dataClass": "memory_facts",
      "residencyAreas": ["database", "backups"],
      "regions": ["eu-west-2"]
    },
    {
      "dataClass": "embeddings",
      "residencyAreas": ["database", "backups"],
      "regions": ["eu-west-2"]
    },
    {
      "dataClass": "exports",
      "residencyAreas": ["database", "evidence"],
      "regions": ["eu-west-2"]
    },
    {
      "dataClass": "audit_rows",
      "residencyAreas": ["database", "evidence"],
      "regions": ["eu-west-2"]
    },
    {
      "dataClass": "backups",
      "residencyAreas": ["backups"],
      "regions": ["eu-west-2"]
    },
    {
      "dataClass": "telemetry",
      "residencyAreas": ["telemetry"],
      "regions": ["eu-west-2"]
    },
    {
      "dataClass": "benchmark_evidence",
      "residencyAreas": ["evidence"],
      "regions": ["eu-west-2"]
    }
  ],
  "retentionWindows": [
    {
      "retentionClass": "ephemeral",
      "payloadRetentionDays": 7,
      "metadataRetentionDays": 30,
      "legalHoldOverrideAllowed": true,
      "stricterSensitivityClasses": ["personal", "secret", "regulated"],
      "actionOnExpiry": "delete_payload_preserve_metadata"
    },
    {
      "retentionClass": "standard",
      "payloadRetentionDays": 365,
      "metadataRetentionDays": 1095,
      "legalHoldOverrideAllowed": true,
      "stricterSensitivityClasses": ["personal", "secret", "regulated"],
      "actionOnExpiry": "minimize_payload_preserve_evidence"
    },
    {
      "retentionClass": "audit",
      "payloadRetentionDays": 2555,
      "metadataRetentionDays": 2555,
      "legalHoldOverrideAllowed": true,
      "stricterSensitivityClasses": ["secret", "regulated"],
      "actionOnExpiry": "preserve_payload_safe_audit_record"
    },
    {
      "retentionClass": "legal_hold",
      "payloadRetentionDays": null,
      "metadataRetentionDays": 3650,
      "legalHoldOverrideAllowed": false,
      "stricterSensitivityClasses": [],
      "actionOnExpiry": "preserve_until_hold_released"
    },
    {
      "retentionClass": "erasure_requested",
      "payloadRetentionDays": 0,
      "metadataRetentionDays": 2555,
      "legalHoldOverrideAllowed": false,
      "stricterSensitivityClasses": [],
      "actionOnExpiry": "erase_payload_preserve_audit"
    }
  ],
  "externalPayloadPolicy": {
    "mode": "disabled",
    "allowedStoreKinds": [],
    "evidenceLocationId": "governance_evidence",
    "deletionEvidenceRequired": false
  },
  "exceptionRecords": [],
  "evidenceLocations": [
    {
      "id": "release_evidence",
      "kind": "object_store",
      "uri": "s3://memorysystem-pilot-evidence/governance/release",
      "region": "eu-west-2",
      "retentionDays": 365,
      "accessOwner": "release-owner",
      "payloadSafeOnly": true
    },
    {
      "id": "governance_evidence",
      "kind": "object_store",
      "uri": "s3://memorysystem-pilot-evidence/governance/governance",
      "region": "eu-west-2",
      "retentionDays": 2555,
      "accessOwner": "governance-owner",
      "payloadSafeOnly": true
    },
    {
      "id": "backup_restore_evidence",
      "kind": "object_store",
      "uri": "s3://memorysystem-pilot-evidence/governance/backup-restore",
      "region": "eu-west-2",
      "retentionDays": 2555,
      "accessOwner": "platform-owner",
      "payloadSafeOnly": true
    },
    {
      "id": "audit_export_evidence",
      "kind": "audit_store",
      "uri": "s3://memorysystem-pilot-evidence/governance/audit-export",
      "region": "eu-west-2",
      "retentionDays": 2555,
      "accessOwner": "security-owner",
      "payloadSafeOnly": true
    }
  ]
}
```

## Validation Rules

`EnvironmentGovernancePolicyValidator` validates that:

- all four target environments can use the same contract shape
- authorization boundary is fixed to `local_access_records_only`
- every residency area, data class, retention class, and evidence id is present
- data-class regions are covered by declared residency regions
- pilot and production evidence does not use local artifact locations
- evidence URIs do not include secret-like values
- external payload stores cannot be allowed without deletion evidence
- exception records are payload-safe, expiring, and linked to evidence

## Follow-On Work

GC-02 can now generate permission-drift reports against this environment
contract. Later slices can use the same contract for backup erasure replay,
retention minimization, external payload-store checks, compliance evidence
packages, and admin console status without changing runtime authorization.
