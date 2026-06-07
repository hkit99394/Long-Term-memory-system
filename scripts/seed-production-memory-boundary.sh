#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENV_FILE="${MEMORYSYSTEM_PRODUCTION_ENV_FILE:-$ROOT_DIR/.env.production}"
COMPOSE_FILE="${MEMORYSYSTEM_PRODUCTION_COMPOSE_FILE:-$ROOT_DIR/docker-compose.production.yml}"

source "$ROOT_DIR/scripts/lib/production-env.sh"

ORG_ID="${MEMORYSYSTEM_CANONICAL_ORG_ID:-9f8e7d6c-5b4a-4321-9123-abcdef123001}"
PROJECT_ID="${MEMORYSYSTEM_CANONICAL_PROJECT_ID:-9f8e7d6c-5b4a-4321-9123-abcdef123002}"
ORG_NAME="${MEMORYSYSTEM_CANONICAL_ORG_NAME:-Personal AI Systems}"
PROJECT_NAME="${MEMORYSYSTEM_CANONICAL_PROJECT_NAME:-Long-Term Memory System}"

operator_principal_id="$(read_env_value MEMORYSYSTEM_OPERATOR_PRINCIPAL_ID "")"
operator_display_name="$(read_env_value MEMORYSYSTEM_OPERATOR_DISPLAY_NAME "Production Operator")"
db_name="$(read_env_value MEMORYSYSTEM_POSTGRES_DB memory_system)"
db_user="$(read_env_value MEMORYSYSTEM_POSTGRES_USER memory_system)"
psql_command=()

if ! [[ "$operator_principal_id" =~ ^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$ ]]; then
  printf 'MEMORYSYSTEM_OPERATOR_PRINCIPAL_ID must be a GUID in %s.\n' "$ENV_FILE" >&2
  exit 64
fi

if is_external_postgres_profile; then
  validate_external_postgres_connection_string
  command -v psql >/dev/null 2>&1 || {
    printf 'psql is required on the operator host for MEMORYSYSTEM_POSTGRES_PROFILE=external.\n' >&2
    exit 64
  }
  psql_command=(psql "$(read_env_value MEMORYSYSTEM_POSTGRES_CONNECTION_STRING "")")
elif [[ "$(postgres_profile)" == "invalid" ]]; then
  printf 'MEMORYSYSTEM_POSTGRES_PROFILE must be local or external.\n' >&2
  exit 64
else
  psql_command=(compose exec -T postgres psql -U "$db_user" -d "$db_name")
fi

"${psql_command[@]}" \
  -v ON_ERROR_STOP=1 \
  -v operator_principal_id="$operator_principal_id" \
  -v operator_display_name="$operator_display_name" \
  -v org_id="$ORG_ID" \
  -v org_name="$ORG_NAME" \
  -v project_id="$PROJECT_ID" \
  -v project_name="$PROJECT_NAME" <<'SQL'
BEGIN;

INSERT INTO principals (
    id,
    principal_type,
    display_name,
    status
)
VALUES (
    :'operator_principal_id'::uuid,
    'human',
    :'operator_display_name',
    'active'
)
ON CONFLICT (id)
DO UPDATE SET
    principal_type = EXCLUDED.principal_type,
    display_name = EXCLUDED.display_name,
    status = EXCLUDED.status,
    updated_at = now();

INSERT INTO organizations (
    id,
    name
)
VALUES (
    :'org_id'::uuid,
    :'org_name'
)
ON CONFLICT (id)
DO UPDATE SET
    name = EXCLUDED.name,
    updated_at = now();

INSERT INTO projects (
    id,
    org_id,
    name,
    status
)
VALUES (
    :'project_id'::uuid,
    :'org_id'::uuid,
    :'project_name',
    'active'
)
ON CONFLICT (id)
DO UPDATE SET
    org_id = EXCLUDED.org_id,
    name = EXCLUDED.name,
    status = EXCLUDED.status,
    updated_at = now();

INSERT INTO organization_memberships (
    org_id,
    principal_id,
    access_level
)
VALUES (
    :'org_id'::uuid,
    :'operator_principal_id'::uuid,
    'owner'
)
ON CONFLICT (org_id, principal_id)
DO UPDATE SET
    access_level = EXCLUDED.access_level;

INSERT INTO project_memberships (
    project_id,
    principal_id,
    access_level
)
VALUES (
    :'project_id'::uuid,
    :'operator_principal_id'::uuid,
    'admin'
)
ON CONFLICT (project_id, principal_id)
DO UPDATE SET
    access_level = EXCLUDED.access_level;

WITH supported_roles(role_id) AS (
    VALUES
        ('product_owner'),
        ('cto'),
        ('security_professional'),
        ('it_manager'),
        ('developer'),
        ('tester_qa'),
        ('release_manager'),
        ('knowledge_steward'),
        ('designer'),
        ('cfo'),
        ('coo'),
        ('ceo')
)
INSERT INTO role_assignments (
    id,
    principal_id,
    role_id,
    scope_type,
    scope_id
)
SELECT
    (
        substr(md5(:'operator_principal_id' || ':' || :'project_id' || ':' || role_id || ':role-assignment'), 1, 8) || '-' ||
        substr(md5(:'operator_principal_id' || ':' || :'project_id' || ':' || role_id || ':role-assignment'), 9, 4) || '-' ||
        substr(md5(:'operator_principal_id' || ':' || :'project_id' || ':' || role_id || ':role-assignment'), 13, 4) || '-' ||
        substr(md5(:'operator_principal_id' || ':' || :'project_id' || ':' || role_id || ':role-assignment'), 17, 4) || '-' ||
        substr(md5(:'operator_principal_id' || ':' || :'project_id' || ':' || role_id || ':role-assignment'), 21, 12)
    )::uuid,
    :'operator_principal_id'::uuid,
    role_id,
    'project',
    :'project_id'::uuid
FROM supported_roles
ON CONFLICT (id)
DO UPDATE SET
    principal_id = EXCLUDED.principal_id,
    role_id = EXCLUDED.role_id,
    scope_type = EXCLUDED.scope_type,
    scope_id = EXCLUDED.scope_id;

WITH namespace_roots(namespace_prefix) AS (
    VALUES
        ('/project/' || :'project_id' || '/goals'),
        ('/project/' || :'project_id' || '/facts'),
        ('/project/' || :'project_id' || '/decisions'),
        ('/project/' || :'project_id' || '/rationale'),
        ('/project/' || :'project_id' || '/risks'),
        ('/project/' || :'project_id' || '/release-evidence'),
        ('/project/' || :'project_id' || '/role/product_owner/lens'),
        ('/project/' || :'project_id' || '/role/cto/lens'),
        ('/project/' || :'project_id' || '/role/security_professional/lens'),
        ('/project/' || :'project_id' || '/role/it_manager/lens'),
        ('/project/' || :'project_id' || '/role/developer/lens'),
        ('/project/' || :'project_id' || '/role/tester_qa/lens'),
        ('/project/' || :'project_id' || '/role/release_manager/lens'),
        ('/project/' || :'project_id' || '/role/knowledge_steward/lens'),
        ('/project/' || :'project_id' || '/role/designer/lens'),
        ('/project/' || :'project_id' || '/role/cfo/lens'),
        ('/project/' || :'project_id' || '/role/coo/lens'),
        ('/project/' || :'project_id' || '/role/ceo/lens')
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
        substr(md5(:'operator_principal_id' || ':' || namespace_prefix || ':admin'), 1, 8) || '-' ||
        substr(md5(:'operator_principal_id' || ':' || namespace_prefix || ':admin'), 9, 4) || '-' ||
        substr(md5(:'operator_principal_id' || ':' || namespace_prefix || ':admin'), 13, 4) || '-' ||
        substr(md5(:'operator_principal_id' || ':' || namespace_prefix || ':admin'), 17, 4) || '-' ||
        substr(md5(:'operator_principal_id' || ':' || namespace_prefix || ':admin'), 21, 12)
    )::uuid,
    :'operator_principal_id'::uuid,
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

COMMIT;

SELECT 'canonical_org_id' AS key, :'org_id' AS value
UNION ALL
SELECT 'canonical_project_id', :'project_id'
UNION ALL
SELECT 'operator_project_membership', access_level
FROM project_memberships
WHERE project_id = :'project_id'::uuid
  AND principal_id = :'operator_principal_id'::uuid
UNION ALL
SELECT 'explicit_project_namespace_admin_grants', count(*)::text
FROM memory_access_grants
WHERE principal_id = :'operator_principal_id'::uuid
  AND namespace_prefix LIKE '/project/' || :'project_id' || '/%';
SQL

printf 'Seeded canonical production memory boundary for project %s.\n' "$PROJECT_ID"
