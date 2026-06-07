#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SEED_SCRIPT="${MEMORYSYSTEM_KNOWLEDGE_SEED_SCRIPT:-$ROOT_DIR/scripts/seed-production-knowledge-base.sh}"
PROJECT_ID="${MEMORYSYSTEM_CANONICAL_PROJECT_ID:-9f8e7d6c-5b4a-4321-9123-abcdef123002}"

python3 - "$ROOT_DIR" "$SEED_SCRIPT" "$PROJECT_ID" "$@" <<'PY'
import argparse
import ast
import hashlib
import json
import re
import sys
from pathlib import Path

repo_root = Path(sys.argv[1])
seed_script = Path(sys.argv[2])
project_id = sys.argv[3]
argv = sys.argv[4:]

required_documents = ("roadmap", "backlog")
canonical_paths = {
    "roadmap": "docs/roadmap.md",
    "backlog": "docs/backlog.md",
}
policy_path = "docs/memory-vs-markdown-policy.md"


class SyncError(RuntimeError):
    pass


def extract_seed_python(script_path):
    try:
        lines = script_path.read_text(encoding="utf-8").splitlines()
    except FileNotFoundError as exc:
        raise SyncError(f"Seed script does not exist: {script_path}") from exc

    start = None
    for index, line in enumerate(lines):
        if "<<'PY'" in line:
            start = index + 1
            break

    if start is None:
        raise SyncError(f"Seed script does not contain the expected Python heredoc: {script_path}")

    for index in range(start, len(lines)):
        if lines[index] == "PY":
            return "\n".join(lines[start:index])

    raise SyncError(f"Seed script Python heredoc is not terminated: {script_path}")


def extract_documents_expression(seed_python):
    marker = "documents = {"
    start = seed_python.find(marker)
    if start < 0:
        raise SyncError("Seed Python does not define a documents map.")

    end = seed_python.find("\n\ndef read_source", start)
    if end < 0:
        raise SyncError("Seed Python does not define read_source after the documents map.")

    assignment_source = seed_python[start:end]
    module = ast.parse(assignment_source, filename=str(seed_script))
    assignments = [
        statement
        for statement in module.body
        if isinstance(statement, ast.Assign)
        and any(isinstance(target, ast.Name) and target.id == "documents" for target in statement.targets)
    ]
    if len(assignments) != 1:
        raise SyncError("Seed Python must contain exactly one documents assignment in the seed manifest block.")

    expression = ast.Expression(assignments[0].value)
    for node in ast.walk(expression):
        if not isinstance(
            node,
            (
                ast.Expression,
                ast.Dict,
                ast.List,
                ast.Tuple,
                ast.Constant,
                ast.JoinedStr,
                ast.FormattedValue,
                ast.Name,
                ast.Load,
            ),
        ):
            raise SyncError(f"Seed documents manifest uses unsupported Python syntax: {type(node).__name__}")
        if isinstance(node, ast.Name) and node.id != "project_id":
            raise SyncError(f"Seed documents manifest references unsupported name: {node.id}")

    return expression


def load_documents(seed_python):
    expression = extract_documents_expression(seed_python)
    documents = eval(
        compile(expression, filename=str(seed_script), mode="eval"),
        {"__builtins__": {}},
        {"project_id": project_id},
    )
    if not isinstance(documents, dict):
        raise SyncError("Seed documents manifest must be an object.")
    return documents


def sha256_file(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def source_text(path):
    return path.read_text(encoding="utf-8")


def policy_checks():
    text = source_text(repo_root / policy_path)
    fragments = [
        "Markdown is the canonical source for project plans",
        "Roadmap targets, priorities, status, and acceptance criteria",
        "docs/roadmap.md",
        "docs/backlog.md",
        "Memory may keep source-backed",
    ]
    checks = []
    for fragment in fragments:
        checks.append({"fragment": fragment, "present": fragment in text})
    return checks


def document_report(key, document):
    errors = []
    expected_path = canonical_paths[key]
    source_path = document.get("path")
    source_file = repo_root / expected_path
    current_sha256 = sha256_file(source_file)
    pinned_sha256 = document.get("sourceSha256")
    excerpts = document.get("excerpts") if isinstance(document.get("excerpts"), list) else []
    items = document.get("items") if isinstance(document.get("items"), list) else []
    text = source_text(source_file)

    if source_path != expected_path:
        errors.append(f"{key}: expected path {expected_path}, found {source_path!r}.")

    if not isinstance(pinned_sha256, str) or not re.fullmatch(r"[0-9a-f]{64}", pinned_sha256):
        errors.append(f"{key}: sourceSha256 must be a lower-case SHA-256 hex digest.")
    elif pinned_sha256 != current_sha256:
        errors.append(f"{key}: source hash drift for {expected_path}; expected {pinned_sha256}, found {current_sha256}.")

    stale_excerpts = [excerpt for excerpt in excerpts if not isinstance(excerpt, str) or excerpt not in text]
    if not excerpts:
        errors.append(f"{key}: curated excerpts are required.")
    elif stale_excerpts:
        errors.append(f"{key}: {len(stale_excerpts)} curated excerpt(s) no longer match {expected_path}.")

    if not items:
        errors.append(f"{key}: at least one high-value memory item is required.")

    item_errors = []
    subjects = []
    memory_types = []
    namespaces = []
    for index, item in enumerate(items, start=1):
        for field in ("memoryType", "namespace", "subject", "predicate", "object", "confidence"):
            if field not in item:
                item_errors.append(f"{key} item {index}: missing {field}.")
        if isinstance(item, dict):
            if item.get("subject"):
                subjects.append(item["subject"])
            if item.get("memoryType"):
                memory_types.append(item["memoryType"])
            if item.get("namespace"):
                namespaces.append(item["namespace"])

    errors.extend(item_errors)

    return {
        "key": key,
        "path": expected_path,
        "pinnedSha256": pinned_sha256,
        "currentSha256": current_sha256,
        "hashMatches": pinned_sha256 == current_sha256,
        "excerptCount": len(excerpts),
        "staleExcerptCount": len(stale_excerpts),
        "memoryItemCount": len(items),
        "memoryTypes": sorted(set(memory_types)),
        "namespaces": sorted(set(namespaces)),
        "subjects": subjects,
        "errors": errors,
    }


def build_report(args):
    seed_python = extract_seed_python(seed_script)
    documents = load_documents(seed_python)
    document_reports = []
    errors = []

    for key in required_documents:
        document = documents.get(key)
        if not isinstance(document, dict):
            errors.append(f"Seed documents manifest is missing {key!r}.")
            continue
        report = document_report(key, document)
        document_reports.append(report)
        errors.extend(report["errors"])

    checks = policy_checks()
    for check in checks:
        if not check["present"]:
            errors.append(f"Memory vs Markdown policy is missing required fragment: {check['fragment']}")

    sync_status = "clear" if not errors else "needs_sync"
    return {
        "status": "dry_run" if args.dry_run else sync_status,
        "syncStatus": sync_status,
        "payloadSafe": True,
        "rawSourcePayloadsIncluded": False,
        "canonicalPolicy": "Roadmap targets and backlog priorities stay in Markdown; memory stores source-linked retrieval summaries only.",
        "canonicalSources": [canonical_paths[key] for key in required_documents],
        "policyChecks": checks,
        "seedScript": str(seed_script.relative_to(repo_root)) if seed_script.is_relative_to(repo_root) else str(seed_script),
        "requiredSeedDocuments": document_reports,
        "commands": {
            "hygiene": "scripts/source-backed-memory-hygiene.sh",
            "seedDryRun": "MEMORYSYSTEM_KNOWLEDGE_SEED_DRY_RUN=true scripts/seed-production-knowledge-base.sh",
            "seedLive": "scripts/seed-production-knowledge-base.sh",
        },
        "reviewChecklist": [
            "Update docs/roadmap.md and docs/backlog.md before changing roadmap or backlog memory.",
            "Refresh curated excerpts and sourceSha256 values in scripts/seed-production-knowledge-base.sh after Markdown changes.",
            "Run source-backed hygiene and the knowledge seed dry run before live seeding.",
            "Live seed only compact high-value state with source evidence and source hashes.",
            "Record context feedback when retrieved roadmap or backlog memory is stale, wrong, missing, or over-broad.",
        ],
        "errors": errors,
    }


def build_parser():
    parser = argparse.ArgumentParser(
        prog="backlog-roadmap-memory-sync.sh",
        description="Validate payload-safe roadmap/backlog memory sync from Markdown to source-backed seed memory.",
    )
    parser.add_argument("--dry-run", action="store_true", help="Validate without writing memory.")
    return parser


try:
    parser = build_parser()
    parsed = parser.parse_args(argv)
    print(json.dumps(build_report(parsed), indent=2, sort_keys=True))
except SyncError as exc:
    print(str(exc), file=sys.stderr)
    sys.exit(2)
PY
