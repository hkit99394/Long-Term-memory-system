#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENV_FILE="${MEMORYSYSTEM_WEEKLY_REVIEW_ENV_FILE:-$ROOT_DIR/.env.production}"

python3 - "$ROOT_DIR" "$ENV_FILE" "$@" <<'PY'
import argparse
import hashlib
import json
import os
import subprocess
import sys
import urllib.error
import urllib.parse
import urllib.request
from datetime import datetime, timezone
from pathlib import Path

repo_root = Path(sys.argv[1])
env_file = Path(sys.argv[2])
argv = sys.argv[3:]

default_project_id = "9f8e7d6c-5b4a-4321-9123-abcdef123002"
feedback_types = ["stale", "wrong", "sensitive", "over_broad", "missing"]
reviewable_feedback_types = {"stale", "wrong", "sensitive"}


class WorkflowError(RuntimeError):
    pass


def utc_now():
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


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


def effective_base_url():
    configured = os.environ.get("MEMORYSYSTEM_API_BASE_URL")
    if configured:
        return configured.rstrip("/")

    port = read_env_value("MEMORYSYSTEM_LOCAL_ACCESS_PORT", "8081")
    return f"http://127.0.0.1:{port}".rstrip("/")


def effective_api_key():
    return os.environ.get("MEMORYSYSTEM_API_KEY") or read_env_value("MEMORYSYSTEM_OPERATOR_API_KEY")


def require_api_key():
    api_key = effective_api_key()
    if not api_key:
        raise WorkflowError(
            "MEMORYSYSTEM_API_KEY or MEMORYSYSTEM_OPERATOR_API_KEY must be configured. "
            "The key is read from the environment or env file and is never printed."
        )
    return api_key


def request_json(method, path):
    api_key = require_api_key()
    request = urllib.request.Request(
        effective_base_url() + path,
        headers={"X-Api-Key": api_key},
        method=method,
    )
    try:
        with urllib.request.urlopen(request, timeout=20) as response:
            payload = response.read().decode("utf-8")
            return json.loads(payload) if payload else {}
    except urllib.error.HTTPError as exc:
        payload = exc.read().decode("utf-8", errors="replace")
        raise WorkflowError(f"{method} {path} failed with HTTP {exc.code}: {payload}") from exc
    except urllib.error.URLError as exc:
        raise WorkflowError(f"{method} {path} failed: {exc.reason}") from exc


def safe_review(review):
    memory = review.get("memory") or {}
    return {
        "reviewId": review.get("id"),
        "reviewStatus": review.get("reviewStatus"),
        "memoryId": memory.get("id"),
        "memoryType": memory.get("memoryType"),
        "scopeType": memory.get("scopeType"),
        "scopeId": memory.get("scopeId"),
        "namespace": memory.get("namespace"),
        "memoryStatus": memory.get("status"),
        "sourceEventId": review.get("sourceEventId") or memory.get("sourceEventId"),
        "sourceLink": review.get("sourceLink") or memory.get("sourceLink"),
        "createdAt": review.get("createdAt"),
        "updatedAt": review.get("updatedAt"),
        "suggestedActions": ["approve", "reject", "edit", "expire", "delete", "supersede"],
    }


def safe_observation(observation):
    memory = observation.get("memory") or {}
    feedback_type = observation.get("feedbackType")
    return {
        "feedbackId": observation.get("id"),
        "feedbackType": feedback_type,
        "packetId": observation.get("packetId"),
        "itemId": observation.get("itemId"),
        "memoryId": memory.get("id") or observation.get("reviewMemoryFactId"),
        "memoryType": memory.get("memoryType"),
        "scopeType": memory.get("scopeType"),
        "scopeId": memory.get("scopeId"),
        "namespace": memory.get("namespace"),
        "sourceType": observation.get("sourceType"),
        "sourceId": observation.get("sourceId"),
        "reviewSourceEventId": observation.get("reviewSourceEventId"),
        "reviewSourceLink": observation.get("reviewSourceLink"),
        "existingPendingReviewId": observation.get("existingPendingReviewId"),
        "reviewable": bool(observation.get("reviewable")),
        "suggestedActions": observation.get("suggestedActions") or (
            ["open_review"] if feedback_type in reviewable_feedback_types else ["triage_manually"]
        ),
        "createdAt": observation.get("createdAt"),
    }


def normalize_identity(value):
    if value is None:
        return ""
    return " ".join(str(value).strip().lower().split())


def duplicate_identity(fact):
    fields = [
        normalize_identity(fact.get("memoryType")),
        normalize_identity(fact.get("namespace")),
        normalize_identity(fact.get("subject")),
        normalize_identity(fact.get("predicate")),
        normalize_identity(fact.get("object")),
    ]
    if not all(fields):
        return None
    body = json.dumps(fields, separators=(",", ":")).encode("utf-8")
    return hashlib.sha256(body).hexdigest()


def find_duplicate_candidates(facts):
    groups = {}
    for fact in facts:
        identity = duplicate_identity(fact)
        if identity is None:
            continue
        groups.setdefault(identity, []).append(fact)

    duplicates = []
    for identity, rows in sorted(groups.items()):
        if len(rows) < 2:
            continue
        duplicates.append(
            {
                "identityHash": identity[:16],
                "count": len(rows),
                "memoryIds": [row.get("id") for row in rows],
                "memoryTypes": sorted({row.get("memoryType") for row in rows if row.get("memoryType")}),
                "namespaces": sorted({row.get("namespace") for row in rows if row.get("namespace")}),
                "suggestedActions": ["compare_source_evidence", "open_review_or_supersede_duplicate"],
            }
        )
    return duplicates


def run_source_hygiene(skip):
    if skip:
        return {
            "status": "skipped",
            "suggestedActions": ["run scripts/source-backed-memory-hygiene.sh before closing weekly review"],
        }

    script = repo_root / "scripts" / "source-backed-memory-hygiene.sh"
    result = subprocess.run(
        [str(script)],
        cwd=repo_root,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=False,
    )
    return {
        "status": "pass" if result.returncode == 0 else "needs_review",
        "exitCode": result.returncode,
        "stdoutLines": result.stdout.splitlines()[:20],
        "stderrLines": result.stderr.splitlines()[:20],
        "suggestedActions": []
        if result.returncode == 0
        else ["refresh_seed_source_hashes", "recurate_excerpts", "review_source_backed_memory"],
    }


def operations_feedback_counts(summary):
    retrieval = summary.get("retrievalFeedback") or {}
    by_type = retrieval.get("byType") or []
    counts = {}
    if isinstance(by_type, list):
        for row in by_type:
            feedback_type = row.get("feedbackType")
            if feedback_type:
                counts[feedback_type] = row
    elif isinstance(by_type, dict):
        counts = by_type

    return {
        feedback_type: counts.get(feedback_type, {"count": 0, "share": 0, "perHour": 0})
        for feedback_type in feedback_types
    }


def access_boundary_review_command(args):
    return (
        "scripts/access-boundary-review.sh "
        f"--scope-type {args.scope_type} "
        f"--scope-id {args.scope_id}"
    )


def collect(args):
    pending_reviews = request_json(
        "GET",
        "/api/reviews/pending?" + urllib.parse.urlencode({"limit": str(args.review_limit)}),
    ).get("reviews") or []

    observations_by_type = {}
    for feedback_type in feedback_types:
        response = request_json(
            "GET",
            "/api/reviews/context-observations?"
            + urllib.parse.urlencode({"feedbackType": feedback_type, "limit": str(args.observation_limit)}),
        )
        observations_by_type[feedback_type] = response.get("observations") or []

    fact_query = {
        "scopeType": args.scope_type,
        "scopeId": args.scope_id,
        "status": "active",
        "limit": str(args.memory_fact_limit),
    }
    facts = request_json(
        "GET",
        "/api/admin/memory/facts?" + urllib.parse.urlencode(fact_query),
    ).get("facts") or []

    operations_summary = request_json("GET", "/api/operations/summary")

    reviewable_feedback = [
        safe_observation(observation)
        for feedback_type in sorted(reviewable_feedback_types)
        for observation in observations_by_type.get(feedback_type, [])
    ]
    non_reviewable_feedback = {
        "over_broad": [safe_observation(observation) for observation in observations_by_type.get("over_broad", [])],
        "missing": {
            "recentCount": operations_feedback_counts(operations_summary)["missing"].get("count", 0),
            "source": "/api/operations/summary",
            "suggestedActions": ["turn_missing_feedback_into_seed_or_backlog_item"],
        },
    }

    duplicate_candidates = find_duplicate_candidates(facts)
    source_drift = run_source_hygiene(args.skip_source_hygiene)
    pending_review_items = [safe_review(review) for review in pending_reviews]

    queue_counts = {
        "pendingReviews": len(pending_review_items),
        "reviewableFeedback": len(reviewable_feedback),
        "overBroadObservations": len(non_reviewable_feedback["over_broad"]),
        "missingFeedbackRecent": non_reviewable_feedback["missing"]["recentCount"],
        "duplicateCandidateGroups": len(duplicate_candidates),
        "sourceDrift": 1 if source_drift["status"] == "needs_review" else 0,
    }

    return {
        "status": "needs_review" if any(queue_counts.values()) else "clear",
        "generatedAt": utc_now(),
        "payloadSafe": True,
        "rawSourcePayloadsIncluded": False,
        "targetScopeType": args.scope_type,
        "targetScopeId": args.scope_id,
        "queueCounts": queue_counts,
        "queues": {
            "pendingReviews": pending_review_items,
            "reviewableFeedback": reviewable_feedback,
            "nonReviewableFeedback": non_reviewable_feedback,
            "duplicateCandidates": duplicate_candidates,
            "sourceDrift": source_drift,
        },
        "accessBoundaryReviewCommand": access_boundary_review_command(args),
        "nextActions": [
            "Open /reviews/ for pending review decisions.",
            "Open /admin/ to inspect memory facts and source links.",
            "Open reviewable stale, wrong, or sensitive context observations.",
            "Convert missing feedback into source-backed docs, backlog, or memory seed work.",
            "Resolve duplicate candidate groups through review, supersede, or source-backed cleanup.",
            "Run source-backed memory hygiene before closing weekly review.",
            "Run the IP-11 access boundary review for permission drift, service accounts, OIDC bindings, and break-glass posture.",
        ],
    }


def dry_run(args):
    return {
        "status": "dry_run",
        "payloadSafe": True,
        "rawSourcePayloadsIncluded": False,
        "targetScopeType": args.scope_type,
        "targetScopeId": args.scope_id,
        "checks": [
            "pending_reviews",
            "reviewable_feedback_stale_wrong_sensitive",
            "over_broad_feedback_triage",
            "missing_feedback_metrics",
            "duplicate_memory_candidates",
            "source_drift_hygiene",
            "access_boundary_permission_drift",
        ],
        "endpoints": [
            "/api/reviews/pending",
            "/api/reviews/context-observations?feedbackType=stale",
            "/api/reviews/context-observations?feedbackType=wrong",
            "/api/reviews/context-observations?feedbackType=sensitive",
            "/api/reviews/context-observations?feedbackType=over_broad",
            "/api/reviews/context-observations?feedbackType=missing",
            "/api/admin/memory/facts",
            "/api/operations/summary",
            "/api/admin/access/permission-drift",
        ],
        "sourceHygieneCommand": "scripts/source-backed-memory-hygiene.sh",
        "accessBoundaryReviewCommand": access_boundary_review_command(args),
        "feedbackTypes": feedback_types,
    }


def build_parser():
    parser = argparse.ArgumentParser(
        prog="weekly-admin-review-workflow.sh",
        description="Collect a payload-safe weekly admin memory review queue.",
    )
    parser.add_argument("--scope-type", default="project", help="Target scope type for duplicate candidate scan.")
    parser.add_argument(
        "--scope-id",
        default=os.environ.get("MEMORYSYSTEM_CANONICAL_PROJECT_ID", default_project_id),
        help="Target scope id for duplicate candidate scan.",
    )
    parser.add_argument("--review-limit", type=int, default=50, help="Pending review limit.")
    parser.add_argument("--observation-limit", type=int, default=50, help="Context observation limit per feedback type.")
    parser.add_argument("--memory-fact-limit", type=int, default=100, help="Admin memory facts limit for duplicate scan.")
    parser.add_argument("--skip-source-hygiene", action="store_true", help="Skip source-backed hygiene check.")
    parser.add_argument("--dry-run", action="store_true", help="Print planned checks without API calls.")
    return parser


try:
    parser = build_parser()
    parsed = parser.parse_args(argv)
    payload = dry_run(parsed) if parsed.dry_run else collect(parsed)
    print(json.dumps(payload, indent=2, sort_keys=True))
except WorkflowError as exc:
    print(str(exc), file=sys.stderr)
    sys.exit(2)
PY
