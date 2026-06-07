#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
COMPOSE_FILE="${MEMORYSYSTEM_PRODUCTION_COMPOSE_FILE:-$ROOT_DIR/docker-compose.production.yml}"
ENV_FILE="${MEMORYSYSTEM_PRODUCTION_ENV_FILE:-$ROOT_DIR/.env.production}"

usage() {
  cat <<'USAGE'
Usage: scripts/production-container.sh <command> [args]

Commands:
  init-host  Create an untracked production env file from the example.
  generate-secrets
            Generate local production-shaped host secrets without printing them.
  preflight  Validate production-host prerequisites and secret placeholders.
  build     Build the v1.0.0 multi-role production image.
  config    Render the Docker Compose production configuration.
  migrate   Run the one-shot migrator role.
  up        Start runtime roles for the selected PostgreSQL profile.
  deploy    Build, migrate, start, and check health.
  enable-local-access
            Enable host-local browser access on 127.0.0.1:8081.
  seed-operator
            Seed the configured operator principal and admin namespace grants.
  health    Check API liveness and readiness inside the API container.
  status    Show production container status.
  logs      Follow production container logs. Pass service names as args.
  down      Stop the production compose stack.

Environment:
  MEMORYSYSTEM_PRODUCTION_ENV_FILE can point at the deployment env file.
  MEMORYSYSTEM_POSTGRES_PROFILE=local uses the protected Docker volume.
  MEMORYSYSTEM_POSTGRES_PROFILE=external uses a managed PostgreSQL connection string.
  MEMORYSYSTEM_LOCAL_ACCESS_ENABLED=true adds the loopback-only local proxy.
  MEMORYSYSTEM_PRODUCTION_TLS_ENABLED=true adds the Caddy TLS override.
  MEMORYSYSTEM_IMAGE defaults to memorysystem:1.0.0.
USAGE
}

read_env_value() {
  local key="$1"
  local fallback="$2"

  if [[ -n "${!key:-}" ]]; then
    printf '%s' "${!key}"
    return
  fi

  if [[ -f "$ENV_FILE" ]]; then
    local value
    value="$(awk -F= -v key="$key" '
      $0 !~ /^[[:space:]]*#/ && $1 == key {
        sub(/^[^=]*=/, "")
        print
      }
    ' "$ENV_FILE" | tail -n 1)"

    if [[ -n "$value" ]]; then
      value="${value%\"}"
      value="${value#\"}"
      value="${value%\'}"
      value="${value#\'}"
      printf '%s' "$value"
      return
    fi
  fi

  printf '%s' "$fallback"
}

compose() {
  local project_name
  local args

  project_name="$(read_env_value COMPOSE_PROJECT_NAME memorysystem-prod)"
  args=(--project-name "$project_name" --file "$COMPOSE_FILE")

  if is_external_postgres_profile; then
    args+=(--file "$ROOT_DIR/docker-compose.production.external-postgres.yml")
  fi

  if is_true "$(read_env_value MEMORYSYSTEM_PRODUCTION_TLS_ENABLED false)"; then
    args+=(--file "$ROOT_DIR/docker-compose.production.tls.yml")
  fi

  if is_true "$(read_env_value MEMORYSYSTEM_LOCAL_ACCESS_ENABLED false)"; then
    args+=(--file "$ROOT_DIR/docker-compose.production.local-access.yml")
  fi

  if [[ -f "$ENV_FILE" ]]; then
    args=(--env-file "$ENV_FILE" "${args[@]}")
  fi

  docker compose "${args[@]}" "$@"
}

postgres_profile() {
  local value

  value="$(read_env_value MEMORYSYSTEM_POSTGRES_PROFILE local)"

  case "$value" in
    local|LOCAL|Local|docker|Docker|docker-volume|Docker-volume)
      printf 'local'
      ;;
    external|EXTERNAL|External|managed|MANAGED|Managed)
      printf 'external'
      ;;
    *)
      printf 'invalid'
      ;;
  esac
}

is_external_postgres_profile() {
  [[ "$(postgres_profile)" == "external" ]]
}

validate_postgres_profile() {
  local profile

  profile="$(postgres_profile)"

  if [[ "$profile" == "invalid" ]]; then
    printf 'Invalid MEMORYSYSTEM_POSTGRES_PROFILE: use local or external.\n' >&2
    return 1
  fi
}

is_true() {
  case "$1" in
    1|true|TRUE|True|yes|YES|Yes|y|Y|on|ON|On)
      return 0
      ;;
    *)
      return 1
      ;;
  esac
}

runtime_services() {
  if ! is_external_postgres_profile; then
    printf '%s\n' postgres
  fi

  printf '%s\n' api worker

  if is_true "$(read_env_value MEMORYSYSTEM_PRODUCTION_TLS_ENABLED false)"; then
    printf '%s\n' caddy
  fi

  if is_true "$(read_env_value MEMORYSYSTEM_LOCAL_ACCESS_ENABLED false)"; then
    printf '%s\n' local-access
  fi
}

is_placeholder() {
  case "$1" in
    ""|placeholder|PLACEHOLDER|Placeholder|changeme|CHANGEME|change-me|CHANGE-ME|replace-with-principal-guid|replace-with-proxy-ip-or-cidr|example.com|ops@example.com)
      return 0
      ;;
    *)
      return 1
      ;;
  esac
}

validate_secret() {
  local key="$1"
  local value

  value="$(read_env_value "$key" "")"

  if is_placeholder "$value" || [[ "${#value}" -lt 16 ]]; then
    printf 'Invalid %s: configure a non-placeholder value of at least 16 characters.\n' "$key" >&2
    return 1
  fi
}

validate_external_postgres_connection_string() {
  local value
  local normalized_value
  local failures=0

  value="$(read_env_value MEMORYSYSTEM_POSTGRES_CONNECTION_STRING "")"
  normalized_value="${value,,}"

  if is_placeholder "$value" || [[ "${#value}" -lt 32 ]]; then
    printf 'Invalid MEMORYSYSTEM_POSTGRES_CONNECTION_STRING: configure a non-placeholder managed PostgreSQL connection string.\n' >&2
    return 1
  fi

  for required_part in "host=" "port=" "database=" "username=" "password="; do
    if [[ "$normalized_value" != *"$required_part"* ]]; then
      printf 'Invalid MEMORYSYSTEM_POSTGRES_CONNECTION_STRING: missing %s in the managed PostgreSQL connection string.\n' "$required_part" >&2
      failures=1
    fi
  done

  case "$normalized_value" in
    *"host=postgres"*|*"host=localhost"*|*"host=127.0.0.1"*|*"host=::1"*|*"memory_system_dev_password"*|*"password=placeholder"*|*"password=changeme"*|*"password=change-me"*)
      printf 'Invalid MEMORYSYSTEM_POSTGRES_CONNECTION_STRING: external profile must not point at the local Compose database or local/test password.\n' >&2
      failures=1
      ;;
  esac

  case "$normalized_value" in
    *"ssl mode=require"*|*"ssl mode=verifyca"*|*"ssl mode=verifyfull"*|*"sslmode=require"*|*"sslmode=verifyca"*|*"sslmode=verifyfull"*)
      ;;
    *)
      printf 'Invalid MEMORYSYSTEM_POSTGRES_CONNECTION_STRING: managed profile must require PostgreSQL TLS with SSL Mode=Require, VerifyCA, or VerifyFull.\n' >&2
      failures=1
      ;;
  esac

  return "$failures"
}

validate_guid() {
  local key="$1"
  local value

  value="$(read_env_value "$key" "")"

  if ! [[ "$value" =~ ^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$ ]]; then
    printf 'Invalid %s: configure a GUID for an active production principal.\n' "$key" >&2
    return 1
  fi
}

validate_loopback_or_explicit_public_bind() {
  local bind_key="$1"
  local override_key="$2"
  local label="$3"
  local value

  value="$(read_env_value "$bind_key" 127.0.0.1)"

  if [[ "$value" != "0.0.0.0" ]]; then
    return 0
  fi

  if is_true "$(read_env_value "$override_key" false)"; then
    printf 'Warning: %s=0.0.0.0 exposes %s because %s=true. Prefer 127.0.0.1 behind TLS.\n' "$bind_key" "$label" "$override_key" >&2
    return 0
  fi

  printf 'Invalid %s=0.0.0.0: this exposes %s. Keep 127.0.0.1 or set %s=true only for a reviewed public-bind deployment.\n' "$bind_key" "$label" "$override_key" >&2
  return 1
}

is_broad_forward_proxy_network() {
  case "$1" in
    0.0.0.0/0|::/0|10.0.0.0/8|172.16.0.0/12|192.168.0.0/16|fc00::/7|fe80::/10)
      return 0
      ;;
    *)
      return 1
      ;;
  esac
}

validate_forward_proxy_network() {
  local value

  value="$(read_env_value MEMORYSYSTEM_FORWARD_PROXY_NETWORK "")"

  if is_placeholder "$value"; then
    printf 'Invalid MEMORYSYSTEM_FORWARD_PROXY_NETWORK: configure the exact trusted TLS proxy IP/CIDR, for example 172.30.42.5/32 or a dedicated Compose subnet such as 172.30.42.0/24.\n' >&2
    return 1
  fi

  if [[ "$value" != */* ]]; then
    printf 'Invalid MEMORYSYSTEM_FORWARD_PROXY_NETWORK: configure CIDR notation such as 172.30.42.5/32, not a broad private range.\n' >&2
    return 1
  fi

  if is_broad_forward_proxy_network "$value"; then
    if is_true "$(read_env_value MEMORYSYSTEM_ALLOW_UNSAFE_BROAD_FORWARD_PROXY_NETWORK false)"; then
      printf 'Warning: MEMORYSYSTEM_FORWARD_PROXY_NETWORK=%s trusts a broad private range because MEMORYSYSTEM_ALLOW_UNSAFE_BROAD_FORWARD_PROXY_NETWORK=true. Prefer an exact proxy /32 or dedicated narrow subnet.\n' "$value" >&2
      return 0
    fi

    printf 'Invalid MEMORYSYSTEM_FORWARD_PROXY_NETWORK=%s: broad private proxy ranges are rejected. Configure the exact proxy /32 or a dedicated narrow subnet, or set MEMORYSYSTEM_ALLOW_UNSAFE_BROAD_FORWARD_PROXY_NETWORK=true only for a reviewed exception.\n' "$value" >&2
    return 1
  fi
}

validate_host() {
  local failures=0
  local image

  if [[ ! -f "$ENV_FILE" ]]; then
    printf 'Missing production env file: %s\n' "$ENV_FILE" >&2
    printf 'Run scripts/production-container.sh init-host, then replace every placeholder.\n' >&2
    return 1
  fi

  command -v docker >/dev/null 2>&1 || {
    printf 'Docker CLI is not installed or not on PATH.\n' >&2
    failures=1
  }

  if command -v docker >/dev/null 2>&1; then
    docker info >/dev/null 2>&1 || {
      printf 'Docker daemon is not reachable for the current user.\n' >&2
      failures=1
    }

    docker compose version >/dev/null 2>&1 || {
      printf 'Docker Compose plugin is not available.\n' >&2
      failures=1
    }
  fi

  validate_postgres_profile || failures=1
  if is_external_postgres_profile; then
    validate_external_postgres_connection_string || failures=1
  else
    validate_secret MEMORYSYSTEM_POSTGRES_PASSWORD || failures=1
  fi

  validate_secret MEMORYSYSTEM_OPERATOR_API_KEY || failures=1
  validate_guid MEMORYSYSTEM_OPERATOR_PRINCIPAL_ID || failures=1

  if [[ "$(read_env_value MEMORYSYSTEM_EMBEDDINGS_PROVIDER openai)" == "openai" ]]; then
    validate_secret OPENAI_API_KEY || failures=1
  fi

  validate_forward_proxy_network || failures=1

  validate_loopback_or_explicit_public_bind \
    MEMORYSYSTEM_API_BIND \
    MEMORYSYSTEM_ALLOW_UNSAFE_PUBLIC_API_BIND \
    "the API container port directly" \
    || failures=1

  if is_true "$(read_env_value MEMORYSYSTEM_LOCAL_ACCESS_ENABLED false)"; then
    validate_loopback_or_explicit_public_bind \
      MEMORYSYSTEM_LOCAL_ACCESS_BIND \
      MEMORYSYSTEM_ALLOW_UNSAFE_PUBLIC_LOCAL_ACCESS_BIND \
      "the local access proxy" \
      || failures=1
  fi

  if is_true "$(read_env_value MEMORYSYSTEM_PRODUCTION_TLS_ENABLED false)"; then
    if is_placeholder "$(read_env_value MEMORYSYSTEM_PUBLIC_HOSTNAME "")"; then
      printf 'Invalid MEMORYSYSTEM_PUBLIC_HOSTNAME: configure the public DNS name for Caddy TLS.\n' >&2
      failures=1
    fi

    if is_placeholder "$(read_env_value MEMORYSYSTEM_TLS_EMAIL "")"; then
      printf 'Invalid MEMORYSYSTEM_TLS_EMAIL: configure the certificate contact email for Caddy TLS.\n' >&2
      failures=1
    fi
  else
    printf 'Warning: Caddy TLS override is disabled. Confirm an external TLS terminator forwards X-Forwarded-Proto=https.\n' >&2
  fi

  image="$(read_env_value MEMORYSYSTEM_IMAGE memorysystem:1.0.0)"
  if command -v docker >/dev/null 2>&1 && ! docker image inspect "$image" >/dev/null 2>&1; then
    printf 'Warning: image %s is not built or loaded yet. Run scripts/production-container.sh build before migrate/up.\n' "$image" >&2
  fi

  compose config >/dev/null || failures=1

  if [[ "$failures" -ne 0 ]]; then
    return 1
  fi

  printf 'Production host preflight passed for %s.\n' "$ENV_FILE"
}

init_host() {
  if [[ -f "$ENV_FILE" ]]; then
    printf 'Production env file already exists: %s\n' "$ENV_FILE"
    return 0
  fi

  cp "$ROOT_DIR/.env.production.example" "$ENV_FILE"
  chmod 600 "$ENV_FILE"
  printf 'Created %s. Replace every placeholder before running preflight or deploy.\n' "$ENV_FILE"
}

random_secret() {
  if command -v openssl >/dev/null 2>&1; then
    openssl rand -hex 32
    return
  fi

  uuidgen | tr -d '-' | tr '[:upper:]' '[:lower:]'
  uuidgen | tr -d '-' | tr '[:upper:]' '[:lower:]'
}

random_guid() {
  if command -v uuidgen >/dev/null 2>&1; then
    uuidgen | tr '[:upper:]' '[:lower:]'
    return
  fi

  printf '%s-%s-%s-%s-%s\n' \
    "$(random_secret | cut -c 1-8)" \
    "$(random_secret | cut -c 1-4)" \
    "$(random_secret | cut -c 1-4)" \
    "$(random_secret | cut -c 1-4)" \
    "$(random_secret | cut -c 1-12)"
}

set_env_value() {
  local key="$1"
  local value="$2"
  local temp_file

  temp_file="$(mktemp "${TMPDIR:-/tmp}/memorysystem-env.XXXXXX")"
  awk -v key="$key" -v value="$value" '
    BEGIN { found = 0 }
    $0 !~ /^[[:space:]]*#/ && index($0, key "=") == 1 {
      print key "=" value
      found = 1
      next
    }
    { print }
    END {
      if (found == 0) {
        print key "=" value
      }
    }
  ' "$ENV_FILE" > "$temp_file"

  mv "$temp_file" "$ENV_FILE"
  chmod 600 "$ENV_FILE"
}

generate_secrets() {
  local source_revision
  init_host >/dev/null

  if is_true "$(read_env_value MEMORYSYSTEM_FORCE_SECRET_ROTATION false)" || is_placeholder "$(read_env_value MEMORYSYSTEM_POSTGRES_PASSWORD "")"; then
    set_env_value MEMORYSYSTEM_POSTGRES_PASSWORD "$(random_secret)"
  fi

  if is_true "$(read_env_value MEMORYSYSTEM_FORCE_SECRET_ROTATION false)" || is_placeholder "$(read_env_value MEMORYSYSTEM_OPERATOR_API_KEY "")"; then
    set_env_value MEMORYSYSTEM_OPERATOR_API_KEY "$(random_secret)"
  fi

  if is_true "$(read_env_value MEMORYSYSTEM_FORCE_SECRET_ROTATION false)" || is_placeholder "$(read_env_value MEMORYSYSTEM_OPERATOR_PRINCIPAL_ID "")"; then
    set_env_value MEMORYSYSTEM_OPERATOR_PRINCIPAL_ID "$(random_guid)"
  fi

  source_revision="$(read_env_value MEMORYSYSTEM_SOURCE_REVISION local)"
  if { is_placeholder "$source_revision" || [[ "$source_revision" == "local" ]]; } && command -v git >/dev/null 2>&1; then
    set_env_value MEMORYSYSTEM_SOURCE_REVISION "$(git -C "$ROOT_DIR" rev-parse --short=12 HEAD 2>/dev/null || printf 'local')"
  fi

  if is_placeholder "$(read_env_value MEMORYSYSTEM_BUILD_DATE unknown)" || [[ "$(read_env_value MEMORYSYSTEM_BUILD_DATE unknown)" == "unknown" ]]; then
    set_env_value MEMORYSYSTEM_BUILD_DATE "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
  fi

  printf 'Generated local production-shaped values in %s.\n' "$ENV_FILE"
  printf 'OPENAI_API_KEY, MEMORYSYSTEM_PUBLIC_HOSTNAME, and MEMORYSYSTEM_TLS_EMAIL still require real deployment values.\n'
}

enable_local_access() {
  local port

  init_host >/dev/null
  port="${1:-$(read_env_value MEMORYSYSTEM_LOCAL_ACCESS_PORT 8081)}"

  set_env_value MEMORYSYSTEM_LOCAL_ACCESS_ENABLED true
  set_env_value MEMORYSYSTEM_LOCAL_ACCESS_BIND 127.0.0.1
  set_env_value MEMORYSYSTEM_LOCAL_ACCESS_PORT "$port"

  printf 'Enabled host-local browser access in %s.\n' "$ENV_FILE"
  printf 'After starting the stack, open http://127.0.0.1:%s/admin/.\n' "$port"
}

seed_operator() {
  local connection_string
  local db_name
  local db_user
  local principal_id
  local display_name
  local psql_command=()

  validate_guid MEMORYSYSTEM_OPERATOR_PRINCIPAL_ID

  db_name="$(read_env_value MEMORYSYSTEM_POSTGRES_DB memory_system)"
  db_user="$(read_env_value MEMORYSYSTEM_POSTGRES_USER memory_system)"
  principal_id="$(read_env_value MEMORYSYSTEM_OPERATOR_PRINCIPAL_ID "")"
  display_name="$(read_env_value MEMORYSYSTEM_OPERATOR_DISPLAY_NAME "Production Operator")"

  if is_external_postgres_profile; then
    validate_external_postgres_connection_string
    command -v psql >/dev/null 2>&1 || {
      printf 'psql is required on the operator host for seed-operator with MEMORYSYSTEM_POSTGRES_PROFILE=external.\n' >&2
      return 1
    }
    connection_string="$(read_env_value MEMORYSYSTEM_POSTGRES_CONNECTION_STRING "")"
    psql_command=(psql "$connection_string")
  else
    psql_command=(compose exec -T postgres psql -U "$db_user" -d "$db_name")
  fi

  "${psql_command[@]}" \
    -v ON_ERROR_STOP=1 \
    -v principal_id="$principal_id" \
    -v display_name="$display_name" <<'SQL'
INSERT INTO principals (
    id,
    principal_type,
    display_name,
    status
)
VALUES (
    :'principal_id'::uuid,
    'human',
    :'display_name',
    'active'
)
ON CONFLICT (id)
DO UPDATE SET
    principal_type = EXCLUDED.principal_type,
    display_name = EXCLUDED.display_name,
    status = EXCLUDED.status,
    updated_at = now();

WITH namespace_roots(namespace_prefix) AS (
    VALUES
        ('/global'),
        ('/org'),
        ('/project'),
        ('/user'),
        ('/role'),
        ('/agent'),
        ('/session')
)
INSERT INTO memory_access_grants (
    id,
    principal_id,
    role_id,
    namespace_prefix,
    permission
)
SELECT
    (
        substr(md5(:'principal_id' || ':' || namespace_prefix || ':admin'), 1, 8) || '-' ||
        substr(md5(:'principal_id' || ':' || namespace_prefix || ':admin'), 9, 4) || '-' ||
        substr(md5(:'principal_id' || ':' || namespace_prefix || ':admin'), 13, 4) || '-' ||
        substr(md5(:'principal_id' || ':' || namespace_prefix || ':admin'), 17, 4) || '-' ||
        substr(md5(:'principal_id' || ':' || namespace_prefix || ':admin'), 21, 12)
    )::uuid,
    :'principal_id'::uuid,
    NULL,
    namespace_prefix,
    'admin'
FROM namespace_roots
ON CONFLICT (id)
DO UPDATE SET
    principal_id = EXCLUDED.principal_id,
    role_id = EXCLUDED.role_id,
    namespace_prefix = EXCLUDED.namespace_prefix,
    permission = EXCLUDED.permission;
SQL

  printf 'Seeded operator principal and admin namespace grants for %s.\n' "$principal_id"
}

build_image() {
  local image
  local version
  local source_revision
  local build_date

  image="$(read_env_value MEMORYSYSTEM_IMAGE memorysystem:1.0.0)"
  version="$(read_env_value MEMORYSYSTEM_SERVICE_VERSION 1.0.0)"
  source_revision="$(read_env_value MEMORYSYSTEM_SOURCE_REVISION local)"
  build_date="$(read_env_value MEMORYSYSTEM_BUILD_DATE unknown)"

  if [[ "$source_revision" == "local" ]] && command -v git >/dev/null 2>&1; then
    source_revision="$(git -C "$ROOT_DIR" rev-parse --short=12 HEAD 2>/dev/null || printf 'local')"
  fi

  if [[ "$build_date" == "unknown" ]]; then
    build_date="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
  fi

  docker build \
    --build-arg "VERSION=$version" \
    --build-arg "SOURCE_REVISION=$source_revision" \
    --build-arg "BUILD_DATE=$build_date" \
    --tag "$image" \
    "$ROOT_DIR"
}

health_once() {
  local failed=0

  compose exec -T api curl -fsS -H "X-Forwarded-Proto: https" http://127.0.0.1:8080/health/live || failed=1
  printf '\n'
  compose exec -T api curl -fsS -H "X-Forwarded-Proto: https" http://127.0.0.1:8080/health/ready || failed=1
  printf '\n'

  return "$failed"
}

health() {
  local retries
  local delay
  local attempt

  retries="$(read_env_value MEMORYSYSTEM_HEALTH_RETRIES 12)"
  delay="$(read_env_value MEMORYSYSTEM_HEALTH_RETRY_DELAY_SECONDS 5)"
  attempt=1

  while true; do
    if health_once; then
      return 0
    fi

    if [[ "$attempt" -ge "$retries" ]]; then
      printf 'API health checks did not pass after %s attempt(s).\n' "$retries" >&2
      return 1
    fi

    printf 'API health checks are not ready yet; retrying in %s second(s) (%s/%s).\n' "$delay" "$attempt" "$retries" >&2
    sleep "$delay"
    attempt=$((attempt + 1))
  done
}

command="${1:-help}"
shift || true

case "$command" in
  init-host)
    init_host
    ;;
  generate-secrets)
    generate_secrets
    ;;
  enable-local-access)
    enable_local_access "$@"
    ;;
  seed-operator)
    seed_operator
    ;;
  preflight)
    validate_host
    ;;
  build)
    build_image
    ;;
  config)
    compose config
    ;;
  migrate)
    compose --profile migrate run --rm migrator
    ;;
  up)
    compose up -d $(runtime_services)
    ;;
  deploy)
    validate_host
    build_image
    compose --profile migrate run --rm migrator
    compose up -d $(runtime_services)
    health
    ;;
  health)
    health
    ;;
  status)
    compose ps
    ;;
  logs)
    compose logs --tail="${MEMORYSYSTEM_LOG_TAIL:-200}" -f "$@"
    ;;
  down)
    compose down
    ;;
  help|-h|--help)
    usage
    ;;
  *)
    usage >&2
    exit 64
    ;;
esac
