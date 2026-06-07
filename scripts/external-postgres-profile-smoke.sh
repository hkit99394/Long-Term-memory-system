#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RUN_ID="$(date -u +%Y%m%dT%H%M%SZ)_$$"
ENV_FILE="$(mktemp "${TMPDIR:-/tmp}/memorysystem-external-postgres-env.XXXXXX")"
CONFIG_FILE="$(mktemp "${TMPDIR:-/tmp}/memorysystem-external-postgres-config.XXXXXX")"

cleanup() {
  rm -f "$ENV_FILE" "$CONFIG_FILE"
}

trap cleanup EXIT

cat >"$ENV_FILE" <<'ENV'
COMPOSE_PROJECT_NAME=memorysystem-managed-smoke
MEMORYSYSTEM_IMAGE=memorysystem:1.0.0
MEMORYSYSTEM_SERVICE_VERSION=1.0.0
MEMORYSYSTEM_SOURCE_REVISION=external-profile-smoke
MEMORYSYSTEM_BUILD_DATE=unknown
MEMORYSYSTEM_POSTGRES_PROFILE=external
MEMORYSYSTEM_POSTGRES_DB=memory_system
MEMORYSYSTEM_POSTGRES_USER=memory_system
MEMORYSYSTEM_POSTGRES_PASSWORD=local-mode-placeholder-unused
MEMORYSYSTEM_POSTGRES_CONNECTION_STRING=Host=managed-postgres.example.internal;Port=5432;Database=memory_system;Username=memory_system;Password=managed-postgres-secret-1234567890;SSL Mode=Require
MEMORYSYSTEM_API_BIND=127.0.0.1
MEMORYSYSTEM_API_PORT=8080
MEMORYSYSTEM_ALLOW_UNSAFE_PUBLIC_API_BIND=false
MEMORYSYSTEM_LOCAL_ACCESS_ENABLED=false
MEMORYSYSTEM_LOCAL_ACCESS_BIND=127.0.0.1
MEMORYSYSTEM_LOCAL_ACCESS_PORT=8081
MEMORYSYSTEM_ALLOW_UNSAFE_PUBLIC_LOCAL_ACCESS_BIND=false
MEMORYSYSTEM_PRODUCTION_TLS_ENABLED=false
MEMORYSYSTEM_PUBLIC_HOSTNAME=example.com
MEMORYSYSTEM_TLS_EMAIL=ops@example.com
MEMORYSYSTEM_HTTP_PORT=80
MEMORYSYSTEM_HTTPS_PORT=443
MEMORYSYSTEM_DOCKER_NETWORK_CIDR=172.30.42.0/24
MEMORYSYSTEM_FORWARD_PROXY_NETWORK=172.30.42.0/24
MEMORYSYSTEM_ALLOW_UNSAFE_BROAD_FORWARD_PROXY_NETWORK=false
MEMORYSYSTEM_OPERATOR_API_KEY=managed-profile-operator-key-1234567890
MEMORYSYSTEM_OPERATOR_PRINCIPAL_ID=11111111-1111-4111-8111-111111111111
MEMORYSYSTEM_OPERATOR_DISPLAY_NAME=Managed Profile Operator
MEMORYSYSTEM_OPERATOR_CREDENTIAL_ID=
MEMORYSYSTEM_EMBEDDINGS_PROVIDER=openai
MEMORYSYSTEM_EMBEDDINGS_MODEL=text-embedding-3-small
MEMORYSYSTEM_EMBEDDINGS_DIMENSION=1536
MEMORYSYSTEM_EMBEDDINGS_ENDPOINT=https://api.openai.com/v1/embeddings
OPENAI_API_KEY=managed-profile-openai-key-1234567890
MEMORYSYSTEM_OUTBOX_WORKER_ID=managed-profile-worker-1
MEMORYSYSTEM_EPHEMERAL_EVENT_RETENTION_ENABLED=true
ENV

MEMORYSYSTEM_PRODUCTION_ENV_FILE="$ENV_FILE" \
  "$ROOT_DIR/scripts/production-container.sh" config >"$CONFIG_FILE"

for service in api worker migrator; do
  if awk -v service="$service" '
      $0 == "  " service ":" { in_service = 1; next }
      /^  [A-Za-z0-9_-]+:/ { in_service = 0 }
      in_service && /depends_on:/ { found = 1 }
      END { exit found ? 0 : 1 }
    ' "$CONFIG_FILE"; then
    printf 'External PostgreSQL profile still has a service dependency from %s.\n' "$service" >&2
    exit 1
  fi
done

if ! grep -q "MEMORYSYSTEM_POSTGRES_CONNECTION_STRING" "$CONFIG_FILE"; then
  printf 'Rendered config did not include MEMORYSYSTEM_POSTGRES_CONNECTION_STRING.\n' >&2
  exit 1
fi

printf 'External PostgreSQL profile smoke passed: %s\n' "$RUN_ID"
