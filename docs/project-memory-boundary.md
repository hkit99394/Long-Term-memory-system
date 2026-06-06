# Project Memory Boundary

Date: 2026-06-06

This document records the canonical production memory boundary for this
repository. The runtime endpoint is the host-local production proxy:

```text
http://127.0.0.1:8081
```

## Canonical Scope

| Boundary | Id | Name |
| --- | --- | --- |
| Organization | `9f8e7d6c-5b4a-4321-9123-abcdef123001` | Personal AI Systems |
| Project | `9f8e7d6c-5b4a-4321-9123-abcdef123002` | Long-Term Memory System |

The project id above is the canonical `scopeId` for durable project memory
about this repository.

## Protected Volume

Production memory is stored in the Docker volume:

```text
memorysystem-prod_memorysystem-postgres-data
```

Do not run `docker compose down -v` unless the explicit intent is to wipe the
production memory database.

## Operator Habit

After every production deploy or restart, verify both endpoints:

```text
GET http://127.0.0.1:8081/health/ready
GET http://127.0.0.1:8081/api/operations/summary
```

`/health/ready` must report healthy checks for PostgreSQL, outbox, worker, and
embedding provider before agents depend on memory.

## Seed Command

Run the idempotent seed command when setting up or repairing the project memory
boundary:

```bash
./scripts/seed-production-memory-boundary.sh
```

The script uses `.env.production` for the operator principal id and Docker
Compose project. It does not print API keys or secret values.

After the boundary exists, seed the first source-backed project knowledge base:

```bash
./scripts/seed-production-knowledge-base.sh
```

That script uses `http://127.0.0.1:8081`, appends document evidence events, and
proposes durable project memories from `project-goal.md`, `architecture.md`,
`external-pilot-go-epr04-v1.0.0-2026-06-04.md`,
`agent-facing-memory-contract.md`, `project-memory-boundary.md`, `roadmap.md`,
`memory-vs-markdown-policy.md`, and `backlog.md`. Each event records the
current source document SHA-256 and the seed refuses to run if curated excerpts
no longer appear in the source file.

Use [How This Project Uses Its Own Memory System](project-memory-runbook.md)
for the repeatable first execution slice, role-lens context checks, feedback
loop, and weekly admin review habit.

## Namespaces

The operator principal receives explicit project-scoped admin grants for:

```text
/project/9f8e7d6c-5b4a-4321-9123-abcdef123002/goals
/project/9f8e7d6c-5b4a-4321-9123-abcdef123002/facts
/project/9f8e7d6c-5b4a-4321-9123-abcdef123002/decisions
/project/9f8e7d6c-5b4a-4321-9123-abcdef123002/rationale
/project/9f8e7d6c-5b4a-4321-9123-abcdef123002/risks
/project/9f8e7d6c-5b4a-4321-9123-abcdef123002/release-evidence
/project/9f8e7d6c-5b4a-4321-9123-abcdef123002/role/product_owner/lens
/project/9f8e7d6c-5b4a-4321-9123-abcdef123002/role/cto/lens
/project/9f8e7d6c-5b4a-4321-9123-abcdef123002/role/security_professional/lens
/project/9f8e7d6c-5b4a-4321-9123-abcdef123002/role/it_manager/lens
/project/9f8e7d6c-5b4a-4321-9123-abcdef123002/role/developer/lens
/project/9f8e7d6c-5b4a-4321-9123-abcdef123002/role/designer/lens
/project/9f8e7d6c-5b4a-4321-9123-abcdef123002/role/tester_qa/lens
/project/9f8e7d6c-5b4a-4321-9123-abcdef123002/role/release_manager/lens
/project/9f8e7d6c-5b4a-4321-9123-abcdef123002/role/knowledge_steward/lens
/project/9f8e7d6c-5b4a-4321-9123-abcdef123002/role/cfo/lens
/project/9f8e7d6c-5b4a-4321-9123-abcdef123002/role/coo/lens
/project/9f8e7d6c-5b4a-4321-9123-abcdef123002/role/ceo/lens
```

## Role Responsibilities

The first-class operating role vocabulary is `product_owner`, `cto`,
`security_professional`, `it_manager`, `developer`, `tester_qa`,
`release_manager`, and `knowledge_steward`. The runtime also keeps legacy
business roles `designer`, `cfo`, `coo`, and `ceo` for compatibility.

| Operating Role | Memory Role | Primary Ownership |
| --- | --- | --- |
| Product Owner | `product_owner` | Goals, target users, acceptance criteria, backlog, roadmap targets, roadmap priority, and product GO/NO-GO rationale. |
| CTO | `cto` | Architecture decisions, technology tradeoffs, platform direction, and technical risk acceptance. |
| Security Professional | `security_professional` | Access boundaries, secret policy, sensitivity classification, audit policy, and retention/erasure requirements. |
| IT Manager / Ops | `it_manager` | Runtime health, deploys, backups, restore validation, monitoring, and incident response. |
| Developer | `developer` | Implementation facts, API contracts, migrations, code constraints, and technical decisions. |
| Tester / QA | `tester_qa` | Test gates, benchmark evidence, regression risks, and release quality evidence. |
| Release Manager | `release_manager` | Version state, release checklist, evidence bundle, rollback plan, and pilot readiness coordination. |
| Knowledge Steward | `knowledge_steward` | Memory taxonomy, source evidence quality, stale/wrong/duplicate review routing, and memory hygiene. |

Role-specific memory is an access boundary, not just metadata. A role lens
must only be used when the principal has the matching role assignment and the
task genuinely needs that perspective.
