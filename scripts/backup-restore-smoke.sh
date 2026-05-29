#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

POSTGRES_DB="${MEMORYSYSTEM_POSTGRES_DB:-memory_system}"
POSTGRES_USER="${MEMORYSYSTEM_POSTGRES_USER:-memory_system}"
POSTGRES_PASSWORD="${MEMORYSYSTEM_POSTGRES_PASSWORD:-memory_system_dev_password}"
POSTGRES_PORT="${MEMORYSYSTEM_POSTGRES_PORT:-55432}"
BACKUP_DIR="${MEMORYSYSTEM_BACKUP_SMOKE_DIR:-/tmp/memorysystem-backups}"
BACKUP_FILE="${MEMORYSYSTEM_BACKUP_SMOKE_FILE:-$BACKUP_DIR/memory-system-smoke-$(date -u +%Y%m%dT%H%M%SZ).dump}"
RESTORE_DB="${MEMORYSYSTEM_RESTORE_SMOKE_DB:-memory_system_restore_smoke_$(date -u +%Y%m%d%H%M%S)}"
KEEP_BACKUP="${MEMORYSYSTEM_BACKUP_SMOKE_KEEP_BACKUP:-false}"

created_restore_db=false

cleanup() {
  if [[ "$created_restore_db" == "true" ]]; then
    docker compose exec -T postgres sh -lc \
      'dropdb -U "$POSTGRES_USER" --if-exists "$1" >/dev/null' \
      sh "$RESTORE_DB" || true
  fi

  if [[ "$KEEP_BACKUP" != "true" ]]; then
    rm -f "$BACKUP_FILE"
  fi
}

trap cleanup EXIT

count_rows() {
  local database_name="$1"
  local table_name="$2"

  docker compose exec -T postgres sh -lc \
    'psql -U "$POSTGRES_USER" -d "$1" -At -v ON_ERROR_STOP=1 -c "SELECT count(*) FROM $2;"' \
    sh "$database_name" "$table_name"
}

verify_vector_extension() {
  local database_name="$1"

  docker compose exec -T postgres sh -lc \
    'psql -U "$POSTGRES_USER" -d "$1" -At -v ON_ERROR_STOP=1 -c "SELECT count(*) FROM pg_extension WHERE extname = '\''vector'\'';"' \
    sh "$database_name"
}

echo "Starting PostgreSQL..."
docker compose up -d --wait postgres

mkdir -p "$BACKUP_DIR"

echo "Creating logical backup: $BACKUP_FILE"
docker compose exec -T postgres sh -lc \
  'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" --format=custom --blobs --no-owner --no-privileges' \
  > "$BACKUP_FILE"

echo "Inspecting backup archive..."
docker compose exec -T postgres sh -lc 'pg_restore --list >/dev/null' < "$BACKUP_FILE"

echo "Creating restore validation database: $RESTORE_DB"
docker compose exec -T postgres sh -lc \
  'dropdb -U "$POSTGRES_USER" --if-exists "$1" >/dev/null && createdb -U "$POSTGRES_USER" "$1"' \
  sh "$RESTORE_DB"
created_restore_db=true

echo "Restoring backup into validation database..."
docker compose exec -T postgres sh -lc \
  'pg_restore -U "$POSTGRES_USER" -d "$1" --clean --if-exists --no-owner --no-privileges' \
  sh "$RESTORE_DB" < "$BACKUP_FILE"

RESTORE_CONNECTION_STRING="Host=127.0.0.1;Port=$POSTGRES_PORT;Database=$RESTORE_DB;Username=$POSTGRES_USER;Password=$POSTGRES_PASSWORD"

echo "Running migrations against restored database..."
dotnet run --project src/MemorySystem.Migrator --configuration Release -- \
  --connection-string "$RESTORE_CONNECTION_STRING" \
  --migrations-directory migrations >/dev/null

echo "Comparing source and restored table counts..."
tables=(
  schema_migrations
  principals
  organizations
  projects
  events
  memory_facts
  role_memory_lenses
  memory_chunks
  memory_embeddings
  memory_retrieval_feedback
  memory_redactions
  api_idempotency_keys
  outbox_jobs
  memory_reviews
  vault_exports
  worker_heartbeats
)

for table_name in "${tables[@]}"; do
  source_count="$(count_rows "$POSTGRES_DB" "$table_name")"
  restored_count="$(count_rows "$RESTORE_DB" "$table_name")"

  if [[ "$source_count" != "$restored_count" ]]; then
    echo "Count mismatch for $table_name: source=$source_count restored=$restored_count" >&2
    exit 1
  fi

  printf '  %-24s %s\n' "$table_name" "$restored_count"
done

if [[ "$(verify_vector_extension "$RESTORE_DB")" != "1" ]]; then
  echo "Restored database is missing pgvector extension." >&2
  exit 1
fi

echo "pgvector extension verified."
echo "Backup/restore smoke passed."
