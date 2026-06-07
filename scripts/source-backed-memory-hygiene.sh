#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SEED_SCRIPT="${MEMORYSYSTEM_KNOWLEDGE_SEED_SCRIPT:-$ROOT_DIR/scripts/seed-production-knowledge-base.sh}"
PROJECT_ID="${MEMORYSYSTEM_CANONICAL_PROJECT_ID:-9f8e7d6c-5b4a-4321-9123-abcdef123002}"

python3 - "$ROOT_DIR" "$SEED_SCRIPT" "$PROJECT_ID" <<'PY'
import ast
import hashlib
import json
import re
import subprocess
import sys
from pathlib import Path, PurePosixPath

repo_root = Path(sys.argv[1])
seed_script = Path(sys.argv[2])
project_id = sys.argv[3]

canonical_memory_types = {
    "goal",
    "target",
    "fact",
    "decision",
    "rationale",
    "risk",
    "assumption",
    "constraint",
    "requirement",
    "release_evidence",
    "role_lens",
}

role_lens_namespace_patterns = (
    re.compile(r"^/role/[a-z0-9_]+/shared(?:/.*)?$"),
    re.compile(r"^/org/[0-9a-fA-F-]{36}/role/[a-z0-9_]+/lens(?:/.*)?$"),
    re.compile(r"^/project/[0-9a-fA-F-]{36}/role/[a-z0-9_]+/lens(?:/.*)?$"),
)


def fail(message):
    print(message, file=sys.stderr)
    sys.exit(2)


def extract_seed_python(script_path):
    try:
        lines = script_path.read_text(encoding="utf-8").splitlines()
    except FileNotFoundError:
        fail(f"Seed script does not exist: {script_path}")

    start = None
    for index, line in enumerate(lines):
        if "<<'PY'" in line:
            start = index + 1
            break

    if start is None:
        fail(f"Seed script does not contain the expected Python heredoc: {script_path}")

    for index in range(start, len(lines)):
        if lines[index] == "PY":
            return "\n".join(lines[start:index])

    fail(f"Seed script Python heredoc is not terminated: {script_path}")


def extract_documents_expression(seed_python):
    marker = "documents = {"
    start = seed_python.find(marker)
    if start < 0:
        fail("Seed Python does not define a documents map.")

    end = seed_python.find("\n\ndef read_source", start)
    if end < 0:
        fail("Seed Python does not define read_source after the documents map.")

    assignment_source = seed_python[start:end]
    module = ast.parse(assignment_source, filename=str(seed_script))
    assignments = [
        statement
        for statement in module.body
        if isinstance(statement, ast.Assign)
        and any(isinstance(target, ast.Name) and target.id == "documents" for target in statement.targets)
    ]
    if len(assignments) != 1:
        fail("Seed Python must contain exactly one documents assignment in the seed manifest block.")

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
            fail(f"Seed documents manifest uses unsupported Python syntax: {type(node).__name__}")
        if isinstance(node, ast.Name) and node.id != "project_id":
            fail(f"Seed documents manifest references unsupported name: {node.id}")

    return expression


def load_documents(seed_python):
    expression = extract_documents_expression(seed_python)
    documents = eval(
        compile(expression, filename=str(seed_script), mode="eval"),
        {"__builtins__": {}},
        {"project_id": project_id},
    )

    if not isinstance(documents, dict) or not documents:
        fail("Seed documents manifest must be a non-empty object.")

    return documents


def is_tracked_source(source_path):
    result = subprocess.run(
        ["git", "-C", str(repo_root), "ls-files", "--error-unmatch", "--", source_path],
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
        check=False,
    )
    return result.returncode == 0


def clean_relative_path(value):
    if not isinstance(value, str) or not value.strip():
        return None
    if value.startswith("/") or "\\" in value:
        return None
    path = PurePosixPath(value)
    if any(part in {"", ".", ".."} for part in path.parts):
        return None
    if str(path) != value:
        return None
    return value


def is_non_empty_string(value):
    return isinstance(value, str) and bool(value.strip())


def identity_hash(item):
    material = {
        "memoryType": item["memoryType"].strip().lower(),
        "namespace": item["namespace"].strip().lower(),
        "subject": item["subject"].strip().lower(),
        "predicate": item["predicate"].strip().lower(),
        "object": item["object"].strip(),
    }
    body = json.dumps(material, separators=(",", ":"), sort_keys=True).encode("utf-8")
    return hashlib.sha256(body).hexdigest()


def validates_role_lens_namespace(namespace):
    return any(pattern.match(namespace) for pattern in role_lens_namespace_patterns)


def validate_seed_payload_contract(seed_python, errors):
    required_fragments = {
        "sourcePath evidence": '"sourcePath": document["path"]',
        "source hash evidence": '"sourceContentSha256": source["sha256"]',
        "curated excerpts evidence": '"excerpts": document["excerpts"]',
        "source hash drift check": 'expected_sha256 = document.get("sourceSha256")',
        "role lens role id passthrough": '"roleId"',
        "role lens base fact passthrough": '"baseMemoryFactId"',
    }

    for name, fragment in required_fragments.items():
        if fragment not in seed_python:
            errors.append(f"seed payload contract is missing {name}.")


def validate_documents(documents, errors):
    source_summaries = []
    memory_locations = {}
    memory_count = 0
    duplicate_identity_count = 0

    for document_key, document in documents.items():
        doc_location = f"document {document_key!r}"

        if not is_non_empty_string(document_key):
            errors.append("document key must be a non-empty string.")
            continue
        if not isinstance(document, dict):
            errors.append(f"{doc_location}: entry must be an object.")
            continue

        for field in ("path", "sourceSha256", "title", "summary", "excerpts", "items"):
            if field not in document:
                errors.append(f"{doc_location}: missing required field {field!r}.")

        source_path = clean_relative_path(document.get("path"))
        if source_path is None:
            errors.append(f"{doc_location}: source path must be a clean repository-relative path.")
            continue

        source_file = repo_root / source_path
        source_text = None
        source_sha256 = None
        if not source_file.exists():
            errors.append(f"{doc_location}: stale source link does not exist: {source_path}.")
        elif not source_file.is_file():
            errors.append(f"{doc_location}: source link is not a file: {source_path}.")
        else:
            if not is_tracked_source(source_path):
                errors.append(f"{doc_location}: source link is not tracked by git: {source_path}.")

            source_bytes = source_file.read_bytes()
            source_sha256 = hashlib.sha256(source_bytes).hexdigest()
            try:
                source_text = source_bytes.decode("utf-8")
            except UnicodeDecodeError:
                errors.append(f"{doc_location}: source file is not UTF-8 text: {source_path}.")

        expected_sha256 = document.get("sourceSha256")
        if not is_non_empty_string(expected_sha256):
            errors.append(f"{doc_location}: missing pinned sourceSha256 for {source_path}.")
        elif not re.fullmatch(r"[0-9a-f]{64}", expected_sha256):
            errors.append(f"{doc_location}: sourceSha256 must be a lower-case SHA-256 hex digest.")
        elif source_sha256 is not None and source_sha256 != expected_sha256:
            errors.append(
                f"{doc_location}: source hash drift for {source_path}; "
                f"expected {expected_sha256}, found {source_sha256}."
            )

        for field in ("title", "summary"):
            if not is_non_empty_string(document.get(field)):
                errors.append(f"{doc_location}: {field} must be a non-empty string.")

        excerpts = document.get("excerpts")
        if not isinstance(excerpts, list) or not excerpts:
            errors.append(f"{doc_location}: excerpts must be a non-empty list.")
        else:
            for excerpt_index, excerpt in enumerate(excerpts, start=1):
                if not is_non_empty_string(excerpt):
                    errors.append(f"{doc_location}: excerpt {excerpt_index} must be a non-empty string.")
                elif source_text is not None and excerpt not in source_text:
                    errors.append(f"{doc_location}: excerpt {excerpt_index} is stale for {source_path}.")

        items = document.get("items")
        if not isinstance(items, list) or not items:
            errors.append(f"{doc_location}: items must be a non-empty list.")
            continue

        memory_count += len(items)
        for item_index, item in enumerate(items, start=1):
            item_location = f"{doc_location} item {item_index}"
            if not isinstance(item, dict):
                errors.append(f"{item_location}: memory item must be an object.")
                continue

            for field in ("memoryType", "namespace", "subject", "predicate", "object", "confidence"):
                if field not in item:
                    errors.append(f"{item_location}: missing required field {field!r}.")

            if not all(is_non_empty_string(item.get(field)) for field in ("memoryType", "namespace", "subject", "predicate", "object")):
                errors.append(f"{item_location}: memoryType, namespace, subject, predicate, and object must be non-empty strings.")
                continue

            memory_type = item["memoryType"].strip().lower()
            namespace = item["namespace"].strip()
            if memory_type not in canonical_memory_types:
                errors.append(f"{item_location}: memoryType {memory_type!r} is not canonical.")

            if memory_type == "role_lens":
                if not validates_role_lens_namespace(namespace):
                    errors.append(f"{item_location}: role_lens namespace must use the canonical role-lens shape.")
                if namespace.startswith("/project/") and not namespace.startswith(f"/project/{project_id}/role/"):
                    errors.append(f"{item_location}: project role_lens namespace must target the canonical project id.")
                for field in ("roleId", "baseMemoryFactId"):
                    if not is_non_empty_string(item.get(field)):
                        errors.append(f"{item_location}: role_lens memory requires {field}.")
            elif not namespace.startswith(f"/project/{project_id}/"):
                errors.append(f"{item_location}: namespace must stay under the canonical project.")

            confidence = item.get("confidence")
            if not isinstance(confidence, (int, float)) or isinstance(confidence, bool):
                errors.append(f"{item_location}: confidence must be a number.")
            elif confidence < 0 or confidence > 1:
                errors.append(f"{item_location}: confidence must be between 0 and 1.")

            duplicate_key = identity_hash(item)
            if duplicate_key in memory_locations:
                duplicate_identity_count += 1
                errors.append(
                    f"{item_location}: duplicate memory identity {duplicate_key[:16]} "
                    f"already appears at {memory_locations[duplicate_key]}."
                )
            else:
                memory_locations[duplicate_key] = item_location

        if source_sha256 is not None:
            source_summaries.append(
                {
                    "path": source_path,
                    "sha256": source_sha256,
                    "memoryCount": len(items) if isinstance(items, list) else 0,
                    "excerptCount": len(excerpts) if isinstance(excerpts, list) else 0,
                }
            )

    return {
        "documentCount": len(documents),
        "memoryCount": memory_count,
        "sourceFiles": source_summaries,
        "duplicateIdentityCount": duplicate_identity_count,
    }


seed_python = extract_seed_python(seed_script)
documents = load_documents(seed_python)
errors = []

validate_seed_payload_contract(seed_python, errors)
summary = validate_documents(documents, errors)
summary["checks"] = [
    "source_hash_drift",
    "stale_source_links",
    "duplicate_memory_identities",
    "missing_evidence",
    "curated_excerpt_drift",
    "canonical_memory_types",
    "role_lens_source_contract",
]

if errors:
    print("Source-backed memory hygiene check failed.", file=sys.stderr)
    for error in errors:
        print(f"- {error}", file=sys.stderr)
    summary["errorCount"] = len(errors)
    print(json.dumps(summary, indent=2, sort_keys=True))
    sys.exit(1)

summary["errorCount"] = 0
print("Source-backed memory hygiene check passed.")
print(json.dumps(summary, indent=2, sort_keys=True))
PY
