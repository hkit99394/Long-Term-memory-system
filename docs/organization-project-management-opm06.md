# Organization And Project Management OPM-06

Status: OPM-06 implemented.

Owner: Product Owner + Tester/QA.

Created: 2026-06-09.

## Purpose

OPM-06 measures whether the organization/project management workflow actually
works for a Product Owner after OPM-01 through OPM-05 made the management
surface usable. The benchmark answers:

```text
Can a Product Owner find, inspect, and safely modify organization/project
management data without SQL, raw payload exposure, or unmeasured access drift?
```

The benchmark is payload-safe. It stores ids, counts, durations, status values,
ratings, and links to evidence. It does not store raw source payloads, memory
bodies, raw queries, API keys, database connection strings, or secret values.

## Management Success Signals

| Signal | Target | Evidence source |
| --- | --- | --- |
| Find and inspect duration | 120 seconds or less | `/admin/` Management view records the time from Management load to first organization/project detail load. |
| Safe modification duration | 300 seconds or less | Lifecycle, scope settings, access revocation, or grant-matrix update completion in the Management view. |
| Operation failures | 0 | Failed Management mutations tracked in the OPM-06 evidence object. |
| Access drift after management | 0 findings | Access inventory stale prompts plus Product Owner access-drift status. |
| SQL fallback | 0 | Product Owner marks whether raw SQL was needed to complete the workflow. |
| Raw payload leakage | 0 | Loaded Management responses must keep `rawSourcePayloadsIncluded: false`. |
| Product Owner confidence | 4 out of 5 or better | Confidence selector in the Management evidence rail. |

## UI Evidence Contract

The `/admin/` Management evidence rail now emits a payload-safe
`managementSuccessBenchmark` evidence object with:

- `benchmarkId: OPM-06`
- `payloadSafe: true`
- `rawSourcePayloadsIncluded: false`
- `rawMemoryPayloadsIncluded: false`
- `projectSuccessEvidenceLoop: true`
- Management start, first-detail, and last-modification timestamps
- find/inspect and modification durations
- visible organization/project counts
- selected organization or project scope id
- operation failure count
- access drift status and finding count
- SQL fallback status and count
- raw payload leakage count
- Product Owner confidence score
- `organizationProjectManagement` metric block for project-success scorecards

The controls are evidence-only. They do not call privileged APIs and they do
not send benchmark-only fields back to the OPM-01 through OPM-05 management
endpoints.

## Project-Success Integration

OPM-06 extends the project-success scorecard, weekly-cycle, and closeout
templates with:

- `targets.managementFindInspectDurationSecondsMaximum`
- `targets.managementModificationDurationSecondsMaximum`
- `targets.managementOperationFailuresMaximum`
- `targets.managementAccessDriftMaximum`
- `targets.managementSqlFallbackMaximum`
- `targets.managementRawPayloadLeakageMaximum`
- `targets.managementUserConfidenceMinimum`
- `metrics.organizationProjectManagement`
- `evidence.organizationProjectManagementBenchmark`

This lets Product Owners compare registration success and ongoing management
success in the same project-success closeout.

## Completion Rule

OPM-06 benchmark status is:

- `done` when the Product Owner has loaded a management detail, completed at
  least one safe management modification, marked access drift clear, avoided SQL
  fallback, exposed no raw payloads, and rated confidence at least 4/5
- `needs_review` when the workflow is incomplete or confidence/failure signals
  need another cycle
- `blocked` when access drift is found, SQL fallback was needed, or raw payload
  leakage is detected

## Verification

Focused verification:

```bash
dotnet test tests/MemorySystem.UnitTests/MemorySystem.UnitTests.csproj --filter FullyQualifiedName~OrganizationProjectManagementOpm06Tests
cd tools/ui
npm run build
npm run check
```
