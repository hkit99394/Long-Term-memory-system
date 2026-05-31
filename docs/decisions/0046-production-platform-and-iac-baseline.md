# Decision 0046: Production Platform And IaC Baseline

Date: 2026-05-31

Status: Accepted

## Context

LR-05 defined the production platform integration boundary. The pre-PI-01 review
found three issues to close before implementation: restore validation table
coverage could drift from migrations, external platform metrics needed
missing-series alert coverage, and PI-01 needed an explicit artifact and
rollback contract.

The project already has a clean runtime split: one-shot migrator, long-running
API, long-running worker, and local smoke scripts that prove backup and restore
validation. The first platform baseline should preserve that shape.

## Decision

Adopt [PI-01 Production Platform And IaC Baseline](../production-platform-baseline-pi01.md).

The first production-pilot target is:

- AWS as the first platform target
- Amazon ECS on Fargate for API and worker services
- ECS one-shot or scheduled tasks for migrator, backup/export,
  restore-validation, benchmark-gate, and smoke jobs
- Amazon RDS for PostgreSQL with `pgvector` support for the database authority
- Amazon ECR for immutable OCI image artifacts
- AWS Secrets Manager or SSM Parameter Store for secret values
- Terraform under `infra/terraform` as the first IaC baseline
- checked-in observability artifacts as the alert/dashboard/trace contract,
  with platform exporter wiring added in later PI slices

The artifact contract is a single immutable multi-role OCI image per commit.
API, worker, migrator, and validation jobs select their executable by command or
entrypoint and should use the same image digest for a release. Rollback means
redeploying the previous image digest and verifying health, metrics, smoke, and
benchmark gates. SQL migrations remain forward-only unless a later decision
approves a restore-based recovery.

Terraform may create cloud resources and secret references, but must not store
API key values, embedding provider keys, database passwords, complete
secret-bearing connection strings, or benchmark outputs in state.

## Consequences

- PI-02 can create a concrete `infra/terraform` skeleton instead of another
  abstract platform plan.
- Platform implementation starts with AWS ECS/RDS/ECR/Secrets Manager concepts,
  while the application remains portable because API contracts and database
  migrations do not change.
- The single-image release unit reduces API/worker/migrator version drift.
- Secret injection and Terraform state rules become part of the platform review
  from the first IaC slice.
- A future provider change requires a new decision, but not a rewrite of the
  memory API, Domain model, or PostgreSQL schema.
