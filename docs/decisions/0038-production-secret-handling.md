# 0038 Production Secret Handling

## Status

Accepted.

## Context

M8-05 closes the operational readiness backlog with explicit handling for production secrets. The system already has API-key authentication, PostgreSQL connection string resolution, and OpenAI embedding provider configuration, but operators need one documented path and runtime checks that catch accidental local, test, or placeholder values.

The API and worker run through standard .NET configuration providers, so production deployments can source secrets from environment variables, mounted configuration, cloud secret stores, or orchestrator-specific injection. The repository should not encode a cloud-specific secret manager yet.

## Decision

Adopt [Production Secret Handling](../production-secrets.md) as the MVP operator guide.

Add runtime guardrails for every environment except `Development` and `Testing`:

- API-key authentication requires configured keys, valid principal ids, distinct key values, and production-safe key values.
- PostgreSQL connection resolution requires either a full connection string or complete connection parts, and rejects the local Docker Compose profile and development password.
- OpenAI embedding configuration requires an HTTPS endpoint and a non-placeholder API key.
- The outbox worker continues to refuse deterministic embeddings outside `Development` and `Testing`.
- The API keeps non-semantic routes available when deterministic embeddings are configured outside `Development` and `Testing`, but readiness and semantic routes remain unavailable until a production provider is configured.
- The API requires HTTPS outside `Development` and `Testing`; forwarded headers are accepted only when explicitly enabled and restricted to configured proxies or networks.

Production secret values are still expected to live in a managed secret store. Environment variables are an acceptable delivery mechanism, not the long-term source of record.

## Consequences

- A production-shaped process fails during startup when API keys, database credentials, or embedding credentials are missing or clearly placeholder values.
- Local development and CI keep deterministic embeddings and Docker Compose PostgreSQL defaults without extra secret setup.
- The API can still expose liveness, structured reads, and non-semantic surfaces during a staged embedding rollout, while readiness calls out the unsafe semantic configuration.
- Future deployment-specific work can add a concrete cloud secret provider without changing the application configuration keys.
