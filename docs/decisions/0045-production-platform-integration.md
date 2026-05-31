# Decision 0045: Production Platform Integration

Date: 2026-05-31

Status: Accepted

## Context

MR-10 proved the production-pilot deployment shape across separate migrator,
API, and worker roles. MR-11 made observability executable with versioned alert
rules, dashboard definitions, metric input manifests, trace coverage, and local
smoke checks. MR-12 added benchmark release gates. LR-01, LR-02, and LR-04 then
scoped enterprise access, context productization, and Domain extraction.

The missing production platform layer is now clear: the project needs a stable
infrastructure-as-code boundary, managed PostgreSQL and backup exporter
assumptions, runtime OpenTelemetry/exporter wiring, alert routing ownership,
and environment-specific release checklists. Those decisions should be made
before vendor-specific platform implementation starts.

## Decision

Adopt [LR-05 Production Platform Integration Plan](../production-platform-integration-lr05.md)
as the production platform integration scope.

The platform integration track will:

- stay platform-neutral until `PI-01` selects a target runtime and IaC baseline
- keep SQL schema ownership in `migrations/` and migration execution in
  `MemorySystem.Migrator`
- provision separate migrator, API, and worker runtime roles
- require managed PostgreSQL with pgvector support, encrypted backups, and a
  restore-to-new-database validation path
- add backup exporter evidence before production release gates rely on backup
  health
- add runtime OpenTelemetry exporters according to the existing trace coverage
  and payload-safety contract
- require alert routing owner, destination, severity, runbook link, silence
  policy, and test route per environment
- treat local, CI, pilot, and production as separate release checklists
- keep API contracts, database shape, authorization semantics, and benchmark
  gates stable unless a later decision explicitly changes them

## Consequences

- LR-05 completes as a planning and boundary decision, not a hidden platform
  implementation.
- Follow-on `PI-*` backlog items can implement platform integration in small,
  testable slices.
- Infrastructure-as-code can wrap the service, but it does not own memory
  policy, grants, retention, schema history, or payload-safety rules.
- Runtime OpenTelemetry work must preserve the existing rule against raw
  memory, query, event payload, proposal note, review note, embedding input,
  and secret leakage.
- Backup health is not considered production-ready until restore validation is
  visible as alertable evidence.
- A future cloud/vendor decision can implement this plan without changing the
  memory API, Domain extraction track, or benchmark contracts.
