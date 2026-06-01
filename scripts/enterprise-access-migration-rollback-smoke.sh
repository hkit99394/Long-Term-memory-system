#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

CONFIGURATION="${MEMORYSYSTEM_ENTERPRISE_ACCESS_SMOKE_CONFIGURATION:-Release}"
TEST_FILTER="FullyQualifiedName~ApiEnterpriseAccessMigrationRollbackSmokeTests"

export MEMORYSYSTEM_REQUIRE_DATABASE_TESTS=true

printf '==> Enterprise access migration/rollback smoke\n'
printf '  %-30s %s\n' "Modes" "api-key-only, oidc-only, dual-auth, service-account, oidc-disabled-rollback"
printf '  %-30s %s\n' "Grant invariant" "memory_access_grants fingerprint unchanged"
printf '  %-30s %s\n' "Configuration" "$CONFIGURATION"

dotnet test tests/MemorySystem.IntegrationTests/MemorySystem.IntegrationTests.csproj \
  --configuration "$CONFIGURATION" \
  --filter "$TEST_FILTER" \
  -m:1 \
  /nodeReuse:false
