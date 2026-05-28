# Production Secret Handling

## Purpose

This note defines how production API keys, PostgreSQL connection strings, and embedding provider credentials are supplied and rotated for the memory system.

Secrets must not be committed to source control. The repository keeps only non-secret defaults and placeholder examples. Development can use .NET Secret Manager or local environment variables. Production should use the deployment platform's secret store, with environment variables or mounted configuration used only as the delivery mechanism.

## Runtime Guardrails

The API and worker treat every environment except `Development` and `Testing` as production-shaped.

Runtime checks enforce these rules:

- API key authentication requires at least one configured key outside `Development` and `Testing`.
- Production-shaped API key values must be non-placeholder secrets with at least 16 characters.
- API key principal ids must be valid GUIDs, and duplicate API key values are rejected.
- PostgreSQL local Docker Compose defaults are allowed only in `Development` and `Testing`.
- PostgreSQL configuration outside `Development` and `Testing` must use a connection string or complete host, port, database, username, and password parts.
- PostgreSQL rejects the local development password outside `Development` and `Testing`.
- OpenAI embedding configuration requires an API key and an absolute HTTPS endpoint.
- OpenAI API keys outside `Development` and `Testing` must be non-placeholder secrets with at least 16 characters.
- The outbox worker refuses deterministic embeddings outside `Development` and `Testing`; API semantic routes return unavailable and readiness reports unhealthy until a production embedding provider is configured.
- The API rejects plain HTTP outside `Development` and `Testing`. Forwarded headers are trusted only when explicitly enabled and restricted to configured proxies or networks.

These guardrails are not a replacement for a managed secret store. They catch accidental local, test, or placeholder values before production traffic depends on them.

## API Keys

API keys are configured under:

```text
Authentication:ApiKey:Keys:{keyId}:Key
Authentication:ApiKey:Keys:{keyId}:PrincipalId
Authentication:ApiKey:Keys:{keyId}:DisplayName
```

`Key` is secret. `PrincipalId` and `DisplayName` are operational metadata, but they should still be managed with the same deployment change because the API maps each key to an active principal.

Example shape:

```text
Authentication:ApiKey:Keys:agent-service-2026-05:Key=<secret-api-key>
Authentication:ApiKey:Keys:agent-service-2026-05:PrincipalId=<principal-guid>
Authentication:ApiKey:Keys:agent-service-2026-05:DisplayName=Agent service
```

Rotation pattern:

1. Create or confirm the replacement principal is active.
2. Add a new key id and key value in the production secret store.
3. Restart or redeploy API instances so startup validation reads the new key.
4. Move callers to the new key.
5. Remove the old key id from configuration.
6. Restart or redeploy API instances again.
7. Record the rotation date, operator, key id, and affected principal.

Do not reuse the same key value under multiple key ids. The API rejects duplicate key values at startup.

## PostgreSQL

Preferred production input:

```text
ConnectionStrings:Postgres=<secret-postgres-connection-string>
```

The equivalent environment variable fallback is:

```text
MEMORYSYSTEM_POSTGRES_CONNECTION_STRING=<secret-postgres-connection-string>
```

When a platform cannot provide a single connection string, configure all parts:

```text
MEMORYSYSTEM_POSTGRES_HOST=<host>
MEMORYSYSTEM_POSTGRES_PORT=<port>
MEMORYSYSTEM_POSTGRES_DB=<database>
MEMORYSYSTEM_POSTGRES_USER=<username>
MEMORYSYSTEM_POSTGRES_PASSWORD=<secret-password>
```

Local Docker Compose defaults are:

```text
Host=localhost;Port=55432;Database=memory_system;Username=memory_system;Password=memory_system_dev_password
```

Those defaults are for local development only. Outside `Development` and `Testing`, the host must provide explicit production database configuration and must not use `memory_system_dev_password`.

Rotation pattern:

1. Provision the replacement database credential with least privilege.
2. Store the new connection string or password in the production secret store.
3. Restart the migrator, API, worker, backup, and restore jobs with the new secret.
4. Run the migration check and `/health/ready`.
5. Revoke the old database credential after all processes are confirmed on the replacement.
6. Record the rotation and any connection interruptions.

## Embedding Provider

Local and CI defaults use deterministic embeddings:

```text
Embeddings:Provider=deterministic
Embeddings:Model=memory-deterministic-v1
Embeddings:Dimension=32
```

Production semantic retrieval and indexing use OpenAI embeddings:

```text
Embeddings:Provider=openai
Embeddings:Model=text-embedding-3-small
Embeddings:Dimension=1536
Embeddings:Endpoint=https://api.openai.com/v1/embeddings
Embeddings:ApiKey=<secret-openai-api-key>
```

`OPENAI_API_KEY` can provide the same secret when `Embeddings:ApiKey` is not set.

The readiness check validates configuration without sending a live embedding request, so it does not prove the key has quota or provider-side permission. After rotation, verify an authenticated semantic or hybrid read path in addition to `/health/ready`.

Rotation pattern:

1. Create the replacement provider key in the provider account or managed secret broker.
2. Update `Embeddings:ApiKey` or `OPENAI_API_KEY` in the production secret store.
3. Restart or redeploy API and worker processes.
4. Confirm `/health/ready` and a semantic retrieval path.
5. Revoke the old provider key.
6. Record the provider key id, operator, and verification result.

## Transport Security

Production-shaped API environments require HTTPS. If the API receives direct HTTPS traffic, no forwarded-header trust configuration is required.

When the API runs behind a TLS-terminating reverse proxy, enable forwarded headers and restrict trust to the proxy addresses:

```text
TransportSecurity:ForwardedHeadersEnabled=true
ForwardedHeaders:KnownProxies:0=<proxy-ip>
ForwardedHeaders:KnownNetworks:0=<proxy-cidr>
```

Configure either `KnownProxies` or `KnownNetworks`. Values can be provided as indexed configuration entries or as comma/semicolon-delimited strings. Do not enable forwarded headers without a trusted proxy or network allowlist; startup validation rejects that shape outside `Development` and `Testing`.

## Operator Checklist

Before promoting a production configuration:

- no secret values are present in committed `appsettings` files
- API key values are unique, long-lived only by policy, and mapped to active principals
- direct HTTPS reaches the API, or forwarded headers are enabled with trusted proxy/network values
- PostgreSQL does not use local Docker Compose credentials
- OpenAI endpoint is HTTPS and the API key is stored outside the repository
- API and worker processes use the same PostgreSQL and embedding provider configuration
- `/health/live` and `/health/ready` are checked after deployment
- key rotations and database credential rotations have an operator record

Minimum deployment smoke checks:

```bash
dotnet run --project src/MemorySystem.Migrator -- \
  --connection-string "$MEMORYSYSTEM_POSTGRES_CONNECTION_STRING" \
  --migrations-directory migrations

curl -fsS "$MEMORYSYSTEM_API_BASE_URL/health/ready"
```

Run an authenticated read or write path after the health checks so API key principal resolution and database authorization are verified together.
