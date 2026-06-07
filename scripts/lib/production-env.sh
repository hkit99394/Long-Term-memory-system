#!/usr/bin/env bash

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

to_lower() {
  printf '%s' "$1" | tr '[:upper:]' '[:lower:]'
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
  local normalized_wrapped
  local failures=0

  value="$(read_env_value MEMORYSYSTEM_POSTGRES_CONNECTION_STRING "")"
  normalized_value="$(to_lower "$value")"
  normalized_wrapped=";$normalized_value;"

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

  case "$normalized_wrapped" in
    *";host=postgres;"*|*";host=localhost;"*|*";host=127.0.0.1;"*|*";host=::1;"*|*"memory_system_dev_password"*|*";password=placeholder;"*|*";password=changeme;"*|*";password=change-me;"*)
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
