#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

CONFIGURATION="${MEMORYSYSTEM_GOVERNANCE_COMPLIANCE_SMOKE_CONFIGURATION:-Release}"
TEST_FILTER="FullyQualifiedName~GovernanceComplianceReleaseSmokeTests"

export MEMORYSYSTEM_REQUIRE_DATABASE_TESTS=true

printf '==> Governance/compliance release smoke\n'
printf '  %-30s %s\n' "Policy config" "environment governance policy evidence"
printf '  %-30s %s\n' "Governance reports" "permission drift, audit export, retention report, legal holds"
printf '  %-30s %s\n' "Platform evidence" "retention dry run, external payload check, erasure replay ledger"
printf '  %-30s %s\n' "Evidence package" "strict compliance manifest, artifact index, SHA-256 sidecar"
printf '  %-30s %s\n' "Configuration" "$CONFIGURATION"

dotnet test tests/MemorySystem.IntegrationTests/MemorySystem.IntegrationTests.csproj \
  --configuration "$CONFIGURATION" \
  --filter "$TEST_FILTER" \
  -m:1 \
  /nodeReuse:false
