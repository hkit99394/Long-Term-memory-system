#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

. "$ROOT_DIR/scripts/restore-validation-tables.sh"

POSTGRES_USER="${MEMORYSYSTEM_POSTGRES_USER:-memory_system}"
POSTGRES_PASSWORD="${MEMORYSYSTEM_POSTGRES_PASSWORD:-memory_system_dev_password}"
POSTGRES_PORT="${MEMORYSYSTEM_POSTGRES_PORT:-55432}"
API_PORT="${MEMORYSYSTEM_PRODUCTION_PILOT_SMOKE_API_PORT:-5199}"
API_KEY="${MEMORYSYSTEM_PRODUCTION_PILOT_SMOKE_API_KEY:-private-alpha-local-key}"
PRINCIPAL_ID="11111111-1111-4111-8111-111111111111"
PROJECT_ID="33333333-3333-4333-8333-333333333333"
RUN_ID="$(date -u +%Y%m%d%H%M%S)_$$"
SMOKE_DB="${MEMORYSYSTEM_PRODUCTION_PILOT_SMOKE_DB:-memorysystem_pilot_smoke_$RUN_ID}"
RESTORE_DB="${MEMORYSYSTEM_PRODUCTION_PILOT_RESTORE_DB:-memorysystem_pilot_restore_$RUN_ID}"
if [[ -n "${MEMORYSYSTEM_PRODUCTION_PILOT_SMOKE_WORK_DIR:-}" ]]; then
  WORK_DIR="$MEMORYSYSTEM_PRODUCTION_PILOT_SMOKE_WORK_DIR"
  created_work_dir=false
else
  WORK_DIR="$(mktemp -d "${TMPDIR:-/tmp}/memorysystem-pilot-smoke.XXXXXX")"
  created_work_dir=true
fi
PUBLISH_DIR="$WORK_DIR/publish"
BACKUP_FILE="$WORK_DIR/$SMOKE_DB.dump"
KEEP_DATABASES="${MEMORYSYSTEM_PRODUCTION_PILOT_SMOKE_KEEP_DATABASES:-false}"
KEEP_WORK_DIR="${MEMORYSYSTEM_PRODUCTION_PILOT_SMOKE_KEEP_WORK_DIR:-false}"

api_pid=""
worker_pid=""
created_smoke_db=false
created_restore_db=false

log() {
  printf '\n==> %s\n' "$1"
}

stop_process() {
  local pid="$1"
  local name="$2"

  if [[ -n "$pid" ]] && kill -0 "$pid" >/dev/null 2>&1; then
    kill "$pid" >/dev/null 2>&1 || true
    wait "$pid" >/dev/null 2>&1 || true
    printf '  %-32s stopped\n' "$name"
  fi
}

cleanup() {
  stop_process "$worker_pid" "Worker role"
  stop_process "$api_pid" "API role"

  if [[ "$KEEP_DATABASES" != "true" ]]; then
    if [[ "$created_restore_db" == "true" ]]; then
      docker compose exec -T postgres sh -lc \
        'dropdb -U "$POSTGRES_USER" --if-exists "$1" >/dev/null' \
        sh "$RESTORE_DB" || true
    fi

    if [[ "$created_smoke_db" == "true" ]]; then
      docker compose exec -T postgres sh -lc \
        'dropdb -U "$POSTGRES_USER" --if-exists "$1" >/dev/null' \
        sh "$SMOKE_DB" || true
    fi
  fi

  if [[ "$created_work_dir" == "true" && "$KEEP_WORK_DIR" != "true" ]]; then
    rm -rf "$WORK_DIR"
  else
    echo "Kept smoke work directory: $WORK_DIR"
  fi
}

trap cleanup EXIT

connection_string() {
  local database_name="$1"

  printf 'Host=127.0.0.1;Port=%s;Database=%s;Username=%s;Password=%s' \
    "$POSTGRES_PORT" "$database_name" "$POSTGRES_USER" "$POSTGRES_PASSWORD"
}

db_scalar() {
  local database_name="$1"
  local sql="$2"

  docker compose exec -T postgres sh -lc \
    'psql -U "$POSTGRES_USER" -d "$1" -At -v ON_ERROR_STOP=1 -c "$2"' \
    sh "$database_name" "$sql"
}

create_database() {
  local database_name="$1"

  docker compose exec -T postgres sh -lc \
    'dropdb -U "$POSTGRES_USER" --if-exists "$1" >/dev/null && createdb -U "$POSTGRES_USER" "$1"' \
    sh "$database_name"
}

ensure_process_running() {
  local pid="$1"
  local name="$2"
  local log_file="$3"

  if ! kill -0 "$pid" >/dev/null 2>&1; then
    echo "$name exited before the smoke check completed. Last log lines:" >&2
    tail -n 80 "$log_file" >&2 || true
    exit 1
  fi
}

wait_for_http() {
  local url="$1"
  local expected_status="$2"
  local label="$3"
  local api_log="$4"

  for _ in {1..90}; do
    local status
    status="$(curl -sS -o /dev/null -w '%{http_code}' "$url" 2>/dev/null || true)"

    if [[ "$status" == "$expected_status" ]]; then
      ensure_process_running "$api_pid" "API role" "$api_log"
      printf '  %-32s %s\n' "$label" "$url"
      return
    fi

    ensure_process_running "$api_pid" "API role" "$api_log"
    sleep 1
  done

  echo "Timed out waiting for $label at $url." >&2
  tail -n 80 "$api_log" >&2 || true
  exit 1
}

wait_for_db_scalar() {
  local database_name="$1"
  local sql="$2"
  local expected="$3"
  local label="$4"
  local worker_log="$5"

  for _ in {1..90}; do
    local value
    value="$(db_scalar "$database_name" "$sql")"

    if [[ "$value" == "$expected" ]]; then
      printf '  %-32s %s\n' "$label" "$value"
      return
    fi

    ensure_process_running "$worker_pid" "Worker role" "$worker_log"
    sleep 1
  done

  echo "Timed out waiting for $label to equal $expected." >&2
  echo "Last observed value: $(db_scalar "$database_name" "$sql")" >&2
  tail -n 80 "$worker_log" >&2 || true
  exit 1
}

start_api() {
  local connection="$1"
  local log_file="$2"

  (
    cd "$PUBLISH_DIR/api"
    exec env \
      ASPNETCORE_ENVIRONMENT=Testing \
      DOTNET_ENVIRONMENT=Testing \
      ASPNETCORE_URLS="http://127.0.0.1:$API_PORT" \
      MEMORYSYSTEM_POSTGRES_CONNECTION_STRING="$connection" \
      Embeddings__Provider=deterministic \
      Authentication__ApiKey__Keys__pilot_smoke__Key="$API_KEY" \
      Authentication__ApiKey__Keys__pilot_smoke__PrincipalId="$PRINCIPAL_ID" \
      Authentication__ApiKey__Keys__pilot_smoke__DisplayName="Production Pilot Smoke Operator" \
      dotnet MemorySystem.Api.dll
  ) >"$log_file" 2>&1 &
  api_pid=$!
}

start_worker() {
  local connection="$1"
  local worker_id="$2"
  local log_file="$3"

  (
    cd "$PUBLISH_DIR/worker"
    exec env \
      DOTNET_ENVIRONMENT=Testing \
      MEMORYSYSTEM_POSTGRES_CONNECTION_STRING="$connection" \
      Embeddings__Provider=deterministic \
      OutboxWorker__WorkerId="$worker_id" \
      OutboxWorker__IdleDelay=00:00:01 \
      OutboxWorker__LeaseDuration=00:01:00 \
      OutboxWorker__HandlerTimeout=00:00:30 \
      EphemeralEventRetention__Enabled=false \
      dotnet MemorySystem.Worker.dll
  ) >"$log_file" 2>&1 &
  worker_pid=$!
}

verify_api_paths() {
  local base_url="$1"
  local label="$2"
  local output_dir="$3"

  local summary_file="$output_dir/operations-summary.json"
  local search_file="$output_dir/memory-search.json"
  local event_file="$output_dir/event-write.json"
  local event_read_file="$output_dir/event-read.json"

  curl -fsS -H "X-Api-Key: $API_KEY" \
    "$base_url/api/operations/summary" \
    -o "$summary_file"
  grep -q '"observed":true' "$summary_file"
  grep -q '"stale":false' "$summary_file"
  printf '  %-32s operations summary\n' "$label"

  MEMORYSYSTEM_API_BASE_URL="$base_url" \
    MEMORYSYSTEM_API_KEY="$API_KEY" \
    "$ROOT_DIR/scripts/operations-metrics-smoke.sh"
  printf '  %-32s alert inputs\n' "$label"

  curl -fsS -G -H "X-Api-Key: $API_KEY" \
    --data-urlencode "q=SQL-first" \
    --data-urlencode "limit=5" \
    "$base_url/api/memory/search" \
    -o "$search_file"
  grep -q 'SQL-first' "$search_file"
  printf '  %-32s authenticated memory read\n' "$label"

  curl -fsS -X POST \
    -H "X-Api-Key: $API_KEY" \
    -H "Idempotency-Key: production-pilot-smoke-$RUN_ID-$label" \
    -H "Content-Type: application/json" \
    --data "{\"eventType\":\"user_message\",\"principalId\":\"$PRINCIPAL_ID\",\"scopeType\":\"user\",\"scopeId\":\"$PRINCIPAL_ID\",\"payload\":{\"message\":\"Production pilot deployment smoke write path for $label.\"}}" \
    "$base_url/api/events" \
    -o "$event_file"

  local event_id
  event_id="$(sed -n 's/.*"id":"\([^"]*\)".*/\1/p' "$event_file")"

  if [[ -z "$event_id" ]]; then
    echo "Could not parse event id from write response:" >&2
    cat "$event_file" >&2
    exit 1
  fi

  curl -fsS -H "X-Api-Key: $API_KEY" \
    "$base_url/api/events/$event_id" \
    -o "$event_read_file"
  grep -q 'Production pilot deployment smoke write path' "$event_read_file"
  printf '  %-32s authenticated write/read\n' "$label"
}

verify_database_counts_match() {
  local source_db="$1"
  local restored_db="$2"

  load_restore_validation_tables

  for table_name in "${RESTORE_VALIDATION_TABLES[@]}"; do
    local source_count
    local restored_count
    source_count="$(db_scalar "$source_db" "SELECT count(*) FROM $table_name;")"
    restored_count="$(db_scalar "$restored_db" "SELECT count(*) FROM $table_name;")"

    if [[ "$source_count" != "$restored_count" ]]; then
      echo "Count mismatch for $table_name: source=$source_count restored=$restored_count" >&2
      exit 1
    fi

    printf '  %-32s %s\n' "$table_name" "$restored_count"
  done
}

SMOKE_CONNECTION_STRING="$(connection_string "$SMOKE_DB")"
RESTORE_CONNECTION_STRING="$(connection_string "$RESTORE_DB")"
API_BASE_URL="http://127.0.0.1:$API_PORT"

log "Starting PostgreSQL"
docker compose up -d --wait postgres

log "Publishing deployment roles"
mkdir -p "$PUBLISH_DIR"
dotnet publish src/MemorySystem.Migrator/MemorySystem.Migrator.csproj --configuration Release --output "$PUBLISH_DIR/migrator" >/dev/null
dotnet publish src/MemorySystem.Api/MemorySystem.Api.csproj --configuration Release --output "$PUBLISH_DIR/api" >/dev/null
dotnet publish src/MemorySystem.Worker/MemorySystem.Worker.csproj --configuration Release --output "$PUBLISH_DIR/worker" >/dev/null
dotnet publish src/MemorySystem.DemoSeeder/MemorySystem.DemoSeeder.csproj --configuration Release --output "$PUBLISH_DIR/seeder" >/dev/null
printf '  %-32s %s\n' "Artifact directory" "$PUBLISH_DIR"

log "Creating production-like pilot database"
create_database "$SMOKE_DB"
created_smoke_db=true
printf '  %-32s %s\n' "Pilot database" "$SMOKE_DB"

log "Running migrator role"
dotnet "$PUBLISH_DIR/migrator/MemorySystem.Migrator.dll" \
  --connection-string "$SMOKE_CONNECTION_STRING" \
  --migrations-directory migrations
printf '  %-32s %s\n' "Migration rows" "$(db_scalar "$SMOKE_DB" "SELECT count(*) FROM schema_migrations;")"

log "Seeding Scenario 0001 without embeddings"
dotnet "$PUBLISH_DIR/seeder/MemorySystem.DemoSeeder.dll" \
  --connection-string "$SMOKE_CONNECTION_STRING" \
  --migrations-directory migrations \
  --skip-migrations \
  --skip-embeddings
printf '  %-32s %s\n' "Pending outbox jobs" "$(db_scalar "$SMOKE_DB" "SELECT count(*) FROM outbox_jobs WHERE status = 'pending';")"

log "Starting API role against pilot database"
api_log="$WORK_DIR/api-smoke.log"
start_api "$SMOKE_CONNECTION_STRING" "$api_log"
wait_for_http "$API_BASE_URL/health/live" "200" "API liveness" "$api_log"

log "Starting worker role against pilot database"
worker_log="$WORK_DIR/worker-smoke.log"
start_worker "$SMOKE_CONNECTION_STRING" "production-pilot-smoke-worker" "$worker_log"
wait_for_db_scalar "$SMOKE_DB" "SELECT count(*) FROM outbox_jobs WHERE status <> 'completed';" "0" "Outbox unfinished jobs" "$worker_log"
wait_for_db_scalar "$SMOKE_DB" "SELECT count(*) FROM memory_embeddings;" "6" "Memory embeddings" "$worker_log"
wait_for_http "$API_BASE_URL/health/ready" "200" "API readiness" "$api_log"
verify_api_paths "$API_BASE_URL" "Pilot database" "$WORK_DIR"

log "Stopping roles for rollback rehearsal"
stop_process "$worker_pid" "Worker role"
worker_pid=""
stop_process "$api_pid" "API role"
api_pid=""

log "Creating restore validation backup"
docker compose exec -T postgres sh -lc \
  'pg_dump -U "$POSTGRES_USER" -d "$1" --format=custom --blobs --no-owner --no-privileges' \
  sh "$SMOKE_DB" >"$BACKUP_FILE"
docker compose exec -T postgres sh -lc 'pg_restore --list >/dev/null' <"$BACKUP_FILE"
printf '  %-32s %s\n' "Backup file" "$BACKUP_FILE"

log "Restoring into fresh rollback database"
create_database "$RESTORE_DB"
created_restore_db=true
docker compose exec -T postgres sh -lc \
  'pg_restore -U "$POSTGRES_USER" -d "$1" --clean --if-exists --no-owner --no-privileges' \
  sh "$RESTORE_DB" <"$BACKUP_FILE"
dotnet "$PUBLISH_DIR/migrator/MemorySystem.Migrator.dll" \
  --connection-string "$RESTORE_CONNECTION_STRING" \
  --migrations-directory migrations
verify_database_counts_match "$SMOKE_DB" "$RESTORE_DB"

if [[ "$(db_scalar "$RESTORE_DB" "SELECT count(*) FROM pg_extension WHERE extname = 'vector';")" != "1" ]]; then
  echo "Restored database is missing pgvector extension." >&2
  exit 1
fi
printf '  %-32s verified\n' "pgvector extension"

log "Restarting roles against restored database"
restore_api_log="$WORK_DIR/api-restore.log"
restore_worker_log="$WORK_DIR/worker-restore.log"
start_api "$RESTORE_CONNECTION_STRING" "$restore_api_log"
wait_for_http "$API_BASE_URL/health/live" "200" "Restored API liveness" "$restore_api_log"
start_worker "$RESTORE_CONNECTION_STRING" "production-pilot-restore-worker" "$restore_worker_log"
wait_for_db_scalar "$RESTORE_DB" "SELECT status FROM worker_heartbeats WHERE worker_id = 'production-pilot-restore-worker' LIMIT 1;" "running" "Restored worker heartbeat" "$restore_worker_log"
wait_for_http "$API_BASE_URL/health/ready" "200" "Restored API readiness" "$restore_api_log"
verify_api_paths "$API_BASE_URL" "Restored database" "$WORK_DIR"

log "Production pilot deployment smoke passed"
printf '  %-32s %s\n' "Migrator role" "verified"
printf '  %-32s %s\n' "API role" "verified"
printf '  %-32s %s\n' "Worker role" "verified"
printf '  %-32s %s\n' "Rollback/restore" "verified"
printf '  %-32s %s\n' "Scenario" "0001"
printf '  %-32s %s\n' "Project" "$PROJECT_ID"
