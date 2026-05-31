# LR-04 Domain Model Extraction Plan

Date: 2026-05-31

Status: Planning accepted

## Goal

Move stable, IO-free memory concepts from Application and Infrastructure into
`MemorySystem.Domain` without changing database schema, HTTP endpoints, response
contracts, benchmark semantics, or caller behavior.

LR-04 is a planning-first slice. It inventories the stable concepts, defines the
target domain value objects, names the compatibility tests, and splits extraction
into small follow-on backlog items. The actual code extraction starts after this
plan with `DM-*` implementation work.

## Non-Goals

- No database migrations.
- No endpoint, OpenAPI, or response DTO churn.
- No broad namespace, lifecycle, trust, retention, or sensitivity renaming.
- No SQL rewrite beyond boundary mapping needed by later implementation slices.
- No dependency from Domain to Application, Infrastructure, API, ASP.NET Core,
  Npgsql, filesystem, network, or provider adapters.

## Current Inventory

| Concept | Current Home | Durable Rule | Target Domain Shape | Compatibility Checks |
| --- | --- | --- | --- | --- |
| Memory scope | `MemoryScopePolicy`, `MemoryScopeResolver`, `EventScope`, `MemoryContextTargetScope`, repository queries | Supported scope types are `global`, `org`, `user`, `project`, `role`, `agent`, and `session`; GUID-backed scopes normalize to canonical GUID strings; global scope id is `global`; session scope id cannot be `global`. | `MemoryScope`, `MemoryScopeType`, `MemoryScopeId` | Existing `MemoryScopePolicyTests`, scope resolver tests, API request scope tests, and database-backed proposal/read tests must keep the same pass/fail behavior and error text where public. |
| Namespace | `MemoryNamespaceParser`, `MemoryNamespace`, SQL namespace predicates, access grants | Namespaces are path-like, begin with `/`, reject empty or relative segments, carry scope metadata, and can carry a role segment for project or org role namespaces. | `MemoryNamespace`, `MemoryNamespacePath`, `MemoryNamespaceParser` facade | `MemoryNamespaceParserTests` become golden compatibility tests; grant and hybrid search database tests prove authorization predicates do not drift. |
| Role id | `MemoryScopePolicy.RoleIds`, role namespace parsing, role lens repository rules | Supported roles are normalized to lower-case known role ids; unsupported role ids fail closed. | `MemoryRoleId`, `KnownMemoryRoles` | Role namespace parser tests, role-lens base fact validation tests, and role-targeted context/search integration tests keep existing behavior. |
| Trust level | `MemoryScopePolicy.TrustLevels`, `MemoryTrustPolicy`, source event fields, broker policy | Known trust levels determine whether external writes are accepted, capped, reviewed, or treated as untrusted evidence. | `MemoryTrustLevel` | Broker policy tests, proposal workflow tests, and source-event append tests preserve accepted values and rejection behavior. |
| Lifecycle status | `MemoryFactStatuses`, repository validation, direct reads, search filters, review/export workflows | Normal retrieval uses `active` only; inactive states remain queryable by explicit lifecycle workflows and audit paths. | `MemoryLifecycleStatus` | `MemoryFactStatusesTests`, direct read tests, search/context tests, review action tests, and vault export tests keep active-only defaults and inactive visibility rules. |
| Retention class | Event append defaults, retention policy docs, admin governance stores, retention reports | Retention class controls raw payload retention and governance reporting; erasure and legal hold remain explicit policy states. | `MemoryRetentionClass` | Governance integration tests, retention report tests, and backup/restore smoke remain unchanged; no retention values are renamed. |
| Sensitivity | `MemoryScopePolicy.Sensitivities`, event fields, hybrid search sensitive filters, admin source-event browser | Sensitivity controls safe handling, redaction pressure, and context/search exclusion behavior for sensitive source evidence. | `MemorySensitivity` | Admin governance tests, source-event search tests, hybrid/context tests, and context product benchmark safe-exclusion checks remain unchanged. |
| Source evidence | `SourceEventReference`, `MemoryContextSourceEvent`, source link builders, fact-finding responses, review/admin source reads | Every durable memory claim must keep a source event reference or safe source link; callers can inspect evidence only through authorized read paths. | `SourceEvidenceReference`, `SourceEvidenceLink` | Proposal, query-facts, context packet, admin inspection, and source evidence read tests preserve source id/link shape and authorization behavior. |
| Retrieval feedback type | `MemoryRetrievalFeedbackTypes`, context feedback observation stores, hybrid ranking signals | Feedback actions normalize to supported action names and item-level actions require source evidence. | `MemoryRetrievalFeedbackType` after context feedback stabilizes | Feedback type unit tests, context feedback API tests, ranking adjustment tests, and context-product benchmark checks keep current action semantics. |

## Target Domain Package

`MemorySystem.Domain` stays dependency-free and deterministic. It should contain
small value objects and vocabulary types, not data-access repositories or HTTP
models.

Initial target folders:

- `Scopes/MemoryScope.cs`
- `Scopes/MemoryScopeType.cs`
- `Scopes/MemoryScopeId.cs`
- `Namespaces/MemoryNamespace.cs`
- `Namespaces/MemoryNamespaceParser.cs`
- `Roles/MemoryRoleId.cs`
- `Trust/MemoryTrustLevel.cs`
- `Lifecycle/MemoryLifecycleStatus.cs`
- `Retention/MemoryRetentionClass.cs`
- `Sensitivity/MemorySensitivity.cs`
- `Evidence/SourceEvidenceReference.cs`
- `Evidence/SourceEvidenceLink.cs`
- `Retrieval/MemoryRetrievalFeedbackType.cs`

The first implementation should favor static `TryParse` or `TryNormalize`
methods and string round-tripping over clever abstractions. Public API DTOs and
database columns can continue to use strings while Application and
Infrastructure map those strings at their boundaries.

## Extraction Sequence

| Slice | Status | Scope | Exit Criteria |
| --- | --- | --- | --- |
| LR-04A | Done by this plan | Inventory stable concepts, accept extraction decision, define compatibility gates, and add follow-on backlog items. | Plan, decision record, backlog, doc index, and doc guard test exist. |
| DM-01 | Done | Add pure Domain value objects and vocabulary types with no callers migrated. | Domain builds independently; unit tests prove accepted values, normalization, and string round-trips match current Application policy. |
| DM-02 | Done | Move namespace parsing rules behind a Domain parser while keeping the Application facade and public error behavior stable. | Existing `MemoryNamespaceParserTests` pass unchanged; compatibility tests prove Application and Domain parsing return the same scope metadata, role ids, segments, errors, and scope prefixes with no schema changes. |
| DM-03 | Done | Extract lifecycle, trust, retention, and sensitivity vocabularies, then adapt Application constants to Domain. | `MemoryFactStatuses`, `MemoryTrustPolicy`, retention, sensitivity, proposal, review, and admin tests pass without endpoint or OpenAPI diffs. |
| DM-04 | Done | Introduce source evidence references in Domain and map them in Application responses and Infrastructure rows. | Query-facts, context packets, review/admin evidence paths, and source evidence reads preserve existing JSON shape and authorization behavior. |
| DM-05 | Done | Move Infrastructure boundary mapping to Domain value objects while leaving SQL schema, predicates, and migration history unchanged. | Database-backed proposal/read/search/context/governance tests pass; no new migration is required. |
| DM-06 | Todo | Remove duplicate string normalization helpers only after callers are migrated. | Application keeps thin facades for compatibility; `rg` shows stable concepts are no longer independently redefined in multiple layers. |

## Compatibility Test Matrix

Compatibility tests come before extraction. Each implementation slice should run
the smallest useful set locally, then the wider database-backed suite when a
repository, access predicate, or response contract is touched.

| Area | Required Checks |
| --- | --- |
| Scope normalization | `MemoryScopePolicyTests`, scope resolver tests, API proposal/request scope tests. |
| Namespace parsing | `MemoryNamespaceParserTests` as golden cases, plus membership/grant and hybrid-search authorization integration tests. |
| Lifecycle status | `MemoryFactStatusesTests`, direct read tests, structured search tests, review action tests, vault export tests. |
| Trust, retention, sensitivity | Broker policy tests, event append tests, admin source-event browser tests, governance retention tests, context sensitive-exclusion tests. |
| Source evidence | `ApiMemoryProposalTests`, `ApiMemorySearchTests.ContextPacket`, query-facts tests, admin inspection tests, source evidence read tests. |
| Agent-facing contracts | OpenAPI artifact checks, caller docs tests, context product caller docs tests, and no intentional JSON shape changes. |
| Benchmarks | Scenario 0001 private-alpha seed/demo, agent-contract usefulness smoke when fact-finding changes, and context-product benchmark smoke when context packet or feedback ranking paths change. |

## Risk Register

| Risk | Severity | Likelihood | Why It Matters | Mitigation |
| --- | --- | --- | --- | --- |
| Public validation text drifts during extraction. | Medium | Medium | Client tests and docs already rely on stable policy failures. | Keep Application facades during migration and add golden tests before moving parsing. |
| Authorization predicates drift from Domain normalization. | High | Medium | Scope, namespace, role, and sensitivity mistakes can leak hidden memory. | Run database-backed access, search, context, and admin tests for every boundary migration. |
| Schema churn sneaks into a model cleanup. | High | Low | LR-04 should improve maintainability without forcing production migration or endpoint coordination. | Treat schema changes as out of scope; reject migrations in `DM-*` unless a later decision explicitly changes scope. |
| Domain types become too coupled to current SQL rows or DTOs. | Medium | Medium | The Domain layer should model stable concepts, not persistence shapes. | Keep strings at API and database boundaries; map into value objects only inside Application and Infrastructure seams. |
| Extraction creates circular project references. | High | Low | Domain must remain the shared foundation. | Domain has no project references; Application references Domain; Infrastructure references Application and Domain transitively as needed. |
| Benchmarks pass but human behavior regresses. | Medium | Low | Domain extraction can look safe in unit tests while weakening source evidence or review flows. | Keep Scenario 0001 and context-product benchmark checks in the release path after fact-finding or context changes. |

## Exit Criteria

LR-04 is complete when this plan, Decision 0044, backlog follow-up items, and a
doc guard test are in place.

The full Domain extraction track is complete only when:

- `MemorySystem.Domain` owns the stable value objects listed above.
- Application exposes compatibility facades or adapters where callers still use
  current names.
- Infrastructure maps database strings into Domain types at repository
  boundaries without schema churn.
- API/OpenAPI response shapes remain stable unless a later contract decision
  explicitly changes them.
- Unit, database-backed integration, and relevant benchmark smoke checks pass.
