# How This Project Uses Its Own Memory System

Date: 2026-06-06

This runbook describes how this repository uses the local production memory
system as its own knowledge-management layer.

## Runtime Boundary

Use the host-local production endpoint as the active memory endpoint:

```text
http://127.0.0.1:8081
```

The canonical durable scope for this repository is:

| Boundary | Id |
| --- | --- |
| Organization | `9f8e7d6c-5b4a-4321-9123-abcdef123001` |
| Project | `9f8e7d6c-5b4a-4321-9123-abcdef123002` |

Production memory is stored in Docker volume
`memorysystem-prod_memorysystem-postgres-data`. Never run
`docker compose down -v` unless the explicit intent is to wipe production
memory.

After every deploy or restart, check:

```text
GET http://127.0.0.1:8081/health/ready
GET http://127.0.0.1:8081/api/operations/summary
```

## Agent Work Loop

Before planning, coding, reviewing, or releasing project work, agents should:

1. Call `GET /api/memory/context` for task-scoped context.
2. Call `POST /api/memory/query-facts` when facts, decisions, contradictions,
   policy metadata, lifecycle state, or source links matter.
3. After the task, call `POST /api/memory/context/feedback` with `useful`,
   `stale`, `wrong`, `sensitive`, `over_broad`, or `missing` feedback.

The context request must target the project scope. Add `roleId` only when the
task genuinely needs that role lens and the principal has the matching role
assignment.

Use [Memory vs Markdown Policy](memory-vs-markdown-policy.md) when retrieved
memory changes project plans, policy, release posture, API contracts, or
backlog status. Markdown and payload-safe release evidence are authoritative
for those records; project memory should store compact source-linked summaries,
not hidden roadmap or policy state.

## First Execution Slice

1. Seed or repair the project, organization, role assignments, and grants:

   ```bash
   ./scripts/seed-production-memory-boundary.sh
   ```

2. Store the first source-backed knowledge base from the current docs:

   ```bash
   ./scripts/seed-production-knowledge-base.sh
   ```

   The seed should keep 10-20 or more high-value memories about goals,
   architecture, release state, the agent contract, memory-vs-Markdown policy,
   roadmap, backlog, and role responsibilities. It validates curated excerpts
   against the current source files and records source document hashes in the
   evidence events.

3. Query context through the CTO lens:

   ```bash
   curl --max-time 10 -sS -G \
     -H "X-Api-Key: ${MEMORYSYSTEM_API_KEY}" \
     --data-urlencode "q=What should the CTO know before this task?" \
     --data-urlencode "scopeType=project" \
     --data-urlencode "scopeId=9f8e7d6c-5b4a-4321-9123-abcdef123002" \
     --data-urlencode "roleId=cto" \
     http://127.0.0.1:8081/api/memory/context
   ```

4. Query context through the Product Owner lens:

   ```bash
   curl --max-time 10 -sS -G \
     -H "X-Api-Key: ${MEMORYSYSTEM_API_KEY}" \
     --data-urlencode "q=What should the Product Owner know before this task?" \
     --data-urlencode "scopeType=project" \
     --data-urlencode "scopeId=9f8e7d6c-5b4a-4321-9123-abcdef123002" \
     --data-urlencode "roleId=product_owner" \
     http://127.0.0.1:8081/api/memory/context
   ```

5. Verify evidence links and role boundaries:

   - The response `targetScope` must be the canonical project.
   - The response `roleId` must match the requested role lens.
   - Returned memory items should include `sourceEventId` and `sourceLink`.
   - Role-lens memory must only appear for authorized matching role requests.
   - Shared project memory may appear across roles when the caller is
     authorized.

6. Record feedback for any memory that changed the work:

   ```text
   POST http://127.0.0.1:8081/api/memory/context/feedback
   ```

## Role Ownership

| Role | Primary Ownership |
| --- | --- |
| Product Owner | Goals, target users, acceptance criteria, backlog, roadmap targets, roadmap priority, and product GO/NO-GO rationale. |
| CTO | Architecture decisions, technology tradeoffs, platform direction, and technical risk acceptance. |
| Security Professional | Access boundaries, secret policy, sensitivity classification, audit policy, and retention/erasure requirements. |
| IT Manager / Ops | Runtime health, deploys, backups, restore validation, monitoring, and incident response. |
| Developer | Implementation facts, API contracts, migrations, code constraints, and technical decisions. |
| Tester / QA | Test gates, benchmark evidence, regression risks, and release quality evidence. |
| Release Manager | Version state, release checklist, evidence bundle, rollback plan, and pilot readiness coordination. |
| Knowledge Steward | Memory taxonomy, source evidence quality, stale/wrong/duplicate review routing, and memory hygiene. |

Product Owner owns backlog management and roadmap target priority. CTO owns the
technical feasibility and architecture consequences of those targets.

## Weekly Review

Once each week, review the live console:

```text
http://127.0.0.1:8081/admin/
http://127.0.0.1:8081/reviews/
```

Route stale, wrong, duplicate, missing, sensitive, or over-broad memories to the
appropriate owner. The Knowledge Steward owns review hygiene, but each role owns
the correctness of its domain.
