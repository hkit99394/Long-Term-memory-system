# Admin UX Polish IP-14

Date completed: 2026-06-07

Status: implemented

## Purpose

IP-14 tightens the existing `/admin/` operator console around the workflows
operators already use most: memory inspection, login state, source evidence,
role-targeted filtering, review routing, and operations status.

## What Changed

- The console now shows whether the current credential is a JWT, API key, or
  missing credential.
- Memory inspection adds memory-type, role, and namespace-prefix filters.
- Source-event inspection adds the same role filter for role-scoped evidence.
- The admin memory/source endpoints accept a `roleId` query parameter and
  validate it against the supported role vocabulary.
- Memory and source-event details include direct review-queue routing.
- A new `Operations` view reads `GET /api/operations/summary` and surfaces API
  reachability, worker heartbeat, outbox, retrieval feedback, review/vault,
  context-product, and embedding-index health.
- The Operations side panel links to `/api/operations/summary`,
  `/api/operations/metrics`, `/health/ready`, and `/reviews/`.

## Runtime Contract

The console remains payload-safe: list views do not include raw source payloads
or hidden memory content. Operators must still open authorized source evidence
explicitly before seeing event payload content.

Role filtering is an inspection aid, not a permission bypass. The API applies
the same read authorization before returning memory facts or source events.

## Verification

Run:

```bash
cd tools/ui
npm run build
npm run check
cd ../..
dotnet test tests/MemorySystem.UnitTests/MemorySystem.UnitTests.csproj --filter FullyQualifiedName~AdminUxPolishIp14Tests
dotnet test tests/MemorySystem.IntegrationTests/MemorySystem.IntegrationTests.csproj --filter FullyQualifiedName~ApiAdminMemoryConsoleTests
```
