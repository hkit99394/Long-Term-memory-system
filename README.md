# Long-Term Memory System

A durable, auditable long-term memory service for AI agents.

The system stores memory in PostgreSQL, uses pgvector for semantic recall, preserves source events as evidence, controls writes through a Memory Broker, controls reads through a Context Builder, and gives humans review and correction workflows.

This is not a prompt dump or a notes app. The product goal is trustworthy AI continuity: every durable memory should have scope, provenance, confidence, lifecycle state, and an access boundary.

## Quickstart

Prerequisites:

- .NET 10 SDK
- Docker with Docker Compose
- Node.js for TypeScript tool builds

Start PostgreSQL with pgvector:

```bash
docker compose up -d --wait postgres
```

Build and test the service:

```bash
dotnet restore MemorySystem.sln
dotnet build MemorySystem.sln --configuration Release --no-restore
dotnet test MemorySystem.sln --configuration Release --no-build --filter "Category!=Database"
MEMORYSYSTEM_REQUIRE_DATABASE_TESTS=true dotnet test tests/MemorySystem.IntegrationTests/MemorySystem.IntegrationTests.csproj --configuration Release --no-build --filter "Category=Database"
```

Run the API locally:

```bash
dotnet run --project src/MemorySystem.Api
```

Run the outbox worker in another terminal:

```bash
dotnet run --project src/MemorySystem.Worker
```

The worker also runs the first retention automation for expired, unreferenced `ephemeral` event payloads.

Useful local endpoints:

- `GET /health/live`
- `GET /health/ready`
- `GET /api/operations/summary`
- `GET /reviews/`

Most API endpoints require `X-Api-Key`. Development allows missing configured keys at startup, but authenticated flows need a configured API key that maps to an active principal row.

## Private Alpha Path

The first product workflow is documented in [Private Alpha Workflow](docs/private-alpha-workflow.md):

1. Append a source event.
2. Propose durable memory.
3. Review or correct the memory.
4. Retrieve a scoped context packet.
5. Export approved memory to the vault.
6. Check the operational summary.

Use [Scenario 0001](docs/scenarios/0001-user-preference-project-decision-cto-context.md) as the demo story.

## Documentation

Start here:

- [Project Goal](docs/project-goal.md)
- [Architecture Overview](docs/architecture.md)
- [Product Improvement Plan](docs/product-improvement-plan.md)
- [Roadmap](docs/roadmap.md)
- [Testing Commands](docs/testing.md)
- [Production Secret Handling](docs/production-secrets.md)

The full documentation index is [docs/README.md](docs/README.md).
