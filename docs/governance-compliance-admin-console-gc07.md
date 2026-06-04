# GC-07 Governance/Compliance Admin Console

GC-07 adds a read-only governance and compliance view to the authenticated
`/admin/` console. The view is an operator index over the existing governance
reports, compliance jobs, and payload-safe evidence artifacts; it does not add a
new source of compliance truth.

## Operator Surface

The admin console now includes a `Compliance` view backed by:

```http
GET /api/admin/compliance/status
```

The endpoint returns:

- `generatedAt`
- global `payloadSafe` and `rawSourcePayloadsIncluded` flags
- one status item each for `retention_report`, `legal_hold_summary`,
  `erasure_replay`, `permission_drift_report`, and
  `compliance_evidence_package`
- links to the existing API, script, documentation, or evidence surfaces
- alert metric names where a platform job already emits them

raw source payloads are not included in this response. The compliance view shows
counts, status names, paths, evidence kinds, and metric names only.

## Status Items

| Item | Source | Status behavior |
| --- | --- | --- |
| `retention_report` | `GET /api/admin/governance/retention-report` | Reports `available` when authorized retention groups are visible and `empty` otherwise. |
| `legal_hold_summary` | `GET /api/admin/governance/legal-holds?status=active` | Reports `active` when visible active held events exist and `clear` otherwise. |
| `erasure_replay` | `scripts/platform-erasure-replay-ledger-export.sh` and `scripts/platform-restore-validation.sh` | Reports `configured` and links the restore-time replay validation contract. |
| `permission_drift_report` | `POST /api/admin/access/permission-drift` | Reports `available` and links the payload-safe drift report contract. |
| `compliance_evidence_package` | `scripts/platform-compliance-evidence-package.sh` | Reports `configured` and links the GC-06 package contract, audit export API, and package metrics. |

The live counts intentionally stay limited to already-authorized legal-hold and
retention queries. Platform job freshness remains a GC-08 release smoke concern
rather than a fake in-process status.

## Payload Safety

The compliance endpoint and view must not display:

- source event `content`
- memory fact body text
- review notes
- raw query text
- external payload bytes, credentials, or provider secrets
- event ids from retention or legal-hold source rows unless an operator opens an
  existing authorized report explicitly

The view may display:

- API paths and methods
- script and documentation paths
- evidence kinds
- counts and status labels
- metric names and short descriptions
- generated timestamps

## Verification

GC-07 is covered by:

- `GovernanceComplianceAdminConsoleContractTests`
- `ApiAdminMemoryConsoleTests.Get_admin_compliance_status_lists_payload_safe_governance_links`
- the existing admin console static asset test, which now asserts the
  `Compliance` mode and `/api/admin/compliance/status` wiring

Run:

```bash
dotnet test tests/MemorySystem.UnitTests/MemorySystem.UnitTests.csproj
MEMORYSYSTEM_TEST_POSTGRES_CONNECTION_STRING="Host=127.0.0.1;Port=55432;Database=memory_system;Username=memory_system;Password=memory_system_dev_password" dotnet test tests/MemorySystem.IntegrationTests/MemorySystem.IntegrationTests.csproj --filter "FullyQualifiedName~ApiAdminMemoryConsoleTests.Get_admin_compliance_status_lists_payload_safe_governance_links"
```

## Next Slice

GC-08 turns this status map into a repeatable release smoke. The smoke creates
or verifies isolated evidence for policy config, permission drift, erasure
replay, retention dry run, audit export, and the compliance evidence manifest.
