# Role Lens First Content Pass IP-13

Date completed: 2026-06-07

Status: implemented

## Purpose

IP-13 seeds the first role-specific interpretation layer for the project
memory boundary. The role lenses are not new facts. They are compact attention
and risk-framing guidance over the shared, source-backed responsibility facts
already seeded from [Project Memory Boundary](project-memory-boundary.md).

## Roles Seeded

| Role | Memory Role | Base Fact Subject | Lens Emphasis |
| --- | --- | --- | --- |
| Product Owner | `product_owner` | `product owner responsibility` | Goals, target users, acceptance criteria, backlog/roadmap priority, and GO/NO-GO rationale. |
| CTO | `cto` | `cto responsibility` | Architecture decisions, platform tradeoffs, domain boundaries, technical risk, migrations, and reversibility. |
| Security Professional | `security_professional` | `security professional responsibility` | Access boundaries, secret handling, sensitivity, auditability, retention/erasure, and fail-closed behavior. |
| IT Manager / Ops | `it_manager` | `it manager ops responsibility` | Runtime health, deploy safety, backup/restore evidence, monitoring, incident response, rollback, and protected volumes. |
| Developer | `developer` | `developer responsibility` | API contracts, migrations, code constraints, testable facts, and source-grounded technical decisions. |
| Tester / QA | `tester_qa` | `tester qa responsibility` | Test gates, benchmark evidence, regression risks, release quality evidence, and verification commands. |
| Release Manager | `release_manager` | `release manager responsibility` | Version state, release checklist, evidence bundles, rollback plans, pilot readiness, and GO/NO-GO ownership. |
| Knowledge Steward | `knowledge_steward` | `knowledge steward responsibility` | Canonical memory types, source evidence quality, stale/wrong/duplicate routing, namespace policy, and memory-vs-Markdown hygiene. |

## Runtime Contract

Run the source-backed project knowledge seed first so the base responsibility
facts exist:

```bash
scripts/seed-production-knowledge-base.sh
```

Then validate the IP-13 plan without API calls:

```bash
bash -n scripts/role-lens-first-pass.sh
scripts/role-lens-first-pass.sh --dry-run
```

The live seed resolves each active base fact through
`GET /api/admin/memory/facts`, appends one payload-safe repository evidence
event, and proposes one `role_lens` memory per operating role through
`POST /api/memory/proposals`.

Each proposed role lens uses:

- `memoryType`: `role_lens`
- `scopeType`: `project`
- `scopeId`: `9f8e7d6c-5b4a-4321-9123-abcdef123002`
- namespace: `/project/{projectId}/role/{roleId}/lens`
- `roleId`: the matching role id
- `baseMemoryFactId`: the live id of the matching responsibility fact
- `trustLevel`: `user_scoped`
- `sensitivity`: `none`

If the running production API has not yet received the IP-06 canonical memory
type update, the script records `compatibilityFallbackReason` and retries with
the legacy `project_role_lens` alias. That alias still writes to the
role-memory-lens storage path; after deploying the IP-06 runtime, rerunning the
script idempotently uses the canonical `role_lens` default.

The script refuses to live-seed if a base fact is missing or duplicated. It
prints only payload-safe ids, counts, source paths, hashes, decisions, and
review status.

## Verification

IP-13 is complete when:

- The dry run lists eight role lenses and three source documents without raw
  source payloads.
- The live run stores or idempotently reuses eight `role_lens` memories.
- Role-context checks through `GET /api/memory/context` return role-lens memory
  only for authorized matching role requests.
- Unit tests cover script wiring, dry-run safety, endpoint usage, base fact
  resolution, and canonical role-lens namespaces.
