# Project Registration UX Plan

Status: REG-01 through REG-06 implemented.

Owner: Product Owner + Designer + Security Professional + Knowledge Steward + Developer.

Created: 2026-06-08.

## Purpose

Project registration turns the current operator runbook into a guided Product
Owner workflow for creating a governed memory boundary for a new project.
IP-16 remains the payload-safe onboarding runbook. This plan adds the product
step that should eventually live in `/admin/` and remove the need for raw SQL
or namespace hand-editing during normal project setup.

## Target And Timeframe

| ID | Target date | Owner | Target | Acceptance criteria |
| --- | --- | --- | --- | --- |
| REG-01 | 2026-06-12 | Product Owner + Security Professional | Registration specification and dry-run contract | Done. Required fields, validation rules, least-privilege presets, source-doc checks, access preview, audit evidence, and closeout criteria are documented and covered by `scripts/project-onboarding-runbook.sh --dry-run`. |
| REG-02 | 2026-06-19 | Developer + Security Professional | First registration API contract | Done. `POST /api/admin/projects/register` creates or updates organization/project rows, role definitions, owner memberships, namespace grants, and payload-safe audit evidence idempotently. |
| REG-03 | 2026-06-26 | Designer + Developer | Admin wizard MVP | Done. `/admin/` exposes a guided Project Registration flow with Project Details, People & Responsibilities, Seed Evidence, Access Rules, Review & Validate, Register, and Finish Setup steps, backed by access checks and the idempotent registration API. |
| REG-04 | 2026-07-03 | Security Professional + Ops | Least-privilege bootstrap cleanup | Done. Root namespace admin grants stay absent, day-to-day setup uses read/write/review presets, and break-glass namespace admin is separated, owner-reviewed, time-bound, and audit-evidenced. |
| REG-05 | 2026-07-10 | Knowledge Steward + Product Owner | Source-backed seed UX | Done. The wizard shows source-document fingerprint status, source owners, evidence type coverage, role-specific guidance readiness, retrieval checks, and feedback closeout without raw payload exposure. |
| REG-06 | 2026-07-17 | Product Owner + Tester/QA | Registration success benchmark | Done. Registration duration, validation failures, access changes after registration, source evidence coverage, and user confidence are tracked in the wizard evidence rail and the project-success scorecard, weekly-cycle, and closeout templates. |

## Required Registration Inputs

Registration planning, the wizard, and closeout evidence should capture or
generate:

- `organizationId`, `organizationName`, `projectId`, `projectName`, and `projectStatus`
- runtime boundary and API base URL
- Product Owner principal, Knowledge Steward principal, Security/Ops principal, and optional operator or break-glass principal
- active roles, deferred roles, and custom project role definitions
- membership level per owner and role
- namespace grant preset per memory area: read, write, review, or admin only for break-glass
- seed document paths, source owners, source hashes, and intended evidence types
- review cadence evidence for memory hygiene, access boundary, roadmap/backlog sync, release evidence, and project-success measurement

## REG-01 Dry-Run Contract

REG-01 is complete when the onboarding dry run emits a payload-safe
`registrationContract` object. The dry run does not create or change projects;
it validates the shape that REG-02 and REG-03 will later turn into API and UI.

Run a complete registration contract dry run:

```bash
scripts/project-onboarding-runbook.sh --dry-run \
  --product-owner-principal-id 11111111-1111-4111-8111-111111111111 \
  --knowledge-steward-principal-id 22222222-2222-4222-8222-222222222222 \
  --security-ops-principal-id 33333333-3333-4333-8333-333333333333 \
  --custom-role research_lead="Research Lead" \
  --deferred-role cfo
```

The dry-run output must include:

- `registrationContract.contractId: REG-01`
- `registrationContract.requiredFields`
- `registrationContract.grantPresets`
- `registrationContract.ownerAssignments`
- `registrationContract.namespaceGrantMatrix`
- `registrationContract.sourceDocChecks`
- `registrationContract.effectiveAccessPreviewPlan`
- `registrationContract.auditEvidencePlan`
- `registrationContract.plannedOperations`
- `registrationContract.preflightChecklist`
- `registrationContract.closeoutCriteria`
- `registrationValidation.status`
- `registrationValidation.blockingValidationErrors`
- `registrationValidation.validationRules`

The default grant preset is `least-privilege`. `bootstrap-admin` is allowed only
as a dry-run exception path and requires owner, reason, review due date, cleanup
action, and audit evidence id before a future live workflow can commit it.

Registration validation fails closed for invalid role ids, missing owner
principals, undefined active/deferred roles, missing seed documents, root
namespace grants, and admin grants without accepted-finding metadata.

## REG-02 API Contract

REG-02 turns the REG-01 dry-run shape into the first live admin API contract.
The endpoint is:

```text
POST /api/admin/projects/register
```

Callers must send normal admin authentication plus an `Idempotency-Key` header.
The request is JSON and includes:

- `organizationId`, `organizationName`, `projectId`, `projectName`, and `projectStatus`
- `roleDefinitions` for project-specific roles that should be active before assignment
- `ownerAssignments` with principal id, role id, project access level, and optional label
- `namespaceGrants` with exactly one `principalId` or `roleId`, a project namespace prefix, and `read`, `write`, or `review`
- `sourceDocuments` with path, source hash, and optional source owner role
- `accessPreviewReportId` and optional `auditExportId`; each audit metadata id is capped at 500 characters

The endpoint returns `contractId: REG-02`, `status: registered`,
organization/project records, role definitions, owner assignments, namespace
grants, and `auditEvidence` containing the payload-safe audit event id,
idempotency record id, request hash, preview report id, optional audit export
id, action type, resource type, and resource id.

Safety rules:

- The response is payload-safe and does not echo raw source payloads.
- Audit metadata ids must be 500 characters or fewer before the project and access rows are committed.
- Operators must have admin access to the target project or owner/admin access to the target organization.
- `planned` and `active` are the registration project statuses; normal first registration uses `active`.
- Owner project memberships are limited to `reader`, `contributor`, or `reviewer`.
- Root namespace grants and project-root grants are rejected.
- Live registration namespace grants are limited to `read`, `write`, and `review`; admin grants remain a separate break-glass path for REG-04.
- Custom role assignments and role grants require an active project role definition.
- Review cadence remains closeout evidence from the onboarding runbook and project-success loop; it is not persisted by the first live registration API.

## REG-03 Admin Wizard MVP

REG-03 adds the Project Registration mode to `/admin/`. The implementation is
checked in as `tools/ui/src/admin/registration-panel.ts` and bundled into
`src/MemorySystem.Api/wwwroot/admin/admin-console.js` by `npm run build` in
`tools/ui`.

The wizard step list is:

- Project Details
- People & Responsibilities
- Seed Evidence
- Access Rules
- Review & Validate
- Register
- Finish Setup

The MVP keeps the Product Owner path API-backed and payload-safe:

- Project Details captures organization/project IDs and names plus project status; the retry safety key is available under advanced settings.
- People & Responsibilities captures owner person assignments, default responsibility templates, and optional custom project responsibilities.
- Seed Evidence captures source document paths, SHA-256 fingerprints, owner responsibilities, and evidence types without raw source payloads.
- Access Rules uses project memory-area presets for read, write, and review grants; live admin grants stay outside the normal registration path.
- Review & Validate shows blockers, links each blocker back to the right step, and calls `POST /api/admin/access/effective-preview` for planned people/access checks before commit.
- Register calls `POST /api/admin/projects/register` with `Idempotency-Key` and stores the payload-safe response in the evidence rail.
- Finish Setup keeps access confirmation, context retrieval check, feedback follow-up, and success benchmark evidence visible after registration.

## REG-UX-01 Beginner-Friendly Registration Wizard

REG-UX-01 applies the UX review findings across the implemented REG-03 to
REG-06 flow. The wizard uses operator-facing language for normal work while
keeping technical IDs and payload-safe evidence available where needed.

The UX pass:

- Renames internal step terms to Project Details, People & Responsibilities, Seed Evidence, Access Rules, Review & Validate, Register, and Finish Setup.
- Adds a "Before you start" panel that tells operators which names, person IDs, source paths, and SHA-256 fingerprints they need.
- Moves the retry safety key into an advanced section so new users do not start with idempotency terminology.
- Puts seed evidence before access rules so operators define the project evidence before deciding who can read, write, or review it.
- Shows registration blockers in a recovery panel with "Go to" actions for the step that fixes each issue.
- Disables Register while blocking checks remain.
- Rewords evidence-rail labels around source evidence coverage, role-specific guidance, access changes from plan, and access check reports.

REG-03 is covered by `ProjectRegistrationReg03Tests`, the `tools/ui` TypeScript
build/check, and the existing REG-02 API/integration coverage.

## REG-04 Bootstrap Admin Separation

REG-04 keeps normal project registration and day-to-day access management on
least-privilege grants. The registration API and wizard continue to create only
`read`, `write`, and `review` namespace grants; root namespace admin grants and
project-root or organization-root admin grants stay absent.

The generic admin Access Management view now separates normal namespace grants
from break-glass namespace admin. The `Namespace grant` form exposes only
`read`, `write`, and `review`. The `Break-glass namespace admin` form posts an
explicit `admin` grant with a `breakGlassEvidence` object.

Break-glass namespace admin requests to
`POST /api/admin/access/namespace-grants` must include:

- exactly one principal target and no role target
- a namespace below the target project or organization root
- owner role
- accepted-by role
- reason
- review due date in `yyyy-MM-dd` format
- cleanup action
- audit evidence id

The endpoint rejects root namespaces (`/global`, `/org`, `/project`, `/user`,
`/role`, `/agent`, and `/session`), project-root namespaces such as
`/project/{projectId}`, organization-root namespaces such as `/org/{orgId}`,
self-admin escalation, role-targeted admin grants, missing evidence, past review
dates, and admin namespaces outside the selected scope.

Successful break-glass admin grants write payload-safe audit metadata:
`breakGlass`, `breakGlassOwnerRole`, `breakGlassAcceptedByRole`,
`breakGlassReason`, `breakGlassReviewDue`, `breakGlassCleanupAction`, and
`breakGlassAuditEvidenceId`. The metadata records the decision and cleanup path
without storing raw source payloads or secrets.

## REG-05 Source-Backed Seed Readiness UX

REG-05 expands the Project Registration wizard's Seed Evidence step from a simple
source list into a source-backed seed readiness panel. The registration API
payload remains payload-safe and continues to send only source document path,
SHA-256 hash, and source owner role; the richer readiness state is shown in the
wizard evidence rail and preflight view.

Seed document rows now capture:

- source document path
- SHA-256 content hash
- source owner role
- intended canonical evidence types

The readiness panel shows:

- source document path coverage
- SHA-256 hash coverage
- source owner role coverage
- canonical evidence type coverage
- role-specific guidance coverage for registered owner responsibilities
- retrieval check status
- feedback closeout status
- raw source payload status as not included

Review & Validate now includes seed readiness evidence next to access check evidence.
Registration is blocked when source paths, SHA-256 hashes, owner roles, evidence
types, or retrieval checks are invalid. Role-specific guidance and feedback closeout
show as `needs_review` until completed, because registration creates the project
boundary while durable memory seed writes happen after source evidence and
closeout checks are available.

## REG-06 Registration Success Benchmark

REG-06 connects project registration to the project-success evidence loop. The
wizard now captures a payload-safe success benchmark alongside the registration
result and preflight evidence, and the project-success scorecard, weekly-cycle,
and closeout templates expose matching fields for Product Owner review.

The benchmark records:

- registration start and completion timestamps
- registration duration in seconds
- blocked validation submit attempts
- current blocking validation checks
- post-registration access change status from the access-boundary review
- source evidence coverage for seed documents with path, SHA-256 hash, and owner role
- Product Owner/user confidence rating from 1 to 5

The benchmark evidence keeps `payloadSafe: true`,
`rawSourcePayloadsIncluded: false`, and `projectSuccessEvidenceLoop: true`.
It does not send benchmark-only fields to `POST /api/admin/projects/register`;
the registration API remains focused on creating the governed project boundary.

Access changes from plan are recorded as `pending`, `clear`, or `drift_found`. The first UI
capture is an evidence field controlled during closeout; live drift detection
continues to come from the access-boundary review workflow. A `clear` drift
status, 100% source evidence coverage, and confidence of at least 4/5 complete the
REG-06 success benchmark.

## UX Requirements

- Generate stable IDs by default and let advanced operators paste known IDs.
- Use role and namespace presets instead of free-text namespace entry for the normal path.
- Show inline validation before registration: invalid project id, invalid role id, missing owner, broad namespace, disabled custom role, missing seed document path, and invalid SHA-256 hash format.
- Check effective access before commit, grouped by person and memory area.
- Show source-backed seed readiness before durable memory writes: path coverage, hash coverage, source owners, canonical evidence types, role-specific guidance, retrieval checks, and feedback closeout.
- Track registration success before finish setup: duration, validation failures, access changes from plan, source evidence coverage, and user confidence.
- Keep review cadence visible as onboarding and closeout evidence through the runbook and project-success loop.
- Make the destructive or privileged path explicit: admin grants require owner, reason, review due date, cleanup action, and audit evidence.
- Keep the primary flow in a stepper with a persistent summary rail: Project Details, People & Responsibilities, Seed Evidence, Access Rules, Review & Validate, Register, Finish Setup.
- Keep payload safety visible through status and evidence labels, not raw source payloads or secret values.

## Success Targets

- A Product Owner can register a new project without editing SQL, shell scripts, or raw namespace strings.
- The default registration path creates zero root namespace grants such as `/project`, `/org`, `/global`, `/user`, `/role`, `/agent`, or `/session`.
- Every break-glass admin grant has owner, reason, review due date, cleanup action, and audit evidence.
- Source-backed seed readiness reaches 100% hash coverage before durable memory writes.
- Registration closeout includes one successful access-boundary review, one context retrieval check, and recorded feedback.
- Registration success evidence reaches clear access-change status, 100% source evidence coverage, and Product Owner confidence of at least 4/5.

## Risks

- Raw UUID and namespace forms are powerful but easy to misuse; the wizard must make the common path picker-driven.
- Role-lens retrieval can look empty when base facts, role assignment, namespace grant, and seed timing are out of order.
- Bootstrap admin access is useful for repair, but it must not become the default operating model.
- Registration is a cross-cutting workflow; implementation should reuse existing access-management primitives rather than create a parallel authorization path.
