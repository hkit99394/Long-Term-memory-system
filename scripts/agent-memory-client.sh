#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENV_FILE="${MEMORYSYSTEM_AGENT_MEMORY_ENV_FILE:-$ROOT_DIR/.env.production}"

python3 - "$ROOT_DIR" "$ENV_FILE" "$@" <<'PY'
import argparse
import datetime as dt
import hashlib
import json
import os
import sys
import tempfile
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

repo_root = Path(sys.argv[1])
env_file = Path(sys.argv[2])
argv = sys.argv[3:]

default_project_id = "9f8e7d6c-5b4a-4321-9123-abcdef123002"
feedback_types = {
    "useful",
    "stale",
    "wrong",
    "sensitive",
    "over_broad",
    "over-broad",
    "missing",
    "noisy",
}
item_feedback_types = {
    "useful",
    "stale",
    "wrong",
    "sensitive",
    "over_broad",
    "noisy",
}
context_groups = [
    "items",
    "userPreferences",
    "projectMemory",
    "roleMemory",
    "relevantDecisions",
]


class ClientError(RuntimeError):
    pass


def read_env_value(key, fallback=""):
    if os.environ.get(key):
        return os.environ[key]

    if env_file.exists():
        for line in env_file.read_text(encoding="utf-8").splitlines():
            if not line or line.lstrip().startswith("#") or "=" not in line:
                continue
            name, value = line.split("=", 1)
            if name == key:
                value = value.strip()
                if len(value) >= 2 and value[0] == value[-1] and value[0] in {"'", '"'}:
                    value = value[1:-1]
                return value

    return fallback


def default_state_file():
    digest = hashlib.sha256(str(repo_root).encode("utf-8")).hexdigest()[:16]
    return Path(tempfile.gettempdir()) / f"memorysystem-agent-memory-client-{digest}.json"


def effective_state_file():
    return Path(os.environ.get("MEMORYSYSTEM_AGENT_MEMORY_STATE_FILE") or default_state_file())


def effective_base_url():
    configured = os.environ.get("MEMORYSYSTEM_API_BASE_URL")
    if configured:
        return configured.rstrip("/")

    port = read_env_value("MEMORYSYSTEM_LOCAL_ACCESS_PORT", "8081")
    return f"http://127.0.0.1:{port}".rstrip("/")


def effective_api_key():
    return os.environ.get("MEMORYSYSTEM_API_KEY") or read_env_value("MEMORYSYSTEM_OPERATOR_API_KEY")


def json_print(value):
    print(json.dumps(value, indent=2, sort_keys=True))


def require_api_key():
    api_key = effective_api_key()
    if not api_key:
        raise ClientError(
            "MEMORYSYSTEM_API_KEY or MEMORYSYSTEM_OPERATOR_API_KEY must be configured. "
            "The key is read from the environment or the configured env file and is never printed."
        )
    return api_key


def request_json(method, path, body=None):
    api_key = require_api_key()
    data = None
    headers = {"X-Api-Key": api_key}

    if body is not None:
        data = json.dumps(body, separators=(",", ":"), sort_keys=True).encode("utf-8")
        headers["Content-Type"] = "application/json"

    request = urllib.request.Request(
        effective_base_url() + path,
        data=data,
        headers=headers,
        method=method,
    )

    try:
        with urllib.request.urlopen(request, timeout=20) as response:
            payload = response.read().decode("utf-8")
            return json.loads(payload) if payload else {}
    except urllib.error.HTTPError as exc:
        payload = exc.read().decode("utf-8", errors="replace")
        raise ClientError(f"{method} {path} failed with HTTP {exc.code}: {payload}") from exc
    except urllib.error.URLError as exc:
        raise ClientError(f"{method} {path} failed: {exc.reason}") from exc


def load_state(path):
    if not path.exists():
        return None

    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        raise ClientError(f"Pending feedback state file is not valid JSON: {path}") from exc


def write_state(path, state):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(state, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def clear_state(path):
    try:
        path.unlink()
    except FileNotFoundError:
        pass


def normalize_feedback_type(value):
    normalized = value.strip().lower()
    if normalized == "over-broad":
        return "over_broad"
    return normalized


def extract_context_items(packet):
    items = []
    seen = set()

    for group in context_groups:
        group_items = packet.get(group)
        if not isinstance(group_items, list):
            continue

        for item in group_items:
            if not isinstance(item, dict):
                continue
            item_id = item.get("itemId")
            source_type = item.get("sourceType")
            source_id = item.get("sourceId")
            identity = (item_id, source_type, source_id)
            if identity in seen:
                continue
            seen.add(identity)
            items.append(
                {
                    "itemId": item_id,
                    "sourceType": source_type,
                    "sourceId": source_id,
                    "memoryType": item.get("memoryType") or item.get("memoryKind"),
                    "namespace": item.get("namespace"),
                    "group": group,
                }
            )

    return items


def extract_fact_count(response):
    for field in ("facts", "results", "items"):
        value = response.get(field)
        if isinstance(value, list):
            return len(value)
    return 0


def resolve_item_ref(state, args):
    if args.feedback_type == "missing":
        forbidden = [args.item_id, args.source_type, args.source_id, args.item_index]
        if any(value is not None for value in forbidden):
            raise ClientError("missing feedback is packet-level; omit item/source identifiers.")
        return {}

    item_id = args.item_id
    source_type = args.source_type
    source_id = args.source_id

    if args.item_index is not None:
        refs = state.get("contextItemRefs") or []
        index = args.item_index - 1
        if index < 0 or index >= len(refs):
            raise ClientError(f"--item-index must be between 1 and {len(refs)}.")
        item = refs[index]
        item_id = item.get("itemId")
        source_type = item.get("sourceType")
        source_id = item.get("sourceId")

    if not item_id or not source_type or not source_id:
        raise ClientError(
            f"{args.feedback_type} feedback is item-level and requires "
            "--item-id, --source-type, and --source-id, or --item-index."
        )

    return {
        "itemId": item_id,
        "sourceType": source_type,
        "sourceId": source_id,
    }


def command_prework(args):
    state_path = effective_state_file()
    existing = load_state(state_path)
    if existing and existing.get("feedbackRequired"):
        if not args.replace_pending:
            raise ClientError(
                "Pending context feedback exists. Run `agent-memory-client.sh feedback ...` "
                f"for packet {existing.get('packetId')} before starting new prework, "
                "or pass --replace-pending to intentionally overwrite it."
            )

    scope_type = args.scope_type
    scope_id = args.scope_id
    role_id = args.role_id or None

    context_params = {
        "q": args.query,
        "scopeType": scope_type,
        "scopeId": scope_id,
        "limit": str(args.limit),
    }
    if role_id:
        context_params["roleId"] = role_id

    context_path = "/api/memory/context?" + urllib.parse.urlencode(context_params)
    context_packet = request_json("GET", context_path)

    fact_body = {
        "query": args.query,
        "targetScope": {
            "scopeType": scope_type,
            "scopeId": scope_id,
        },
        "includeContradictions": args.include_contradictions,
        "includeExcluded": args.include_excluded,
        "limit": args.limit,
    }
    if role_id:
        fact_body["roleId"] = role_id
    if args.namespace:
        fact_body["namespaces"] = args.namespace
    if args.memory_type:
        fact_body["memoryTypes"] = args.memory_type

    facts = request_json("POST", "/api/memory/query-facts", fact_body)
    packet_id = context_packet.get("packetId")
    if not packet_id:
        raise ClientError("memory.getContext did not return packetId; feedback cannot be enforced.")

    item_refs = extract_context_items(context_packet)
    state = {
        "version": 1,
        "feedbackRequired": True,
        "startedAt": dt.datetime.now(dt.timezone.utc).isoformat().replace("+00:00", "Z"),
        "packetId": packet_id,
        "targetScopeType": scope_type,
        "targetScopeId": scope_id,
        "roleId": role_id,
        "contextItemCount": len(item_refs),
        "queryFactsCount": extract_fact_count(facts),
        "contextItemRefs": item_refs,
    }
    write_state(state_path, state)

    json_print(
        {
            "status": "prework_complete",
            "packetId": packet_id,
            "stateFile": str(state_path),
            "targetScopeType": scope_type,
            "targetScopeId": scope_id,
            "roleId": role_id,
            "contextItemCount": state["contextItemCount"],
            "queryFactsCount": state["queryFactsCount"],
            "feedbackRequired": True,
            "contextItemRefs": item_refs[: args.show_items],
            "next": "Run `scripts/agent-memory-client.sh feedback --feedback-type missing` or item-level feedback when the task is complete.",
        }
    )


def command_feedback(args):
    args.feedback_type = normalize_feedback_type(args.feedback_type)
    state_path = effective_state_file()
    state = load_state(state_path)
    if not state or not state.get("feedbackRequired"):
        raise ClientError("No pending prework packet found. Run `agent-memory-client.sh prework ...` first.")

    body = {
        "packetId": state["packetId"],
        "targetScopeType": state["targetScopeType"],
        "targetScopeId": state["targetScopeId"],
        "feedbackType": args.feedback_type,
    }
    if state.get("roleId"):
        body["roleId"] = state["roleId"]

    body.update(resolve_item_ref(state, args))
    response = request_json("POST", "/api/memory/context/feedback", body)
    clear_state(state_path)

    json_print(
        {
            "status": "feedback_recorded",
            "packetId": state["packetId"],
            "feedbackType": args.feedback_type,
            "feedbackId": response.get("id"),
            "stateCleared": True,
        }
    )


def command_status(_args):
    state_path = effective_state_file()
    state = load_state(state_path)
    if not state or not state.get("feedbackRequired"):
        json_print(
            {
                "status": "no_pending_feedback",
                "stateFile": str(state_path),
            }
        )
        return

    json_print(
        {
            "status": "pending_feedback",
            "stateFile": str(state_path),
            "packetId": state.get("packetId"),
            "targetScopeType": state.get("targetScopeType"),
            "targetScopeId": state.get("targetScopeId"),
            "roleId": state.get("roleId"),
            "contextItemCount": state.get("contextItemCount"),
            "queryFactsCount": state.get("queryFactsCount"),
            "contextItemRefs": (state.get("contextItemRefs") or [])[:10],
        }
    )


def build_parser():
    parser = argparse.ArgumentParser(
        prog="agent-memory-client.sh",
        description="Payload-safe LMSS v1 wrapper that enforces getContext, queryFacts, and feedback.",
    )
    subparsers = parser.add_subparsers(dest="command", required=True)

    prework = subparsers.add_parser(
        "prework",
        help="Run memory.getContext and memory.queryFacts before project work.",
    )
    prework.add_argument("--query", required=True, help="Task query for context and fact retrieval.")
    prework.add_argument("--scope-type", default="project", help="Target scope type.")
    prework.add_argument(
        "--scope-id",
        default=os.environ.get("MEMORYSYSTEM_CANONICAL_PROJECT_ID", default_project_id),
        help="Target scope id.",
    )
    prework.add_argument(
        "--role-id",
        default=os.environ.get("MEMORYSYSTEM_AGENT_MEMORY_ROLE_ID", ""),
        help="Optional role perspective.",
    )
    prework.add_argument("--limit", type=int, default=8, help="Context and fact result limit.")
    prework.add_argument("--namespace", action="append", help="Optional memory.queryFacts namespace filter.")
    prework.add_argument("--memory-type", action="append", help="Optional memory.queryFacts memory type filter.")
    prework.add_argument("--no-include-contradictions", dest="include_contradictions", action="store_false")
    prework.add_argument("--no-include-excluded", dest="include_excluded", action="store_false")
    prework.add_argument("--replace-pending", action="store_true", help="Overwrite an existing pending-feedback packet.")
    prework.add_argument("--show-items", type=int, default=10, help="Number of item refs to print.")
    prework.set_defaults(func=command_prework, include_contradictions=True, include_excluded=True)

    feedback = subparsers.add_parser(
        "feedback",
        help="Record feedback for the pending context packet and clear pending state.",
    )
    feedback.add_argument("--feedback-type", required=True, choices=sorted(feedback_types))
    feedback.add_argument("--item-id", help="Context item id for item-level feedback.")
    feedback.add_argument("--source-type", help="Source type for item-level feedback.")
    feedback.add_argument("--source-id", help="Source id for item-level feedback.")
    feedback.add_argument("--item-index", type=int, help="Use a 1-based item ref from the pending state.")
    feedback.set_defaults(func=command_feedback)

    status = subparsers.add_parser("status", help="Show whether feedback is pending.")
    status.set_defaults(func=command_status)

    return parser


try:
    parser = build_parser()
    parsed = parser.parse_args(argv)
    parsed.func(parsed)
except ClientError as exc:
    print(str(exc), file=sys.stderr)
    sys.exit(2)
PY
