#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENV_FILE="${MEMORYSYSTEM_PRODUCTION_ENV_FILE:-$ROOT_DIR/.env.production}"
API_BASE_URL="${MEMORYSYSTEM_API_BASE_URL:-http://127.0.0.1:8081}"
PROJECT_ID="${MEMORYSYSTEM_CANONICAL_PROJECT_ID:-9f8e7d6c-5b4a-4321-9123-abcdef123002}"
SEED_VERSION="${MEMORYSYSTEM_KNOWLEDGE_SEED_VERSION:-prod-knowledge-v1}"
DRY_RUN="${MEMORYSYSTEM_KNOWLEDGE_SEED_DRY_RUN:-false}"

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

api_key="$(read_env_value MEMORYSYSTEM_OPERATOR_API_KEY "")"

if ! is_true "$DRY_RUN" && [[ -z "$api_key" ]]; then
  printf 'MEMORYSYSTEM_OPERATOR_API_KEY must be configured in %s or the environment.\n' "$ENV_FILE" >&2
  exit 64
fi

python3 - "$API_BASE_URL" "$api_key" "$PROJECT_ID" "$SEED_VERSION" "$ROOT_DIR" "$DRY_RUN" <<'PY'
import hashlib
import json
import sys
import urllib.error
import urllib.request
from pathlib import Path

base_url, api_key, project_id, seed_version, root_dir, dry_run_raw = sys.argv[1:7]
repo_root = Path(root_dir)
dry_run = dry_run_raw.strip().lower() in {"1", "true", "yes", "y", "on"}

documents = {
    "project-goal": {
        "path": "docs/project-goal.md",
        "title": "Project Goal",
        "summary": "North-star definition for trustworthy, auditable, permission-aware long-term memory.",
        "excerpts": [
            "Build a durable, auditable long-term memory system for AI agents that can remember useful facts, preferences, decisions, role-specific perspectives, and project history without turning memory into an uncontrolled prompt dump.",
            "The goal is not simply to store more context.",
            "Build a trustworthy long-term memory layer for AI agents where Postgres stores truth, pgvector enables recall, events preserve evidence, the Memory Broker controls writes, the Context Builder controls reads, and humans can review, correct, and govern memory over time."
        ],
        "items": [
            {
                "memoryType": "fact",
                "namespace": f"/project/{project_id}/goals",
                "subject": "project north star",
                "predicate": "is",
                "object": "Build a trustworthy long-term memory layer for AI agents where durable memories have scope, provenance, confidence, lifecycle state, and access boundaries.",
                "confidence": 0.96
            },
            {
                "memoryType": "fact",
                "namespace": f"/project/{project_id}/goals",
                "subject": "project non-goal",
                "predicate": "rejects",
                "object": "The system should not become an uncontrolled prompt dump or simply store more context without governance.",
                "confidence": 0.94
            }
        ]
    },
    "architecture": {
        "path": "docs/architecture.md",
        "title": "Architecture Overview",
        "summary": "Core architecture boundaries, write path, read path, and trust model.",
        "excerpts": [
            "Postgres stores truth, pgvector supports recall, the Memory Broker controls writes, and the Context Builder controls reads.",
            "Durable writes must pass through the Memory Broker.",
            "Context construction must pass through the Context Builder.",
            "Unauthorized rows must not enter candidate sets for full-text or vector search."
        ],
        "items": [
            {
                "memoryType": "decision",
                "namespace": f"/project/{project_id}/decisions",
                "subject": "source of truth architecture",
                "predicate": "uses",
                "object": "PostgreSQL is the durable source of truth; pgvector is rebuildable recall support rather than authoritative memory.",
                "confidence": 0.96
            },
            {
                "memoryType": "decision",
                "namespace": f"/project/{project_id}/decisions",
                "subject": "memory write path",
                "predicate": "requires",
                "object": "Durable memory writes must pass through the Memory Broker with source evidence, scope validation, permission enforcement, deduplication, contradiction checks, confidence, review state, and transactional storage.",
                "confidence": 0.95
            },
            {
                "memoryType": "decision",
                "namespace": f"/project/{project_id}/decisions",
                "subject": "memory read path",
                "predicate": "requires",
                "object": "Context construction must pass through the Context Builder, retrieve only authorized candidates, exclude inactive or unsafe memory, rank compactly, and return source-linked context.",
                "confidence": 0.95
            },
            {
                "memoryType": "fact",
                "namespace": f"/project/{project_id}/facts",
                "subject": "retrieval safety invariant",
                "predicate": "requires",
                "object": "Unauthorized rows must not enter full-text, semantic, hybrid, or context-packet candidate sets before ranking.",
                "confidence": 0.94
            },
            {
                "memoryType": "fact",
                "namespace": f"/project/{project_id}/facts",
                "subject": "role memory model",
                "predicate": "treats",
                "object": "Roles are lenses over shared truth, not separate realities; project facts must not leak across project or role boundaries.",
                "confidence": 0.93
            }
        ]
    },
    "release-go": {
        "path": "docs/external-pilot-go-epr04-v1.0.0-2026-06-04.md",
        "title": "External Pilot GO EPR-04 v1.0.0",
        "summary": "Owner-approved GO decision for version 1.0.0 external pilot and residual evidence hardening.",
        "excerpts": [
            "Date: 2026-06-04",
            "Status: GO recorded; external pilot invite approved for version 1.0.0.",
            "those target-environment artifacts from external-invite blockers to post-GO"
        ],
        "items": [
            {
                "memoryType": "decision",
                "namespace": f"/project/{project_id}/release-evidence",
                "subject": "version 1.0.0 external pilot",
                "predicate": "is",
                "object": "GO recorded on 2026-06-04; the owner approved marking the system as version 1.0.0 and proceeding with the external pilot invite.",
                "confidence": 0.97
            },
            {
                "memoryType": "fact",
                "namespace": f"/project/{project_id}/risks",
                "subject": "post-GO evidence hardening",
                "predicate": "requires",
                "object": "Attach controlled audit-store evidence, target-environment deployment smoke, pilot-model benchmark scorecards, target governance/compliance smoke, alert receiver acknowledgement, and rollback or communication operating notes.",
                "confidence": 0.92
            }
        ]
    },
    "agent-contract": {
        "path": "docs/agent-facing-memory-contract.md",
        "title": "Agent-Facing Memory Contract",
        "summary": "LMSS v1 tool contract and agent memory-use semantics.",
        "excerpts": [
            "append source evidence before asking the system to remember something",
            "Authorization happens before ranking, summarization, or fact packaging.",
            "Targeting is explicit.",
            "Responses explain provenance."
        ],
        "items": [
            {
                "memoryType": "decision",
                "namespace": f"/project/{project_id}/decisions",
                "subject": "agent memory write contract",
                "predicate": "requires",
                "object": "Agents append source evidence first, then propose durable memory with an explicit sourceEventId; durable writes must be idempotent and evidence-backed.",
                "confidence": 0.95
            },
            {
                "memoryType": "decision",
                "namespace": f"/project/{project_id}/decisions",
                "subject": "agent memory read contract",
                "predicate": "requires",
                "object": "Agents retrieve scoped context and query facts through authorized APIs using explicit target scope and optional role context before making project-specific claims.",
                "confidence": 0.95
            },
            {
                "memoryType": "fact",
                "namespace": f"/project/{project_id}/facts",
                "subject": "memory feedback contract",
                "predicate": "supports",
                "object": "Context feedback may mark memory useful, stale, wrong, sensitive, over_broad, or missing without storing raw query text.",
                "confidence": 0.94
            },
            {
                "memoryType": "fact",
                "namespace": f"/project/{project_id}/facts",
                "subject": "agent contract v1 tool surface",
                "predicate": "includes",
                "object": "memory.appendEvent, memory.propose, memory.getContext, memory.recordContextFeedback, memory.readFact, memory.readEvidence, and memory.queryFacts.",
                "confidence": 0.93
            }
        ]
    },
    "memory-vs-markdown-policy": {
        "path": "docs/memory-vs-markdown-policy.md",
        "title": "Memory vs Markdown Policy",
        "summary": "Project source-of-truth policy for Markdown, memory, backlog, source evidence, role lenses, release evidence, and vault exports.",
        "excerpts": [
            "Markdown is the canonical source for project plans, policy, architecture, API",
            "Memory is a governed retrieval layer over source-backed facts, decisions,",
            "If a durable project claim matters for future agents, first put the source in",
            "Role lenses are role-specific interpretations of shared truth, not separate",
            "Release decisions and pilot claims require committed, payload-safe evidence."
        ],
        "items": [
            {
                "memoryType": "decision",
                "namespace": f"/project/{project_id}/decisions",
                "subject": "project source-of-truth policy",
                "predicate": "uses",
                "object": "Markdown docs and committed release evidence are canonical for project plans, policies, architecture, API contracts, runbooks, release decisions, and backlog status; memory is a source-linked retrieval projection, not the sole authority.",
                "confidence": 0.96
            },
            {
                "memoryType": "decision",
                "namespace": f"/project/{project_id}/decisions",
                "subject": "source-backed memory sync policy",
                "predicate": "requires",
                "object": "When a durable project claim affects future work, update the right Markdown or release-evidence record first, then seed or propose compact memory from that source with source evidence and a source hash.",
                "confidence": 0.95
            },
            {
                "memoryType": "fact",
                "namespace": f"/project/{project_id}/facts",
                "subject": "role lens boundary",
                "predicate": "is",
                "object": "Role lenses hold role-specific interpretation, attention, and risk framing over shared truth; they must not become alternate facts or unverified private policy.",
                "confidence": 0.94
            },
            {
                "memoryType": "fact",
                "namespace": f"/project/{project_id}/release-evidence",
                "subject": "release evidence boundary",
                "predicate": "requires",
                "object": "Release decisions and pilot claims belong in committed payload-safe evidence records and readiness JSON; memory may summarize current status only with links to those records.",
                "confidence": 0.94
            }
        ]
    },
    "project-memory-boundary": {
        "path": "docs/project-memory-boundary.md",
        "title": "Project Memory Boundary",
        "summary": "Canonical production memory scope, protected runtime habits, namespaces, and first-class role responsibilities.",
        "excerpts": [
            "The project id above is the canonical `scopeId` for durable project memory",
            "Production memory is stored in the Docker volume:",
            "The first-class operating role vocabulary is `product_owner`, `cto`,",
            "| Product Owner | `product_owner` | Goals, target users, acceptance criteria, backlog, roadmap targets, roadmap priority, and product GO/NO-GO rationale. |"
        ],
        "items": [
            {
                "memoryType": "decision",
                "namespace": f"/project/{project_id}/decisions",
                "subject": "project memory role model",
                "predicate": "uses",
                "object": "The project treats product_owner, cto, security_professional, it_manager, developer, tester_qa, release_manager, and knowledge_steward as first-class operating role lenses while retaining designer, cfo, coo, and ceo for compatibility.",
                "confidence": 0.96
            },
            {
                "memoryType": "fact",
                "namespace": f"/project/{project_id}/facts",
                "subject": "product owner responsibility",
                "predicate": "owns",
                "object": "Goals, target users, acceptance criteria, backlog, roadmap targets, roadmap priority, and product GO/NO-GO rationale.",
                "confidence": 0.95
            },
            {
                "memoryType": "fact",
                "namespace": f"/project/{project_id}/facts",
                "subject": "cto responsibility",
                "predicate": "owns",
                "object": "Architecture decisions, technology tradeoffs, platform direction, and technical risk acceptance.",
                "confidence": 0.95
            },
            {
                "memoryType": "fact",
                "namespace": f"/project/{project_id}/facts",
                "subject": "security professional responsibility",
                "predicate": "owns",
                "object": "Access boundaries, secret policy, sensitivity classification, audit policy, and retention/erasure requirements.",
                "confidence": 0.95
            },
            {
                "memoryType": "fact",
                "namespace": f"/project/{project_id}/facts",
                "subject": "it manager ops responsibility",
                "predicate": "owns",
                "object": "Runtime health, deploys, backups, restore validation, monitoring, and incident response.",
                "confidence": 0.95
            },
            {
                "memoryType": "fact",
                "namespace": f"/project/{project_id}/facts",
                "subject": "developer responsibility",
                "predicate": "owns",
                "object": "Implementation facts, API contracts, migrations, code constraints, and technical decisions.",
                "confidence": 0.95
            },
            {
                "memoryType": "fact",
                "namespace": f"/project/{project_id}/facts",
                "subject": "tester qa responsibility",
                "predicate": "owns",
                "object": "Test gates, benchmark evidence, regression risks, and release quality evidence.",
                "confidence": 0.95
            },
            {
                "memoryType": "fact",
                "namespace": f"/project/{project_id}/facts",
                "subject": "release manager responsibility",
                "predicate": "owns",
                "object": "Version state, release checklist, evidence bundle, rollback plan, and pilot readiness coordination.",
                "confidence": 0.95
            },
            {
                "memoryType": "fact",
                "namespace": f"/project/{project_id}/facts",
                "subject": "knowledge steward responsibility",
                "predicate": "owns",
                "object": "Memory taxonomy, source evidence quality, stale/wrong/duplicate review routing, and memory hygiene.",
                "confidence": 0.95
            }
        ]
    },
    "roadmap": {
        "path": "docs/roadmap.md",
        "title": "Roadmap",
        "summary": "Current delivery track, milestones, decision gates, and next milestone.",
        "excerpts": [
            "Current milestone: Middle Run production-pilot hardening baseline is complete;",
            "Next milestone: version 1.0.0 external pilot execution and post-GO target",
            "The first production-shaped win is in place: a local API and database can accept an event, broker a memory proposal, persist memory with provenance, enforce scoped reads, and return authorized hybrid context packets."
        ],
        "items": [
            {
                "memoryType": "fact",
                "namespace": f"/project/{project_id}/facts",
                "subject": "roadmap current state",
                "predicate": "says",
                "object": "M0 through M8 and the Middle Run production-pilot hardening baseline are complete; the next milestone is external pilot execution and post-GO target-environment evidence hardening.",
                "confidence": 0.95
            },
            {
                "memoryType": "fact",
                "namespace": f"/project/{project_id}/facts",
                "subject": "production-shaped baseline",
                "predicate": "can",
                "object": "Accept an event, broker a memory proposal, persist memory with provenance, enforce scoped reads, and return authorized hybrid context packets.",
                "confidence": 0.94
            }
        ]
    },
    "backlog": {
        "path": "docs/backlog.md",
        "title": "Backlog",
        "summary": "Current backlog state and immediate next work after completed gates.",
        "excerpts": [
            "replacement. The next focus is attaching post-GO target-environment evidence",
            "| EPR-01 | P0 | Done | Define the target-environment pilot rehearsal runbook.",
            "| CP-01 | P0 | Done | Define productized context packet schema."
        ],
        "items": [
            {
                "memoryType": "fact",
                "namespace": f"/project/{project_id}/facts",
                "subject": "external pilot readiness gates",
                "predicate": "are",
                "object": "EPR-01 through EPR-07 are complete, including the runbook, local pilot-equivalent evidence, GO record, documentation truth cleanup, readiness status contract, and pilot operator cockpit.",
                "confidence": 0.95
            },
            {
                "memoryType": "fact",
                "namespace": f"/project/{project_id}/facts",
                "subject": "context productization backlog",
                "predicate": "is",
                "object": "CP-01 through CP-10 are complete, covering productized context schema, packet and item ids, explanations, safe exclusions, feedback actions, review workflow, ranking loop, benchmark checks, metrics, and caller docs.",
                "confidence": 0.94
            },
            {
                "memoryType": "fact",
                "namespace": f"/project/{project_id}/risks",
                "subject": "next backlog focus",
                "predicate": "is",
                "object": "Attach post-GO target-environment evidence under the controlled evidence trail.",
                "confidence": 0.94
            }
        ]
    }
}


def read_source(document):
    source_path = repo_root / document["path"]
    try:
        source_bytes = source_path.read_bytes()
    except FileNotFoundError as exc:
        raise RuntimeError(f"Source document does not exist: {document['path']}") from exc

    try:
        source_text = source_bytes.decode("utf-8")
    except UnicodeDecodeError as exc:
        raise RuntimeError(f"Source document is not UTF-8 text: {document['path']}") from exc

    missing_excerpts = [
        excerpt
        for excerpt in document["excerpts"]
        if excerpt not in source_text
    ]
    if missing_excerpts:
        missing = "; ".join(missing_excerpts)
        raise RuntimeError(
            f"Curated excerpts are stale for {document['path']}. "
            f"Update the seed before writing memory. Missing excerpts: {missing}"
        )

    return {
        "sha256": hashlib.sha256(source_bytes).hexdigest(),
        "byteLength": len(source_bytes),
        "lineCount": source_text.count("\n") + (1 if source_text else 0),
    }


def digest_item(item):
    body = json.dumps(item, separators=(",", ":"), sort_keys=True).encode("utf-8")
    return hashlib.sha256(body).hexdigest()


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


def append_event(document_key, document, source):
    body = {
        "eventType": "user_message",
        "scopeType": "project",
        "scopeId": project_id,
        "trustLevel": "user_scoped",
        "retentionClass": "audit",
        "sensitivity": "none",
        "payload": {
            "sourceType": "repository_document",
            "sourcePath": document["path"],
            "sourceContentSha256": source["sha256"],
            "sourceByteLength": source["byteLength"],
            "sourceLineCount": source["lineCount"],
            "seedVersion": seed_version,
            "title": document["title"],
            "summary": document["summary"],
            "excerpts": document["excerpts"],
            "curationPolicy": "Curated excerpts are validated against the current repository file, and idempotency keys are versioned by source and item hash."
        },
    }
    _, payload = request_json(
        "POST",
        "/api/events",
        body,
        idempotency_key=f"{seed_version}:event:{document_key}:{source['sha256'][:16]}",
    )
    return payload["id"]


def propose_memory(document_key, index, source_event_id, source, item):
    item_hash = digest_item(item)
    body = {
        "sourceEventId": source_event_id,
        "memoryType": item["memoryType"],
        "scopeType": "project",
        "scopeId": project_id,
        "namespace": item["namespace"],
        "visibility": "project_shared",
        "subject": item["subject"],
        "predicate": item["predicate"],
        "object": item["object"],
        "confidence": item["confidence"],
        "trustLevel": "user_scoped",
        "sensitivity": "none",
    }
    _, payload = request_json(
        "POST",
        "/api/memory/proposals",
        body,
        idempotency_key=f"{seed_version}:proposal:{document_key}:{source['sha256'][:16]}:{index:02d}:{item_hash[:16]}",
    )
    return payload


stored = []
review = []
other = []

sources = {
    document_key: read_source(document)
    for document_key, document in documents.items()
}

if dry_run:
    print("Dry run: validated source-backed knowledge seed inputs.")
    for document_key, document in documents.items():
        source = sources[document_key]
        print(
            f"{document['path']}: sha256={source['sha256']} "
            f"bytes={source['byteLength']} memories={len(document['items'])}"
        )
    sys.exit(0)

for document_key, document in documents.items():
    source = sources[document_key]
    source_event_id = append_event(document_key, document, source)
    print(f"{document['path']}: source event {source_event_id}")

    for index, item in enumerate(document["items"], start=1):
        result = propose_memory(document_key, index, source_event_id, source, item)
        row = {
            "document": document["path"],
            "subject": item["subject"],
            "decision": result.get("decision"),
            "memoryId": result.get("memoryId"),
            "reason": result.get("reason"),
        }
        if result.get("decision") == "stored":
            stored.append(row)
        elif result.get("decision") == "review_required":
            review.append(row)
        else:
            other.append(row)
        print(f"  {result.get('decision')}: {item['subject']} ({result.get('memoryId') or result.get('reason')})")

print("")
print(f"Stored memories: {len(stored)}")
print(f"Review required: {len(review)}")
print(f"Other decisions: {len(other)}")

if review or other:
    print(json.dumps({"review": review, "other": other}, indent=2))
PY
