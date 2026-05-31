#!/usr/bin/env bash

RESTORE_VALIDATION_TABLES=()

load_restore_validation_tables() {
  local manifest_path="${1:-${MEMORYSYSTEM_RESTORE_VALIDATION_TABLES_FILE:-$ROOT_DIR/scripts/restore-validation-tables.txt}}"
  local table_name

  if [[ ! -f "$manifest_path" ]]; then
    echo "Restore validation table manifest not found: $manifest_path" >&2
    exit 1
  fi

  RESTORE_VALIDATION_TABLES=()

  while IFS= read -r table_name || [[ -n "$table_name" ]]; do
    table_name="${table_name%%#*}"
    table_name="${table_name#"${table_name%%[![:space:]]*}"}"
    table_name="${table_name%"${table_name##*[![:space:]]}"}"

    if [[ -z "$table_name" ]]; then
      continue
    fi

    if [[ ! "$table_name" =~ ^[a-z_][a-z0-9_]*$ ]]; then
      echo "Invalid table name in restore validation manifest: $table_name" >&2
      exit 1
    fi

    RESTORE_VALIDATION_TABLES+=("$table_name")
  done < "$manifest_path"

  if [[ "${#RESTORE_VALIDATION_TABLES[@]}" -eq 0 ]]; then
    echo "Restore validation table manifest is empty: $manifest_path" >&2
    exit 1
  fi
}
