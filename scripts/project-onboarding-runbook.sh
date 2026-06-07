#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

python3 - "$ROOT_DIR" "$@" <<'PY'
import argparse
import hashlib
import json
import os
import re
import sys
from pathlib import Path

repo_root = Path(sys.argv[1])
argv = sys.argv[2:]

default_org_id = "9f8e7d6c-5b4a-4321-9123-abcdef123001"
default_project_id = "9f8e7d6c-5b4a-4321-9123-abcdef123002"

canonical_memory_types = [
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
]

default_roles = [
    {
        "roleId": "product_owner",
        "owner": "Product Owner",
        "primaryOwnership": "Goals, target users, acceptance criteria, backlog, roadmap priority, and product go/no-go rationale.",
    },
    {
        "roleId": "cto",
        "owner": "CTO",
        "primaryOwnership": "Architecture decisions, platform tradeoffs, domain boundaries, migrations, reversibility, and technical risk.",
    },
    {
        "roleId": "security_professional",
        "owner": "Security Professional",
        "primaryOwnership": "Access boundaries, secret handling, sensitivity, auditability, retention, erasure, and fail-closed behavior.",
    },
    {
        "roleId": "it_manager",
        "owner": "IT Manager / Ops",
        "primaryOwnership": "Runtime health, deploy safety, backup and restore evidence, monitoring, incident response, and protected volumes.",
    },
    {
        "roleId": "developer",
        "owner": "Developer",
        "primaryOwnership": "API contracts, migrations, code constraints, testable facts, and source-grounded technical decisions.",
    },
    {
        "roleId": "tester_qa",
        "owner": "Tester / QA",
        "primaryOwnership": "Test gates, benchmark evidence, regression risks, release quality evidence, and verification commands.",
    },
    {
        "roleId": "release_manager",
        "owner": "Release Manager",
        "primaryOwnership": "Version state, release checklist, evidence bundle, rollback plan, pilot readiness, and go/no-go ownership.",
    },
    {
        "roleId": "knowledge_steward",
        "owner": "Knowledge Steward",
        "primaryOwnership": "Memory taxonomy, source evidence quality, stale/wrong/duplicate routing, namespace policy, and memory-vs-Markdown hygiene.",
    },
]

optional_role_templates = ["designer", "cfo", "coo", "ceo"]

default_seed_docs = [
    "docs/project-goal.md",
    "docs/architecture.md",
    "docs/agent-facing-memory-contract.md",
    "docs/memory-vs-markdown-policy.md",
    "docs/project-memory-boundary.md",
    "docs/project-memory-runbook.md",
    "docs/roadmap.md",
    "docs/backlog.md",
]


class OnboardingError(RuntimeError):
    pass


def relative_path(path):
    try:
        return str(path.relative_to(repo_root))
    except ValueError:
        return str(path)


def sha256_file(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def validate_scope_id(label, value):
    if not value:
        raise OnboardingError(f"{label} is required.")
    if not re.fullmatch(r"[0-9a-fA-F-]{16,64}", value):
        raise OnboardingError(f"{label} should be a stable id, usually a UUID.")
    return value


def namespace_grants(project_id, roles):
    shared = [
        {
            "namespace": f"/project/{project_id}/goals",
            "memoryTypes": ["goal", "target", "requirement"],
            "reviewOwnerRoleId": "product_owner",
            "purpose": "Project goals, target users, outcomes, and acceptance criteria.",
        },
        {
            "namespace": f"/project/{project_id}/facts",
            "memoryTypes": ["fact", "requirement", "constraint"],
            "reviewOwnerRoleId": "knowledge_steward",
            "purpose": "Stable project facts grounded in committed source evidence.",
        },
        {
            "namespace": f"/project/{project_id}/decisions",
            "memoryTypes": ["decision"],
            "reviewOwnerRoleId": "cto",
            "purpose": "Accepted architecture, platform, release, and product decisions.",
        },
        {
            "namespace": f"/project/{project_id}/rationale",
            "memoryTypes": ["rationale", "assumption"],
            "reviewOwnerRoleId": "product_owner",
            "purpose": "Reasoning behind product and implementation choices.",
        },
        {
            "namespace": f"/project/{project_id}/risks",
            "memoryTypes": ["risk", "constraint"],
            "reviewOwnerRoleId": "security_professional",
            "purpose": "Security, operational, product, release, and technical risks.",
        },
        {
            "namespace": f"/project/{project_id}/release-evidence",
            "memoryTypes": ["release_evidence"],
            "reviewOwnerRoleId": "release_manager",
            "purpose": "Payload-safe release, deploy, rollback, backup, benchmark, and go/no-go evidence.",
        },
    ]

    role_lens = [
        {
            "namespace": f"/project/{project_id}/role/{role['roleId']}/lens",
            "memoryTypes": ["role_lens"],
            "reviewOwnerRoleId": role["roleId"],
            "purpose": f"Role-specific retrieval guidance for {role['owner']}.",
        }
        for role in roles
    ]

    return shared + role_lens


def seed_document_report(path_text):
    path = Path(path_text)
    if path.is_absolute():
        source_path = path
        display_path = str(path)
    else:
        source_path = repo_root / path
        display_path = path_text

    if not source_path.exists():
        return {
            "path": display_path,
            "exists": False,
            "sha256": None,
            "headingCount": 0,
            "suggestedExcerptCount": 0,
            "errors": [f"Missing seed document: {display_path}"],
        }

    text = source_path.read_text(encoding="utf-8")
    headings = [line for line in text.splitlines() if line.startswith("#")]
    suggested_excerpt_count = min(max(len(headings), 1), 8)
    return {
        "path": relative_path(source_path),
        "exists": True,
        "sha256": sha256_file(source_path),
        "headingCount": len(headings),
        "suggestedExcerptCount": suggested_excerpt_count,
        "errors": [],
    }


def build_report(args):
    project_id = validate_scope_id("project id", args.project_id)
    organization_id = validate_scope_id("organization id", args.organization_id)
    seed_docs = args.seed_doc if args.seed_doc else default_seed_docs
    seed_reports = [seed_document_report(path) for path in seed_docs]
    errors = [error for report in seed_reports for error in report["errors"]]
    grants = namespace_grants(project_id, default_roles)

    return {
        "status": "dry_run" if args.dry_run else "ready",
        "payloadSafe": True,
        "rawSourcePayloadsIncluded": False,
        "liveWritesMemory": False,
        "targetScope": {
            "organizationId": organization_id,
            "organizationName": args.organization_name,
            "scopeType": "project",
            "scopeId": project_id,
            "projectName": args.project_name,
        },
        "setupFlow": [
            "define_project_scope",
            "confirm_role_owners",
            "grant_project_namespaces",
            "confirm_canonical_memory_types",
            "curate_seed_documents",
            "append_source_evidence",
            "propose_seed_memory",
            "seed_role_lenses",
            "verify_role_context",
            "schedule_review_cadence",
        ],
        "roleDefinitions": default_roles,
        "optionalRoleTemplates": optional_role_templates,
        "namespaceGrants": grants,
        "memoryTypes": canonical_memory_types,
        "seedDocuments": seed_reports,
        "sourceEvidencePlan": {
            "eventEndpoint": "/api/events",
            "proposalEndpoint": "/api/memory/proposals",
            "contextEndpoint": "/api/memory/context",
            "feedbackEndpoint": "/api/memory/context/feedback",
            "requiredEventFields": [
                "scopeType",
                "scopeId",
                "sourceType",
                "sourceUri",
                "sourceContentSha256",
                "occurredAt",
            ],
            "requiredProposalFields": [
                "memoryType",
                "namespace",
                "subject",
                "predicate",
                "object",
                "confidence",
                "sourceEventId",
                "sourceLink",
            ],
            "idempotencyKeys": [
                "project-onboarding:{projectId}:source-event:{sourcePath}:{sourceContentSha256}",
                "project-onboarding:{projectId}:memory-proposal:{memoryType}:{namespace}:{subject}:{sourceContentSha256}",
            ],
        },
        "reviewCadence": [
            {
                "cadence": "after onboarding",
                "ownerRoleId": "knowledge_steward",
                "action": "Verify each proposed memory has sourceEventId, sourceLink, sourceContentSha256, canonical memoryType, namespace, and confidence.",
            },
            {
                "cadence": "weekly",
                "ownerRoleId": "knowledge_steward",
                "action": "Run scripts/weekly-admin-review-workflow.sh and route stale, wrong, missing, sensitive, over-broad, and duplicate items.",
            },
            {
                "cadence": "weekly",
                "ownerRoleId": "security_professional",
                "action": "Run scripts/access-boundary-review.sh for memberships, role assignments, namespace grants, service accounts, OIDC bindings, and break-glass posture.",
            },
            {
                "cadence": "after roadmap or backlog change",
                "ownerRoleId": "product_owner",
                "action": "Run scripts/backlog-roadmap-memory-sync.sh --dry-run before reseeding roadmap or backlog memory.",
            },
            {
                "cadence": "after release or target deploy",
                "ownerRoleId": "release_manager",
                "action": "Store only payload-safe release_evidence memory that links back to committed release artifacts.",
            },
        ],
        "commands": {
            "boundarySeed": "scripts/seed-production-memory-boundary.sh",
            "sourceBackedHygiene": "scripts/source-backed-memory-hygiene.sh",
            "backlogRoadmapSync": "scripts/backlog-roadmap-memory-sync.sh --dry-run",
            "knowledgeSeedDryRun": "MEMORYSYSTEM_KNOWLEDGE_SEED_DRY_RUN=true scripts/seed-production-knowledge-base.sh",
            "knowledgeSeedLive": "scripts/seed-production-knowledge-base.sh",
            "roleLensDryRun": "scripts/role-lens-first-pass.sh --dry-run",
            "roleLensLive": "scripts/role-lens-first-pass.sh",
            "weeklyReview": f"scripts/weekly-admin-review-workflow.sh --scope-type project --scope-id {project_id}",
            "accessBoundaryReview": f"scripts/access-boundary-review.sh --scope-type project --scope-id {project_id}",
        },
        "completionCriteria": [
            "The project scope and organization scope are recorded in committed Markdown.",
            "Default role owners are confirmed or disabled before role-lens memory is seeded.",
            "Namespace grants cover shared project memory and role-lens memory without broader access than needed.",
            "Seed documents are committed, hashed, and curated before source evidence events are appended.",
            "Every durable memory proposal uses a canonical memory type and source evidence.",
            "Role context checks prove role_lens memory appears only for authorized matching roles.",
            "Weekly memory hygiene and access-boundary review cadence is scheduled.",
        ],
        "errors": errors,
    }


def build_parser():
    parser = argparse.ArgumentParser(
        prog="project-onboarding-runbook.sh",
        description="Build the IP-16 payload-safe project onboarding plan.",
    )
    parser.add_argument("--dry-run", action="store_true", help="Emit the onboarding plan without API calls or writes.")
    parser.add_argument("--organization-id", default=os.environ.get("MEMORYSYSTEM_ONBOARDING_ORGANIZATION_ID", default_org_id))
    parser.add_argument("--organization-name", default=os.environ.get("MEMORYSYSTEM_ONBOARDING_ORGANIZATION_NAME", "Personal AI Systems"))
    parser.add_argument("--project-id", default=os.environ.get("MEMORYSYSTEM_ONBOARDING_PROJECT_ID", default_project_id))
    parser.add_argument("--project-name", default=os.environ.get("MEMORYSYSTEM_ONBOARDING_PROJECT_NAME", "Long-Term Memory System"))
    parser.add_argument("--seed-doc", action="append", help="Seed document path. Repeat to override the default seed document set.")
    parser.add_argument("--output", help="Optional path to write the JSON report.")
    return parser


try:
    parsed = build_parser().parse_args(argv)
    report = build_report(parsed)
    payload = json.dumps(report, indent=2, sort_keys=True)
    if parsed.output:
        Path(parsed.output).write_text(payload + "\n", encoding="utf-8")
    print(payload)
    if report["errors"]:
        sys.exit(2)
except OnboardingError as exc:
    print(str(exc), file=sys.stderr)
    sys.exit(2)
PY
