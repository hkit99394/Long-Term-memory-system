# PI-01 Production Platform And IaC Baseline

Date: 2026-05-31

Status: Accepted

## Goal

PI-01 chooses the first production platform baseline so the follow-on `PI-*`
work can create infrastructure without changing API contracts, SQL schema
history, authorization semantics, or benchmark gates.

The baseline is intentionally pilot-sized. It gives the project a concrete
target for IaC, artifacts, secrets, runtime roles, and ownership while keeping
the application portable.

## Decision Summary

| Area | Decision |
| --- | --- |
| First target platform | AWS container platform for the production pilot. |
| Runtime service | Amazon ECS on Fargate for API and worker runtime roles. |
| One-shot jobs | ECS run-task or scheduled ECS tasks for migrator, backup/export, restore validation, benchmark gate, and platform smokes. |
| Database | Amazon RDS for PostgreSQL with `pgvector` support and restore-to-new-database validation. |
| Artifact | Immutable OCI container image in Amazon ECR, deployed by digest. |
| IaC | Terraform under `infra/terraform`, starting in PI-02. |
| Secrets | Secret values live in AWS Secrets Manager or SSM Parameter Store; Terraform references names or ARNs only. |
| Observability | Keep checked-in Prometheus/Grafana/trace artifacts as the contract; map them to AWS/platform exporters in later PI slices. |

AWS is a practical first pilot target because the current service already has a
container-friendly role split, PostgreSQL is the system of record, and official
AWS documentation lists `pgvector` for RDS PostgreSQL extension support. ECS
also supports standalone or scheduled tasks, which maps cleanly to the one-shot
migrator and restore-validation jobs.

References:

- [Amazon RDS for PostgreSQL extension versions](https://docs.aws.amazon.com/AmazonRDS/latest/PostgreSQLReleaseNotes/postgresql-extensions.html)
- [Amazon ECS standalone tasks](https://docs.aws.amazon.com/AmazonECS/latest/developerguide/standalone-tasks.html)
- [Terraform AWS provider documentation](https://registry.terraform.io/providers/hashicorp/aws/latest/docs)

## Artifact Contract

The first platform artifact is a single immutable OCI image per commit. The
image contains the published outputs for:

- `MemorySystem.Api`
- `MemorySystem.Worker`
- `MemorySystem.Migrator`
- `MemorySystem.DemoSeeder`

Each ECS task definition selects the role by command or entrypoint. API,
worker, migrator, and validation jobs must run the same image digest for a
release unless a later decision explicitly splits role images.

Rules:

- build once per commit
- tag by git SHA for human lookup
- deploy by image digest, not mutable tags
- keep `latest` out of pilot and production release records
- record image digest, git SHA, migration result, smoke result, and benchmark
  report in release evidence
- rollback application code by redeploying the previous digest
- do not attempt to roll back already-applied SQL migrations automatically

This makes the rollback unit visible and prevents API, worker, and migrator
version drift during a release.

## Terraform Layout

PI-02 creates the initial Terraform layout:

```text
infra/
  terraform/
    modules/
      memorysystem-runtime/
      memorysystem-postgres/
      memorysystem-observability/
    environments/
      pilot/
      production/
```

The exact module split can evolve, but environment overlays must remain
separate from application source. Terraform owns cloud resources and references
to externally managed secret values. It does not own SQL migrations, application
policy, test data, benchmark outputs, or raw secret values.

## Environment Model

| Environment | Purpose | Platform shape |
| --- | --- | --- |
| Local | Developer proof and debugging | Docker Compose PostgreSQL plus local `dotnet`/published-role scripts. |
| CI | Contract and regression checks | GitHub Actions with PostgreSQL service and no AWS resource mutation. |
| Pilot | First production-shaped environment | AWS ECS Fargate, ECR, RDS PostgreSQL, Secrets Manager or SSM, platform metrics/exporters, alert routing. |
| Production | Later hardened environment | Separate account or boundary, stricter change approval, backup/PITR evidence, restore validation, and release records. |

Pilot can start in a single AWS region. Production should use a separate
environment boundary before external users depend on it.

## Secret And State Rules

Terraform state must not contain API keys, OpenAI keys, database passwords, or
raw connection strings.

Allowed in Terraform:

- secret names or ARNs
- IAM role and policy references
- ECS task environment variable names
- non-secret hostnames, ports, database names, and resource identifiers

Not allowed in Terraform:

- API key values
- embedding provider keys
- PostgreSQL passwords or complete secret-bearing connection strings
- one-off operator credentials
- benchmark result payloads

Terraform backend state should be encrypted and locked. The bootstrap mechanism
for the backend can be manual for PI-02, but it must be documented before the
first platform rehearsal.

## Ownership Model

| Owner | Responsibilities |
| --- | --- |
| Application owner | API, worker, migrator, smoke scripts, benchmark gates, payload-safe telemetry contract. |
| Platform owner | Terraform modules, ECS services and tasks, ECR, networking, TLS/ingress, exporter wiring, alert routing. |
| Data/recovery owner | RDS configuration, backup/export evidence, restore validation, retention and erasure caveats. |
| Product/release owner | Go/no-go decisions, release notes, benchmark report acceptance, pilot communication. |

One person can temporarily hold multiple roles in private alpha, but release
records should still name the role being exercised.

## PI-02 Entry Criteria

PI-02 can start when:

- this baseline and Decision 0046 are linked from the docs index
- restore validation uses the central table manifest
- external platform metric alerts include missing-series coverage
- backlog marks PI-01 done and names the AWS/Terraform/ECS baseline
- no application endpoint, schema, or authorization behavior changed as part of
  PI-01

## PI-02 Result

PI-02 adds a minimal Terraform skeleton and Docker artifact contract only. It
does not create a full production environment by itself.

PI-02 deliverables:

- `infra/terraform/README.md`
- environment folder skeletons for `pilot` and `production`
- module placeholders for runtime, PostgreSQL, and observability
- variables for image digest, environment name, secret references, and database
  references
- a root `Dockerfile` for the multi-role OCI image
- tests or docs guards that prove Terraform does not expect secret values

## PI-03 Result

PI-03 adds the first live AWS resource shape: managed RDS PostgreSQL with
pgvector validation metadata.

PI-03 deliverables:

- an `aws_db_instance` PostgreSQL module with private subnet placement
- database security group ingress from approved client security groups or IPv4
  CIDR blocks
- RDS-managed master credential material with no raw secret values in Terraform
- storage encryption, backup retention, point-in-time recovery settings, final
  snapshot behavior, maintenance windows, and log exports
- a `pgvector_validation` output that records the migration SQL and extension
  check query
- pilot and production overlay variables for RDS sizing, network access,
  backup windows, and production Multi-AZ posture

## PI-04 Result

PI-04 adds the first executable recovery job shape for the platform baseline.
It does not yet provision ECS task definitions, but the multi-role image and
Terraform runtime contract now name the commands, inputs, evidence files, and
metrics expected by those tasks.

PI-04 deliverables:

- `platform-backup-export.sh` for custom-format PostgreSQL backup export,
  archive inspection, evidence JSON, and `memorysystem_backup_*` metrics
- `platform-restore-validation.sh` for restore-to-new-database validation,
  migration replay, restore table manifest checks, pgvector verification,
  evidence JSON, and `memorysystem_restore_validation_*` metrics
- multi-role container image support for PostgreSQL client tools and backup
  scripts under `/app/scripts/`
- runtime role contracts for scheduled backup export and run-task restore
  validation jobs
- pilot and production overlay schedule inputs plus updated observability
  metric manifests, dashboard references, and missing-series alert coverage

GC-03 extends this recovery shape with
`platform-erasure-replay-ledger-export.sh` and restore-time erasure replay
validation metrics/evidence so restored databases can prove post-backup
erasures still hide source payloads and derived projections before promotion.
GC-04 adds `platform-retention-minimization.sh` as a governance operator job
with dry-run/execute modes, legal-hold skips, external-payload skips, and
payload-safe retention evidence.
GC-05 adds `platform-external-payload-retention-check.sh` as the companion
governance operator job for external payload pointer inventory, provider-state
checks, disabled-policy violations, and payload-safe evidence.
GC-06 adds `platform-compliance-evidence-package.sh` as the package job that
links governance, backup/restore, release, benchmark, alert-route, and
permission-drift evidence into a payload-safe manifest and artifact index.
