# Documentation Hub

This folder contains the product, architecture, API, testing, operations,
release, and decision records for the Long-Term Memory System.

If you are new to the repository, start with the short path below. The later
sections are for deeper implementation, pilot, governance, and platform work.

## Start Here

| Need | Read |
| --- | --- |
| Understand the project in one page | [Project Goal](project-goal.md) |
| Understand the system shape | [Architecture Overview](architecture.md) |
| Run the local product workflow | [Local Demo Workflow](private-alpha-workflow.md) |
| Find setup and test commands | [Testing Commands](testing.md) |
| Integrate as an agent or client | [API Contracts](api/README.md) |
| Know whether memory or Markdown is authoritative | [Memory vs Markdown Policy](memory-vs-markdown-policy.md) |
| Understand current release posture | [External Pilot GO EPR-04 v1.0.0](external-pilot-go-epr04-v1.0.0-2026-06-04.md) and [release readiness status](external-pilot-readiness-status.json) |

The root [README](../README.md) has the fastest copy-paste quick start.

## Public Repository Notes

- Local demo credentials such as `private-alpha-local-key` and
  `memory_system_dev_password` are committed placeholders for local development
  and tests only. Do not reuse them for shared, pilot, staging, or production
  environments.
- Several records use terms such as "private alpha" and "external pilot". These
  are historical product milestones, not hidden deployment instructions.
- Current release status is tracked by
  [external-pilot-readiness-status.json](external-pilot-readiness-status.json)
  and the v1.0.0 GO record. Older no-go or rehearsal documents are preserved as
  decision history.
- A license file is not currently committed. Choose a license before inviting
  external reuse or broad contribution.

## Reader Paths

### Local Development

1. [Project Goal](project-goal.md)
2. [Architecture Overview](architecture.md)
3. [Folder Structure](folder-structure.md)
4. [Local Demo Workflow](private-alpha-workflow.md)
5. [Testing Commands](testing.md)

### Agent/API Integration

1. [API Contracts](api/README.md)
2. [Agent Memory v1 Client Examples](api/agent-memory-v1-examples.md)
3. [Agent Memory OpenAPI v1](api/agent-memory-v1.openapi.json)
4. [Policy Targeting For Agent Callers](api/policy-targeting-for-agent-callers.md)
5. [Context Product v1 Caller Guide](api/context-product-v1-caller-guide.md)
6. [Context Packet Product v1 Contract](api/context-packet-product-v1.md)
7. [Agent-Facing Memory Contract](agent-facing-memory-contract.md)
8. [Memory vs Markdown Policy](memory-vs-markdown-policy.md)

### Operations And Governance

1. [Production Secret Handling](production-secrets.md)
2. [Production Deployment Shape](production-deployment-shape.md)
3. [External / Managed PostgreSQL Production Profile](external-managed-postgres-profile.md)
4. [Production Container Tool](production-container.md)
5. [Project Memory Boundary](project-memory-boundary.md)
6. [Project Memory Runbook](project-memory-runbook.md)
7. [Project Onboarding Runbook IP-16](project-onboarding-runbook-ip16.md)
8. [Production Observability and Alerting](production-observability.md)
9. [Backup and Restore Runbook](backup-restore.md)
10. [Production Backup And Restore Drill Schedule IP-10](production-backup-restore-drill-schedule-ip10.md)
11. [Access Boundary Review IP-11](access-boundary-review-ip11.md)
12. [Retention Policy](retention-policy.md)
13. [Governance And Compliance Gate LR-06](governance-compliance-gate-lr06.md)
14. [Release Evidence Bundle Automation IP-17](release-evidence-bundle-automation-ip17.md)
15. [Governance/Compliance Release Smoke GC-08](governance-compliance-release-smoke-gc08.md)

### Planning And Release History

1. [Product Improvement Plan](product-improvement-plan.md)
2. [Roadmap](roadmap.md)
3. [Backlog](backlog.md)
4. [Benchmarking Plan](benchmarking.md)
5. [Project Success Benchmark PS-01](project-success-benchmark-ps01.md)
6. [Project Success Pilot Baseline PS-02](project-success-pilot-baseline-ps02.md)
7. [Benchmark Release-Gate Report LR-03](benchmark-release-gate-lr03.md)
8. [External Pilot GO EPR-04 v1.0.0](external-pilot-go-epr04-v1.0.0-2026-06-04.md)
9. [Documentation Truth Cleanup P1](documentation-truth-cleanup-p1-2026-06-04.md)

## Reference Index

### Product And Architecture

| Document | Purpose |
| --- | --- |
| [Project Goal](project-goal.md) | North star for trustworthy, auditable, permission-aware long-term memory. |
| [Architecture Overview](architecture.md) | Component map, write path, read path, data boundaries, and trust model. |
| [Long-Term AI Memory System Plan](long-term-memory-system-plan.md) | Full design plan, schema direction, phases, risks, and build steps. |
| [Folder Structure](folder-structure.md) | Repository layout, ownership boundaries, and where new work should live. |
| [Memory vs Markdown Policy](memory-vs-markdown-policy.md) | Source-of-truth rules for Markdown, memory, backlog, source evidence, role lenses, release evidence, and vault exports. |
| [Project-Defined Roles IP-05](project-defined-roles-ip05.md) | Project-specific role definitions, default role templates, and project role-lens validation rules. |
| [Canonical Memory Types IP-06](canonical-memory-types-ip06.md) | Canonical durable memory type vocabulary and compatibility aliases for proposals and query filters. |
| [Source-Backed Memory Hygiene Automation IP-07](source-backed-memory-hygiene-ip07.md) | Source hash drift, stale source link, duplicate memory, missing evidence, seed validation, and role-lens hygiene gate. |
| [Agent Memory Client Wrapper IP-08](agent-memory-client-wrapper-ip08.md) | Repo-local wrapper that enforces context retrieval, fact queries, and packet-id feedback before and after project work. |
| [Weekly Admin Review Workflow IP-09](weekly-admin-review-workflow-ip09.md) | Structured weekly queue for stale, wrong, missing, sensitive, over-broad, duplicate, and source-drift review. |
| [Access Boundary Review IP-11](access-boundary-review-ip11.md) | Weekly access-boundary review for memberships, roles, grants, service accounts, OIDC bindings, break-glass posture, and permission drift. |
| [Backlog And Roadmap Memory Sync IP-12](backlog-roadmap-memory-sync-ip12.md) | Sync guard that keeps roadmap/backlog state canonical in Markdown while seeding compact source-backed memory. |
| [Role Lens First Content Pass IP-13](role-lens-first-content-pass-ip13.md) | First source-backed role-lens seed for Product Owner, CTO, Security, Ops, Developer, QA, Release Manager, and Knowledge Steward. |
| [Admin UX Polish IP-14](admin-ux-polish-ip14.md) | Admin console polish for credential state, memory/source filters, review routing, and operations status. |
| [Memory Quality Metrics IP-15](memory-quality-metrics-ip15.md) | Operations summary, Prometheus metrics, and admin readout for source-link coverage, stale/useful/missing feedback, role-boundary misses, and duplicates. |
| [Project Onboarding Runbook IP-16](project-onboarding-runbook-ip16.md) | Payload-safe new-project setup flow for scope, roles, grants, memory types, seed docs, source evidence, and review cadence. |
| [Release Evidence Bundle Automation IP-17](release-evidence-bundle-automation-ip17.md) | Payload-safe release bundle generator for tests, migration, health, operations, benchmark, backup/restore, and rollback evidence. |
| [Directory Sync Evaluation IP-18](directory-sync-evaluation-ip18.md) | Provisioning-only directory sync evaluation gate that keeps runtime authorization local. |
| [Project Success Benchmark PS-01](project-success-benchmark-ps01.md) | Pilot scorecard for whether governed memory improves real project work. |
| [Project Success Pilot Baseline PS-02](project-success-pilot-baseline-ps02.md) | First dogfood pilot selection, baseline pains, starting metrics, and observation entry criteria. |
| [Agent-Facing Memory Contract](agent-facing-memory-contract.md) | LMSS v1 agent-tool contract and safety semantics. |
| [Domain Model Extraction LR-04](domain-model-extraction-lr04.md) | Plan for moving stable IO-free concepts into `MemorySystem.Domain`. |
| [Decision 0044: Domain Model Extraction Slice](decisions/0044-domain-model-extraction-slice.md) | Accepted staged extraction boundary for stable IO-free domain concepts. |

### API

| Document | Purpose |
| --- | --- |
| [API Contracts](api/README.md) | Entry point for API contracts and runnable examples. |
| [Agent Memory OpenAPI v1](api/agent-memory-v1.openapi.json) | Curated OpenAPI contract for agent-facing endpoints. |
| [Agent Memory v1 Client Examples](api/agent-memory-v1-examples.md) | Curl-based end-to-end memory workflow. |
| [Agent Memory Client Wrapper IP-08](agent-memory-client-wrapper-ip08.md) | Local CLI wrapper for the required getContext, queryFacts, and feedback loop. |
| [Policy Targeting For Agent Callers](api/policy-targeting-for-agent-callers.md) | Principal, scope, namespace, trust, retention, sensitivity, and source evidence rules. |
| [Context Product v1 Caller Guide](api/context-product-v1-caller-guide.md) | Context explanations, safe exclusions, feedback, review handoff, and raw query hygiene. |
| [Context Packet Product v1 Contract](api/context-packet-product-v1.md) | Productized context packet contract and schema reference. |
| [API `memory.queryFacts` Implementation Plan](api/memory-query-facts-implementation-plan.md) | Implementation map for the fact-finding endpoint. |

### Local Workflow And Tests

| Document | Purpose |
| --- | --- |
| [Local Demo Workflow](private-alpha-workflow.md) | Repeatable local product path from evidence to memory, review, retrieval, feedback, export, and operations. |
| [Scenario 0001](scenarios/0001-user-preference-project-decision-cto-context.md) | Seeded demo story used by local workflows and benchmarks. |
| [Testing Commands](testing.md) | Fast, database-backed, TypeScript, backup/restore, benchmark, and smoke commands. |
| [Benchmarking Plan](benchmarking.md) | Product-level benchmark strategy and release gate criteria. |
| [Project Success Benchmark PS-01](project-success-benchmark-ps01.md) | Post-MVP pilot scorecard for adoption, quality, safety, operator burden, delivery impact, and confidence. |
| [Project Success Pilot Baseline PS-02](project-success-pilot-baseline-ps02.md) | Selected Long-Term Memory System dogfood pilot and baseline before the two-week observation run. |
| [LR-03 Benchmark Release-Gate Report](benchmark-release-gate-lr03.md) | First local benchmark release-gate baseline. |
| [Private Alpha 0.1 Release Notes](private-alpha-0.1-release.md) | Historical private-alpha baseline and verification notes. |

### Operations, Platform, And Compliance

| Document | Purpose |
| --- | --- |
| [Retention Policy](retention-policy.md) | Raw event payload retention, sensitivity, legal hold, erasure, and audit preservation. |
| [Backup and Restore Runbook](backup-restore.md) | PostgreSQL backup, restore, validation, and retention-aware recovery. |
| [Production Secret Handling](production-secrets.md) | Runtime secret configuration, rotation, and guardrails. |
| [Production Deployment Shape](production-deployment-shape.md) | Migrator/API/worker split, managed PostgreSQL expectations, rollback, and restore validation. |
| [External / Managed PostgreSQL Production Profile](external-managed-postgres-profile.md) | Local Docker-volume versus external managed PostgreSQL profile switch, migration flow, backup/restore validation, and smoke checks. |
| [Production Container Tool](production-container.md) | Single-machine Docker Compose operator tool and rollout path. |
| [Project Memory Boundary](project-memory-boundary.md) | Canonical local production endpoint, protected volume, project scope ids, role mapping, and seed command. |
| [Project-Defined Roles IP-05](project-defined-roles-ip05.md) | Project role-definition table, admin API, validation rules, and default role templates. |
| [Project Memory Runbook](project-memory-runbook.md) | Repeatable first execution slice, role-lens context checks, feedback loop, and weekly admin review habit. |
| [Project Onboarding Runbook IP-16](project-onboarding-runbook-ip16.md) | Product Owner and Knowledge Steward onboarding flow for a new project memory boundary. |
| [Weekly Admin Review Workflow IP-09](weekly-admin-review-workflow-ip09.md) | Weekly payload-safe memory review queue collector and role-owner operating procedure. |
| [Access Boundary Review IP-11](access-boundary-review-ip11.md) | Weekly security review for permission drift, service accounts, OIDC bindings, break-glass keys, and audit evidence. |
| [Role Lens First Content Pass IP-13](role-lens-first-content-pass-ip13.md) | Role-owner seed workflow for source-backed project role lenses. |
| [Admin UX Polish IP-14](admin-ux-polish-ip14.md) | `/admin/` console polish for memory inspection, role filters, source links, review routing, and operations status. |
| [Production Observability and Alerting](production-observability.md) | Pilot metrics, traces, logs, alerts, dashboards, and smoke checks. |
| [Production Platform Integration LR-05](production-platform-integration-lr05.md) | Infrastructure-as-code and managed platform integration boundary. |
| [Decision 0045: Production Platform Integration](decisions/0045-production-platform-integration.md) | Accepted platform integration boundary for IaC, managed PostgreSQL, telemetry, alert routing, and release checklists. |
| [Production Platform Baseline PI-01](production-platform-baseline-pi01.md) | AWS/Terraform/container platform baseline. |
| [Decision 0046: Production Platform And IaC Baseline](decisions/0046-production-platform-and-iac-baseline.md) | Accepted AWS, Terraform, and immutable OCI artifact baseline for the first platform target. |
| [Production Release Checklists PI-07](production-release-checklists-pi07.md) | Local, CI, pilot, and production release evidence gates. |
| [Release Evidence Bundle Automation IP-17](release-evidence-bundle-automation-ip17.md) | Release Manager and Ops bundle automation for payload-safe release evidence manifests. |
| [Production Platform Rehearsal PI-08](production-platform-rehearsal-pi08.md) | First isolated platform rehearsal evidence. |
| [Target-Environment Evidence Hardening IP-04](target-environment-evidence-hardening-ip04.md) | Payload-safe target evidence manifest, checksum verifier, and UAT completion rule for post-GO target artifacts. |
| [Production Backup And Restore Drill Schedule IP-10](production-backup-restore-drill-schedule-ip10.md) | Weekly, monthly, quarterly, post-erasure, and release-gate backup/restore drill cadence with RPO/RTO and evidence checks. |
| [Terraform Platform PI-02/PI-04](../infra/terraform/README.md) | Terraform module and environment layout for the AWS pilot target. |
| [Observability Artifacts](../observability/README.md) | Alerts, dashboard, trace coverage, and metric input manifests. |

### Governance And Compliance Track

| Document | Purpose |
| --- | --- |
| [Governance And Compliance Gate LR-06](governance-compliance-gate-lr06.md) | Governance/compliance release boundary and implementation plan. |
| [Decision 0048: Governance And Compliance Gate](decisions/0048-governance-compliance-gate.md) | Accepted compliance boundary for residency, erasure replay, drift reporting, retention policy, external payload checks, and evidence packages. |
| [Environment Governance Policy GC-01](environment-governance-policy-gc01.md) | Environment-specific data residency, retention, exceptions, and evidence policy. |
| [Permission-Drift Report GC-02](permission-drift-report-gc02.md) | Payload-safe access-drift report contract. |
| [Backup Erasure Replay Validation GC-03](backup-erasure-replay-validation-gc03.md) | Restore-time replay of post-backup erasure actions. |
| [Standard And Audit Retention Minimization GC-04](standard-audit-retention-minimization-gc04.md) | Dry-run/execute minimization job for old standard and audit payloads. |
| [External Payload Retention Check GC-05](external-payload-retention-check-gc05.md) | External payload-store retention verification contract. |
| [Compliance Evidence Package GC-06](compliance-evidence-package-gc06.md) | Payload-safe evidence package manifest and artifact index. |
| [Governance/Compliance Admin Console GC-07](governance-compliance-admin-console-gc07.md) | Admin compliance view and status endpoint contract. |
| [Governance/Compliance Release Smoke GC-08](governance-compliance-release-smoke-gc08.md) | One-command database-backed governance/compliance smoke. |

### Enterprise Access And Context Productization

| Document | Purpose |
| --- | --- |
| [Enterprise Access Gate](enterprise-access-gate.md) | OIDC/SSO, service accounts, access-management UI, audit export, and migration plan. |
| [Enterprise Access Pilot Operator Runbook](enterprise-access-pilot-operator-runbook.md) | Pilot workflow for OIDC, service accounts, grants, audit export, rollback, and break-glass keys. |
| [Enterprise Directory Sync Evaluation EA-10](enterprise-directory-sync-evaluation-ea10.md) | Decision to defer SCIM/directory sync until after pilot evidence. |
| [Directory Sync Evaluation IP-18](directory-sync-evaluation-ip18.md) | Product-improvement evaluation gate for SCIM/group sync pressure signals. |
| [Decision 0047: Directory Sync Is Provisioning Only](decisions/0047-directory-sync-provisioning-only.md) | Accepted constraint that future directory sync cannot bypass local memberships, roles, grants, previews, or audit records. |
| [Context Productization Gate](context-productization-gate.md) | Explainable context packets, safe exclusions, reviewer action, feedback, and ranking plan. |

### Release Evidence

| Document | Purpose |
| --- | --- |
| [Pilot Readiness Evidence Review](pilot-readiness-evidence-review-2026-06-01.md) | Historical evidence review and no-go decision before target-environment evidence. |
| [Target-Environment Pilot Rehearsal P0](target-environment-pilot-rehearsal-p0.md) | Target-environment rehearsal gates, evidence bundle, and go/no-go template. |
| [Pilot Release Evidence EPR-03](pilot-release-evidence-epr03-2026-06-04.md) | Local pilot-equivalent release evidence record. |
| [Release Evidence Bundle Automation IP-17](release-evidence-bundle-automation-ip17.md) | Generates the per-release payload-safe bundle and artifact index for release evidence. |
| [Target-Environment Evidence Hardening IP-04](target-environment-evidence-hardening-ip04.md) | Target evidence manifest and verifier contract, now completed for the accepted UAT target. |
| [UAT Target Evidence IP-04](release-evidence/uat-layout-fix-2026-06-08/ip04/README.md) | Verified UAT target evidence manifest, verifier output, and payload-safe gate artifacts for `uat-layout-fix-2026-06-08`. |
| [External Pilot Go/No-Go EPR-04](external-pilot-go-no-go-epr04-2026-06-04.md) | Historical no-go record retained for traceability. |
| [External Pilot GO EPR-04 v1.0.0](external-pilot-go-epr04-v1.0.0-2026-06-04.md) | Owner-approved GO replacement for version 1.0.0 external pilot. |
| [Documentation Truth Cleanup P1](documentation-truth-cleanup-p1-2026-06-04.md) | Reconciliation record that points current release decisions to the v1.0.0 GO record. |
| [Release Readiness Status Contract P2](release-readiness-status-contract-p2.md) | Contract for the machine-readable readiness status JSON. |
| [Pilot Operator Cockpit P3](pilot-operator-cockpit-p3.md) | Admin Pilot view and readiness endpoint plan. |

### Planning

| Document | Purpose |
| --- | --- |
| [Product Improvement Plan](product-improvement-plan.md) | Product-owner improvement plan from private alpha through production pilot. |
| [Roadmap](roadmap.md) | Milestones, dependencies, decision gates, and build sequence. |
| [Backlog](backlog.md) | Work items by milestone with priorities, statuses, and acceptance criteria. |
| [Memory vs Markdown Policy](memory-vs-markdown-policy.md) | Canonical policy for what should live in Markdown, memory, backlog, source evidence, role lenses, and release evidence. |
| [Canonical Memory Types IP-06](canonical-memory-types-ip06.md) | Source-of-truth memory type vocabulary for durable project memory and role lenses. |
| [Source-Backed Memory Hygiene Automation IP-07](source-backed-memory-hygiene-ip07.md) | Automation contract for validating source-backed memory seed evidence before reseeding. |
| [Agent Memory Client Wrapper IP-08](agent-memory-client-wrapper-ip08.md) | Wrapper contract for enforcing the agent memory prework and feedback loop. |
| [Weekly Admin Review Workflow IP-09](weekly-admin-review-workflow-ip09.md) | Structured weekly admin review queue contract for memory hygiene. |
| [Access Boundary Review IP-11](access-boundary-review-ip11.md) | Structured weekly access-boundary review contract for security posture. |
| [Backlog And Roadmap Memory Sync IP-12](backlog-roadmap-memory-sync-ip12.md) | Product Owner sync workflow for roadmap/backlog Markdown and source-backed memory. |
| [Memory Quality Metrics IP-15](memory-quality-metrics-ip15.md) | Tester/QA and Knowledge Steward quality metrics for operations, alerts, and admin review. |
| [Project Onboarding Runbook IP-16](project-onboarding-runbook-ip16.md) | Repeatable setup plan for scope, role owners, grants, source-backed seeds, and reviews. |
| [Release Evidence Bundle Automation IP-17](release-evidence-bundle-automation-ip17.md) | Release Manager workflow for the generated evidence bundle and strict promotion rule. |
| [Directory Sync Evaluation IP-18](directory-sync-evaluation-ip18.md) | Security/Ops workflow for deciding whether future provisioning-only sync is justified. |
| [Project Success Benchmark PS-01](project-success-benchmark-ps01.md) | Product Owner workflow for proving real project-success impact after the technical MVP promotion. |
| [Project Success Pilot Baseline PS-02](project-success-pilot-baseline-ps02.md) | Product Owner baseline record for the first internal dogfood pilot. |
| [Decisions](decisions/) | Architecture and implementation decision records. |

## Glossary

| Term | Meaning |
| --- | --- |
| Agent | An AI process or role that can use memory to complete tasks. |
| Context Builder | Read-control component that retrieves, filters, ranks, and compresses relevant memory before an LLM call. |
| Context packet | A compact, source-linked, explainable memory bundle built from authorized retrieval results. |
| Durable memory | Memory intended to persist beyond the current session or task. |
| Memory Broker | Write-control component that decides whether proposed memory should be stored, rejected, reviewed, expired, or treated as session-only. |
| Memory fact | Structured memory record with scope, provenance, confidence, lifecycle state, and policy metadata. |
| Namespace | Parsed path-like scope used to separate global, organization, user, project, role, agent, and session memory. |
| Outbox job | Retry-safe background work item used for embedding, indexing, export, review, expiry, redaction, or summaries. |
| Principal | A human, agent, or service account making a request. |
| Provenance | Evidence showing where a memory came from, usually through a source event. |
| Review dashboard | UI for approving, rejecting, editing, expiring, deleting, or superseding pending memory. |
| Trust level | Metadata that separates trusted system or human-approved content from user-scoped, agent-private, tool, web, or retrieved content. |
| Vector index | Search index used for semantic recall. It is not the source of truth. |
