# Production Container Tool

## Purpose

This runbook turns the version 1.0.0 multi-role OCI image into a repeatable
single-machine Docker deployment tool. It complements the AWS/ECS platform
baseline: the runtime contract stays the same, but Docker Compose owns the
local machine process supervision.

The production container shape preserves the existing release boundaries:

- one image for API, worker, migrator, seeder, and operator scripts
- migrator runs once before traffic
- API and worker run as separate long-lived roles
- PostgreSQL with pgvector is the durable authority
- secrets are supplied by the machine's secret source, not committed files
- TLS is terminated before the API, with trusted forwarded headers configured

## Implementation Plan

1. Build the immutable v1.0.0 image.
   Use `scripts/production-container.sh build` to build
   `memorysystem:1.0.0` with OCI version, source revision, and build-date
   labels.
2. Configure the production host.
   Start from `.env.production.example`, replace every placeholder secret, and
   keep the real values outside source control. The API and worker reject
   placeholder API keys, placeholder OpenAI keys, deterministic embeddings, and
   local PostgreSQL defaults outside `Development` and `Testing`.
3. Terminate TLS before the API container.
   The compose file binds the API to `127.0.0.1` by default. Put Caddy, nginx,
   a load balancer, or another TLS terminator in front of it and set
   `MEMORYSYSTEM_FORWARD_PROXY_NETWORK` to the exact proxy CIDR that sends
   `X-Forwarded-Proto=https`.
4. Run migrations.
   Use `scripts/production-container.sh migrate` and capture the applied and
   skipped migration output in the release record.
5. Start the runtime roles.
   Use `scripts/production-container.sh up` for PostgreSQL, API, and worker.
   The API has a Docker liveness check; full readiness still depends on
   PostgreSQL, embedding configuration, outbox backlog, and worker heartbeat.
6. Verify and operate.
   Use `scripts/production-container.sh health`, then check the authenticated
   `/api/operations/summary` and `/api/operations/metrics` paths through the
   TLS endpoint.

## Tool Commands

```bash
scripts/production-container.sh init-host
scripts/production-container.sh preflight
scripts/production-container.sh build
scripts/production-container.sh migrate
scripts/production-container.sh up
scripts/production-container.sh health
scripts/production-container.sh status
scripts/production-container.sh logs api worker
```

For a full first deployment on a prepared host:

```bash
scripts/production-container.sh deploy
```

## Production Host Workflow

Initialize the untracked host env file:

```bash
scripts/production-container.sh init-host
```

Then edit `.env.production` on the host and replace every placeholder. The file
is created with mode `0600`; keep it that way or replace it with the host's
secret manager injection path.

Run preflight before every first deploy and after secret or proxy changes:

```bash
scripts/production-container.sh preflight
```

Preflight checks that Docker and Docker Compose are reachable, the env file
exists, required secrets are non-placeholder values, the operator principal id
is a GUID, OpenAI embeddings are not configured with a placeholder key, the
trusted proxy CIDR is set, and the compose configuration renders.

Preflight fails when `MEMORYSYSTEM_API_BIND=0.0.0.0` because that exposes the
API container port directly. Keep the default `127.0.0.1` bind unless a reviewed
deployment intentionally publishes the API port; that exception must set
`MEMORYSYSTEM_ALLOW_UNSAFE_PUBLIC_API_BIND=true`. When local browser access is
enabled, `MEMORYSYSTEM_LOCAL_ACCESS_BIND=0.0.0.0` is treated the same way and
requires `MEMORYSYSTEM_ALLOW_UNSAFE_PUBLIC_LOCAL_ACCESS_BIND=true`.

### TLS Option

The default production compose file binds the API to `127.0.0.1` and expects an
external TLS terminator. For a single-host deployment where this project should
own TLS termination, set:

```text
MEMORYSYSTEM_PRODUCTION_TLS_ENABLED=true
MEMORYSYSTEM_PUBLIC_HOSTNAME=memory.example.com
MEMORYSYSTEM_TLS_EMAIL=ops@example.com
MEMORYSYSTEM_DOCKER_NETWORK_CIDR=172.30.42.0/24
MEMORYSYSTEM_FORWARD_PROXY_NETWORK=172.30.42.0/24
MEMORYSYSTEM_ALLOW_UNSAFE_BROAD_FORWARD_PROXY_NETWORK=false
```

With TLS enabled, `scripts/production-container.sh up` also starts the Caddy
service from `docker-compose.production.tls.yml`. Caddy listens on ports 80 and
443, requests certificates for `MEMORYSYSTEM_PUBLIC_HOSTNAME`, and reverse
proxies to the API service over the dedicated private Docker network. Prefer an
exact proxy `/32` when the reverse proxy IP is fixed; otherwise use a narrow
dedicated subnet. Preflight rejects broad private ranges such as
`172.16.0.0/12` unless
`MEMORYSYSTEM_ALLOW_UNSAFE_BROAD_FORWARD_PROXY_NETWORK=true` is set for a
reviewed exception. The production and
local-access Caddy configs set security headers for the self-hosted admin and
review UI, including same-origin CSP, frame denial, `nosniff`, referrer policy,
and camera/geolocation/microphone permission denial.

### Local Browser Access

For a production-shaped single-host stack that should be inspected only from
the host itself, run:

```bash
scripts/production-container.sh enable-local-access
```

This enables the Caddy local-access proxy on `127.0.0.1:8081` and keeps
`X-Forwarded-Proto=https` flowing to the API without publishing the proxy on
all interfaces.

## Required Configuration

| Setting | Purpose |
| --- | --- |
| `MEMORYSYSTEM_POSTGRES_PASSWORD` | Production PostgreSQL password. |
| `MEMORYSYSTEM_OPERATOR_API_KEY` | Initial operator API key value. |
| `MEMORYSYSTEM_OPERATOR_PRINCIPAL_ID` | Active internal principal id for the operator key. |
| `OPENAI_API_KEY` | Production embedding provider key. |
| `MEMORYSYSTEM_DOCKER_NETWORK_CIDR` | Dedicated production Compose bridge subnet when Compose owns the reverse proxy network. |
| `MEMORYSYSTEM_FORWARD_PROXY_NETWORK` | Exact trusted TLS proxy `/32` or narrow dedicated proxy CIDR for forwarded HTTPS headers. |
| `MEMORYSYSTEM_PUBLIC_HOSTNAME` | Public DNS name when the Caddy TLS override is enabled. |
| `MEMORYSYSTEM_TLS_EMAIL` | Certificate contact email when the Caddy TLS override is enabled. |

The operator principal and any service-account credential must already exist in
the database for authenticated API calls to succeed. Health endpoints remain
anonymous, but operator and agent calls require active principals and grants.

## Rollback

Application rollback is an image rollback:

1. Stop or pause the worker with `scripts/production-container.sh down` or a
   targeted compose command.
2. Set `MEMORYSYSTEM_IMAGE` to the previous known-good image.
3. Run `scripts/production-container.sh up`.
4. Verify `/health/ready`, worker heartbeat, outbox dead letters, and an
   authenticated read path.

Do not edit old migration files or `schema_migrations`. If an incompatible
schema change must be backed out, follow the restore-to-new-database procedure
in [Backup and Restore Runbook](backup-restore.md).
