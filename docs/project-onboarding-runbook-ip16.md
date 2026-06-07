# Project Onboarding Runbook IP-16

Status: implemented for improvement plan item IP-16.

Owner: Product Owner + Knowledge Steward.

## Purpose

IP-16 turns a new project memory boundary into a repeatable Product Owner and
Knowledge Steward workflow. It covers project scope, role owners, namespace
grants, canonical memory types, seed documents, source evidence, role-lens
checks, and review cadence before agents depend on durable memory.

The runbook is payload-safe. It does not make live API writes by default and it
does not print raw document or memory payloads.

## Dry Run

Render the onboarding plan for the canonical project:

```bash
bash -n scripts/project-onboarding-runbook.sh
scripts/project-onboarding-runbook.sh --dry-run
```

Render a plan for a different project:

```bash
scripts/project-onboarding-runbook.sh --dry-run \
  --organization-id 00000000-0000-0000-0000-000000000001 \
  --organization-name "Example Org" \
  --project-id 00000000-0000-0000-0000-000000000002 \
  --project-name "Example Project"
```

The JSON report includes:

- `targetScope` with organization id, project id, and `scopeType: project`
- `roleDefinitions` for Product Owner, CTO, Security, Ops, Developer, QA,
  Release Manager, and Knowledge Steward
- `namespaceGrants` for shared project memory and role-specific lens memory
- canonical `memoryTypes` from [Canonical Memory Types IP-06](canonical-memory-types-ip06.md)
- `seedDocuments` with SHA-256 hashes and excerpt counts, not raw payloads
- `sourceEvidencePlan` for `/api/events`, `/api/memory/proposals`,
  `/api/memory/context`, and `/api/memory/context/feedback`
- `reviewCadence` for onboarding closeout, weekly memory review, weekly access
  review, roadmap/backlog sync, and release evidence

## Onboarding Flow

1. Define the scope.
   Record the organization id, project id, names, protected runtime boundary,
   and host-local or managed endpoint in committed Markdown before seeding
   memory. The project id becomes the durable `scopeId`.

2. Confirm role owners.
   Start with the default operating roles from
   [Project Memory Boundary](project-memory-boundary.md): `product_owner`,
   `cto`, `security_professional`, `it_manager`, `developer`, `tester_qa`,
   `release_manager`, and `knowledge_steward`. Disable or defer roles that do
   not have accountable owners.

3. Grant namespaces.
   Grant only the project namespaces needed for shared memory:
   `/project/{projectId}/goals`, `/facts`, `/decisions`, `/rationale`,
   `/risks`, and `/release-evidence`. Grant role-lens memory only under
   `/project/{projectId}/role/{roleId}/lens`.

4. Confirm memory types.
   Use canonical durable memory types only: `goal`, `target`, `fact`,
   `decision`, `rationale`, `risk`, `assumption`, `constraint`, `requirement`,
   `release_evidence`, and `role_lens`.

5. Curate seed documents.
   Start with the project goal, architecture, agent-facing contract,
   memory-vs-Markdown policy, project memory boundary, project memory runbook,
   roadmap, and backlog. Curate short excerpts in the seed script and refresh
   every `sourceContentSha256` when the source file changes.

6. Append source evidence.
   Append evidence through `/api/events` with `sourceUri`,
   `sourceContentSha256`, `scopeType`, `scopeId`, and an idempotency key. Do
   not store raw secrets, credentials, or unreviewed private payloads.

7. Propose seed memory.
   Propose compact durable memory through `/api/memory/proposals`. Each
   proposal must include canonical `memoryType`, namespace, source event id,
   source link, confidence, trust level, sensitivity, and review behavior.

8. Seed role lenses.
   Run the role-lens first pass after base responsibility facts exist. Role
   lenses are retrieval guidance and must remain role-bound.

9. Verify context.
   Query `/api/memory/context` for shared project memory and for each active
   role lens. Confirm that `role_lens` memory appears only for the authorized
   matching role and that source links are present.

10. Schedule review cadence.
    Record onboarding feedback through `/api/memory/context/feedback`, run the
    weekly memory review queue, run the weekly access-boundary review, and
    rerun roadmap/backlog sync whenever product plan state changes.

## Verification

IP-16 is complete when:

- `scripts/project-onboarding-runbook.sh --dry-run` emits a payload-safe JSON
  onboarding plan with project scope, roles, grants, memory types, seed docs,
  source evidence, and review cadence.
- [Project Memory Runbook](project-memory-runbook.md) and
  [Project Memory Boundary](project-memory-boundary.md) link to the onboarding
  flow.
- [Testing Commands](testing.md), [Documentation Index](README.md), and
  [Folder Structure](folder-structure.md) include the IP-16 workflow.
- Unit tests cover the documentation contract and dry-run output.
