#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENV_FILE="${MEMORYSYSTEM_PRODUCTION_ENV_FILE:-$ROOT_DIR/.env.production}"
API_BASE_URL="${MEMORYSYSTEM_API_BASE_URL:-http://127.0.0.1:8081}"
PROJECT_ID="${MEMORYSYSTEM_CANONICAL_PROJECT_ID:-9f8e7d6c-5b4a-4321-9123-abcdef123002}"
SEED_VERSION="${MEMORYSYSTEM_ROLE_LENS_SEED_VERSION:-ip13-role-lens-first-pass-v1}"

show_help() {
  cat <<'TXT'
Usage: role-lens-first-pass.sh [--dry-run]

Seed the IP-13 first content pass for project role lenses.

The live run resolves active source-backed base memory facts through
/api/admin/memory/facts, appends one payload-safe source event, and proposes
canonical role_lens memories for Product Owner, CTO, Security, Ops, Developer,
QA, Release Manager, and Knowledge Steward.

Options:
  --dry-run   Validate source excerpts and print the planned payload-safe role lens bundle without API calls.
  -h, --help  Show this help text.
TXT
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

dry_run_flag="$(read_env_value MEMORYSYSTEM_ROLE_LENS_FIRST_PASS_DRY_RUN false)"

for arg in "$@"; do
  case "$arg" in
    -h|--help)
      show_help
      exit 0
      ;;
    --dry-run)
      dry_run_flag=true
      ;;
  esac
done

api_key="$(read_env_value MEMORYSYSTEM_OPERATOR_API_KEY "")"

if ! is_true "$dry_run_flag" && [[ -z "$api_key" ]]; then
  printf 'MEMORYSYSTEM_OPERATOR_API_KEY must be configured in %s or the environment for a live role-lens seed.\n' "$ENV_FILE" >&2
  exit 64
fi

python3 - "$API_BASE_URL" "$api_key" "$PROJECT_ID" "$SEED_VERSION" "$ROOT_DIR" "$dry_run_flag" "$@" <<'PY'
import argparse
import hashlib
import json
import os
import sys
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path, PurePosixPath

base_url, api_key, project_id, seed_version, root_dir, dry_run_raw = sys.argv[1:7]
argv = sys.argv[7:]
repo_root = Path(root_dir)
proposal_memory_type = os.environ.get("MEMORYSYSTEM_ROLE_LENS_MEMORY_TYPE", "role_lens").strip().lower()

if proposal_memory_type not in {"role_lens", "project_role_lens"}:
    print("MEMORYSYSTEM_ROLE_LENS_MEMORY_TYPE must be role_lens or project_role_lens.", file=sys.stderr)
    sys.exit(64)


def truthy(value):
    return str(value).strip().lower() in {"1", "true", "yes", "y", "on"}


role_lenses = [
    {
        "roleId": "product_owner",
        "displayName": "Product Owner",
        "baseSubject": "product owner responsibility",
        "subject": "Product Owner role lens",
        "object": "Prioritize goals, target users, acceptance criteria, backlog or roadmap priority, and GO/NO-GO rationale; flag work that changes product truth without Markdown or release-evidence backing.",
    },
    {
        "roleId": "cto",
        "displayName": "CTO",
        "baseSubject": "cto responsibility",
        "subject": "CTO role lens",
        "object": "Prioritize architecture decisions, platform tradeoffs, domain boundaries, technical risk acceptance, migration safety, and reversible implementation choices.",
    },
    {
        "roleId": "security_professional",
        "displayName": "Security Professional",
        "baseSubject": "security professional responsibility",
        "subject": "Security Professional role lens",
        "object": "Prioritize access boundaries, secret handling, sensitivity classification, auditability, retention and erasure rules, and fail-closed authorization behavior.",
    },
    {
        "roleId": "it_manager",
        "displayName": "IT Manager / Ops",
        "baseSubject": "it manager ops responsibility",
        "subject": "IT Manager / Ops role lens",
        "object": "Prioritize runtime health, deploy safety, backup and restore evidence, monitoring, incident response, operational rollback readiness, and protected production volumes.",
    },
    {
        "roleId": "developer",
        "displayName": "Developer",
        "baseSubject": "developer responsibility",
        "subject": "Developer role lens",
        "object": "Prioritize API contracts, migrations, code constraints, testable implementation facts, and technical decisions grounded in current source files.",
    },
    {
        "roleId": "tester_qa",
        "displayName": "Tester / QA",
        "baseSubject": "tester qa responsibility",
        "subject": "Tester / QA role lens",
        "object": "Prioritize test gates, benchmark evidence, regression risks, release quality evidence, and clear reproduction or verification commands.",
    },
    {
        "roleId": "release_manager",
        "displayName": "Release Manager",
        "baseSubject": "release manager responsibility",
        "subject": "Release Manager role lens",
        "object": "Prioritize version state, release checklist completion, evidence bundle links, rollback plans, pilot readiness, and explicit GO/NO-GO ownership.",
    },
    {
        "roleId": "knowledge_steward",
        "displayName": "Knowledge Steward",
        "baseSubject": "knowledge steward responsibility",
        "subject": "Knowledge Steward role lens",
        "object": "Prioritize canonical memory types, source evidence quality, stale/wrong/duplicate review routing, namespace policy, and memory-vs-Markdown hygiene.",
    },
]

source_documents = [
    {
        "path": "docs/project-memory-boundary.md",
        "excerpts": [
            "The default role templates are `product_owner`, `cto`,",
            "| Product Owner | `product_owner` | Goals, target users, acceptance criteria, backlog, roadmap targets, roadmap priority, and product GO/NO-GO rationale. |",
            "| CTO | `cto` | Architecture decisions, technology tradeoffs, platform direction, and technical risk acceptance. |",
            "| Security Professional | `security_professional` | Access boundaries, secret policy, sensitivity classification, audit policy, and retention/erasure requirements. |",
            "| IT Manager / Ops | `it_manager` | Runtime health, deploys, backups, restore validation, monitoring, and incident response. |",
            "| Developer | `developer` | Implementation facts, API contracts, migrations, code constraints, and technical decisions. |",
            "| Tester / QA | `tester_qa` | Test gates, benchmark evidence, regression risks, and release quality evidence. |",
            "| Release Manager | `release_manager` | Version state, release checklist, evidence bundle, rollback plan, and pilot readiness coordination. |",
            "| Knowledge Steward | `knowledge_steward` | Memory taxonomy, source evidence quality, stale/wrong/duplicate review routing, and memory hygiene. |",
            "Role-specific memory is an access boundary, not just metadata.",
        ],
    },
    {
        "path": "docs/memory-vs-markdown-policy.md",
        "excerpts": [
            "| Role-specific interpretation | Role-lens memory after source-backed proposal and review where needed | Perspective, attention, and risk framing for an authorized role. |",
            "Follow-on hygiene automation should detect source hash drift, stale source",
        ],
    },
    {
        "path": "docs/project-memory-runbook.md",
        "excerpts": [
            "The response `roleId` must match the requested role lens.",
            "Role-lens memory must only appear for authorized matching role requests.",
        ],
    },
]


def clean_relative_path(value):
    if not isinstance(value, str) or not value.strip():
        raise RuntimeError("Source path must be a non-empty string.")
    if value.startswith("/") or "\\" in value:
        raise RuntimeError(f"Source path must be repository-relative: {value}")
    path = PurePosixPath(value)
    if any(part in {"", ".", ".."} for part in path.parts) or str(path) != value:
        raise RuntimeError(f"Source path must be clean: {value}")
    return value


def read_source(document):
    source_path = clean_relative_path(document["path"])
    full_path = repo_root / source_path

    try:
        source_bytes = full_path.read_bytes()
    except FileNotFoundError as exc:
        raise RuntimeError(f"Source document does not exist: {source_path}") from exc

    try:
        source_text = source_bytes.decode("utf-8")
    except UnicodeDecodeError as exc:
        raise RuntimeError(f"Source document is not UTF-8 text: {source_path}") from exc

    missing_excerpts = [
        excerpt
        for excerpt in document["excerpts"]
        if excerpt not in source_text
    ]
    if missing_excerpts:
        raise RuntimeError(
            f"Curated excerpts are stale for {source_path}: "
            + "; ".join(missing_excerpts)
        )

    return {
        "path": source_path,
        "sha256": hashlib.sha256(source_bytes).hexdigest(),
        "byteLength": len(source_bytes),
        "lineCount": source_text.count("\n") + (1 if source_text else 0),
        "excerptCount": len(document["excerpts"]),
        "excerpts": document["excerpts"],
    }


def request_json(method, path, body=None, idempotency_key=None):
    data = None
    headers = {"X-Api-Key": api_key}

    if body is not None:
        data = json.dumps(body, separators=(",", ":"), sort_keys=True).encode("utf-8")
        headers["Content-Type"] = "application/json"

    if idempotency_key:
        headers["Idempotency-Key"] = idempotency_key

    request = urllib.request.Request(
        base_url.rstrip("/") + path,
        data=data,
        headers=headers,
        method=method,
    )

    try:
        with urllib.request.urlopen(request, timeout=20) as response:
            payload = response.read().decode("utf-8")
            return response.status, json.loads(payload) if payload else {}
    except urllib.error.HTTPError as exc:
        payload = exc.read().decode("utf-8", errors="replace")
        raise RuntimeError(f"{method} {path} failed with HTTP {exc.code}: {payload}") from exc


def role_namespace(role_id):
    return f"/project/{project_id}/role/{role_id}/lens"


def build_planned_lens(role):
    return {
        "roleId": role["roleId"],
        "displayName": role["displayName"],
        "memoryType": proposal_memory_type,
        "namespace": role_namespace(role["roleId"]),
        "baseSubject": role["baseSubject"],
        "basePredicate": "owns",
        "requiresBaseMemoryFact": True,
        "subject": role["subject"],
        "predicate": "prioritizes",
        "confidence": 0.95,
    }


def find_base_fact(role, limit):
    query = {
        "scopeType": "project",
        "scopeId": project_id,
        "status": "active",
        "memoryType": "fact",
        "namespacePrefix": f"/project/{project_id}/facts",
        "q": role["baseSubject"],
        "limit": str(limit),
    }
    _, payload = request_json(
        "GET",
        "/api/admin/memory/facts?" + urllib.parse.urlencode(query),
    )
    facts = payload.get("facts") or []
    matches = [
        fact for fact in facts
        if fact.get("memoryType") == "fact"
        and fact.get("status") == "active"
        and fact.get("namespace") == f"/project/{project_id}/facts"
        and fact.get("subject") == role["baseSubject"]
        and fact.get("predicate") == "owns"
    ]

    if len(matches) != 1:
        raise RuntimeError(
            f"Expected exactly one active source-backed base fact for {role['roleId']} "
            f"subject {role['baseSubject']!r}, found {len(matches)}. "
            "Run scripts/seed-production-knowledge-base.sh first or resolve duplicates."
        )

    return matches[0]


def append_source_event(sources):
    body = {
        "eventType": "user_message",
        "scopeType": "project",
        "scopeId": project_id,
        "trustLevel": "user_scoped",
        "retentionClass": "audit",
        "sensitivity": "none",
        "payload": {
            "sourceType": "repository_role_lens_first_pass",
            "seedVersion": seed_version,
            "title": "IP-13 Role Lens First Content Pass",
            "summary": "Source-backed first pass of project role-lens interpretations for the default operating roles.",
            "sourceFiles": [
                {
                    "sourcePath": source["path"],
                    "sourceContentSha256": source["sha256"],
                    "sourceByteLength": source["byteLength"],
                    "sourceLineCount": source["lineCount"],
                    "excerpts": source["excerpts"],
                }
                for source in sources
            ],
            "roleIds": [role["roleId"] for role in role_lenses],
            "lensCount": len(role_lenses),
            "curationPolicy": "Curated excerpts are validated against current repository files; baseMemoryFactId values are resolved live from active source-backed responsibility facts.",
        },
    }

    event_key_material = "|".join(f"{source['path']}:{source['sha256'][:16]}" for source in sources)
    event_key_hash = hashlib.sha256(event_key_material.encode("utf-8")).hexdigest()
    _, payload = request_json(
        "POST",
        "/api/events",
        body,
        idempotency_key=f"{seed_version}:event:{event_key_hash[:24]}",
    )
    return payload["id"]


def propose_role_lens(role, source_event_id, base_fact, memory_type):
    body = {
        "sourceEventId": source_event_id,
        "memoryType": memory_type,
        "scopeType": "project",
        "scopeId": project_id,
        "namespace": role_namespace(role["roleId"]),
        "visibility": "project_shared",
        "subject": role["subject"],
        "predicate": "prioritizes",
        "object": role["object"],
        "confidence": 0.95,
        "trustLevel": "user_scoped",
        "sensitivity": "none",
        "roleId": role["roleId"],
        "baseMemoryFactId": base_fact["id"],
    }
    item_hash = hashlib.sha256(json.dumps(body, separators=(",", ":"), sort_keys=True).encode("utf-8")).hexdigest()

    _, payload = request_json(
        "POST",
        "/api/memory/proposals",
        body,
        idempotency_key=f"{seed_version}:proposal:{memory_type}:{role['roleId']}:{item_hash[:16]}",
    )
    return payload


def dry_run_payload(args, sources):
    return {
        "status": "dry_run",
        "payloadSafe": True,
        "rawSourcePayloadsIncluded": False,
        "targetScopeType": "project",
        "targetScopeId": project_id,
        "seedVersion": seed_version,
        "proposalMemoryType": proposal_memory_type,
        "compatibilityMemoryTypes": ["project_role_lens"],
        "lensCount": len(role_lenses),
        "sourceFiles": [
            {
                "path": source["path"],
                "sha256": source["sha256"],
                "byteLength": source["byteLength"],
                "lineCount": source["lineCount"],
                "excerptCount": source["excerptCount"],
            }
            for source in sources
        ],
        "plannedLenses": [build_planned_lens(role) for role in role_lenses],
        "requiredBaseFacts": [
            {
                "memoryType": "fact",
                "namespace": f"/project/{project_id}/facts",
                "subject": role["baseSubject"],
                "predicate": "owns",
            }
            for role in role_lenses
        ],
        "endpoints": [
            "/api/admin/memory/facts",
            "/api/events",
            "/api/memory/proposals",
        ],
        "commands": {
            "dryRun": "scripts/role-lens-first-pass.sh --dry-run",
            "live": "scripts/role-lens-first-pass.sh",
            "sourceSeed": "scripts/seed-production-knowledge-base.sh",
            "roleContextCheck": "GET /api/memory/context?scopeType=project&roleId={roleId}",
        },
        "baseFactLimit": args.base_fact_limit,
    }


def live_payload(args, sources):
    base_facts = {
        role["roleId"]: find_base_fact(role, args.base_fact_limit)
        for role in role_lenses
    }
    source_event_id = append_source_event(sources)

    rows = []
    active_memory_type = proposal_memory_type
    compatibility_reason = None
    for role in role_lenses:
        base_fact = base_facts[role["roleId"]]
        try:
            result = propose_role_lens(role, source_event_id, base_fact, active_memory_type)
        except RuntimeError as exc:
            if active_memory_type != "role_lens" or "memoryType is required and must be supported" not in str(exc):
                raise

            active_memory_type = "project_role_lens"
            compatibility_reason = "production_api_rejected_canonical_role_lens_memory_type"
            result = propose_role_lens(role, source_event_id, base_fact, active_memory_type)

        rows.append(
            {
                "roleId": role["roleId"],
                "memoryType": active_memory_type,
                "subject": role["subject"],
                "namespace": role_namespace(role["roleId"]),
                "baseMemoryFactId": base_fact["id"],
                "decision": result.get("decision"),
                "memoryId": result.get("memoryId"),
                "reason": result.get("reason"),
            }
        )

    review_count = sum(1 for row in rows if row["decision"] == "review_required")
    stored_count = sum(1 for row in rows if row["decision"] == "stored")
    other_count = len(rows) - review_count - stored_count

    return {
        "status": "completed" if review_count == 0 and other_count == 0 else "needs_review",
        "payloadSafe": True,
        "rawSourcePayloadsIncluded": False,
        "targetScopeType": "project",
        "targetScopeId": project_id,
        "seedVersion": seed_version,
        "requestedMemoryType": proposal_memory_type,
        "usedMemoryType": active_memory_type,
        "compatibilityFallbackReason": compatibility_reason,
        "sourceEventId": source_event_id,
        "lensCount": len(rows),
        "storedCount": stored_count,
        "reviewRequiredCount": review_count,
        "otherCount": other_count,
        "lenses": rows,
    }


def build_parser():
    parser = argparse.ArgumentParser(
        prog="role-lens-first-pass.sh",
        description="Seed the IP-13 source-backed first content pass for project role lenses.",
    )
    parser.add_argument("--dry-run", action="store_true", help="Validate and print the planned payload-safe role lens bundle without API calls.")
    parser.add_argument("--base-fact-limit", type=int, default=20, help="Maximum admin facts returned while resolving each base responsibility fact.")
    return parser


try:
    parser = build_parser()
    parsed = parser.parse_args(argv)
    parsed.dry_run = parsed.dry_run or truthy(dry_run_raw)

    if parsed.base_fact_limit < 1 or parsed.base_fact_limit > 100:
        raise RuntimeError("--base-fact-limit must be between 1 and 100.")

    source_records = [read_source(document) for document in source_documents]
    payload = dry_run_payload(parsed, source_records) if parsed.dry_run else live_payload(parsed, source_records)
    print(json.dumps(payload, indent=2, sort_keys=True))
except RuntimeError as exc:
    print(str(exc), file=sys.stderr)
    sys.exit(1)
PY
