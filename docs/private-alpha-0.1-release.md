# Private Alpha 0.1 Release Notes

Date: May 29, 2026

## Status

Private Alpha 0.1 is the Short Run completion baseline.

## What Shipped

- Top-level product entrypoint and quickstart.
- Private-alpha workflow documentation grounded in Scenario 0001.
- One-command Scenario 0001 seed/demo runner.
- Operational summary endpoint for API, worker, outbox, review, and vault-export state.
- Ephemeral event retention worker for expired unreferenced payload minimization.
- Backup/restore smoke script using custom-format PostgreSQL dumps and restore validation.
- Split migration schema and API integration tests for reviewability.
- Context-packet feedback log to start Middle Run retrieval-quality measurement.

## Verification

Verified on May 29, 2026:

```bash
dotnet build MemorySystem.sln --configuration Release
dotnet test MemorySystem.sln --configuration Release --no-build --filter "Category!=Database"
MEMORYSYSTEM_TEST_POSTGRES_CONNECTION_STRING='Host=127.0.0.1;Port=55432;Database=memory_system;Username=memory_system;Password=memory_system_dev_password' dotnet test tests/MemorySystem.IntegrationTests/MemorySystem.IntegrationTests.csproj --configuration Release --no-build --filter "Category=Database"
./scripts/seed-private-alpha-demo.sh
./scripts/backup-restore-smoke.sh
```

Results:

- Build succeeded with zero warnings.
- Fast suite passed: 238 unit tests and 34 non-database integration tests.
- Database suite passed: 177 tests.
- Scenario 0001 seed runner completed against the local PostgreSQL database.
- Backup/restore smoke passed with 20 restored migrations and `pgvector` verified.

## Known Caveats At Release Time

- API key configuration is still environment-driven.
- The review dashboard was functional, but the fuller admin memory/source
  inspection workflow was deferred to Middle Run.
- Retrieval feedback was captured as raw observations; aggregate operator
  metrics were deferred to Middle Run.

Middle Run has since added the admin inspection slices, retrieval feedback
metrics, governance endpoints, deployment smoke, observability artifacts, and
benchmark release gates. This release note remains the historical Short Run
baseline.
