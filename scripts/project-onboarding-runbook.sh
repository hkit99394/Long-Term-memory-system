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
role_id_pattern = re.compile(r"[a-z][a-z0-9_-]{0,63}")
guid_pattern = re.compile(r"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}")
root_namespace_prefixes = ["/global", "/org", "/project", "/user", "/role", "/agent", "/session"]

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

optional_role_definitions = [
    {
        "roleId": "designer",
        "owner": "Designer",
        "primaryOwnership": "Experience design, workflow clarity, UI copy, and operator ergonomics.",
    },
    {
        "roleId": "cfo",
        "owner": "CFO",
        "primaryOwnership": "Cost, budget impact, procurement constraints, and financial risk.",
    },
    {
        "roleId": "coo",
        "owner": "COO",
        "primaryOwnership": "Operational process, rollout sequencing, and cross-functional execution.",
    },
    {
        "roleId": "ceo",
        "owner": "CEO",
        "primaryOwnership": "Strategic direction, executive tradeoffs, and final business acceptance.",
    },
]

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
    if not valid_principal_id(value):
        raise OnboardingError(f"{label} must be a UUID.")
    return value.lower()


def valid_role_id(value):
    return bool(role_id_pattern.fullmatch(value or ""))


def valid_principal_id(value):
    return bool(guid_pattern.fullmatch(value or ""))


def parse_custom_roles(raw_roles, errors):
    roles = []
    for raw_role in raw_roles or []:
        if "=" not in raw_role:
            errors.append(f"Custom role '{raw_role}' must use role_id=Display Name.")
            continue

        role_id, display_name = raw_role.split("=", 1)
        role_id = role_id.strip()
        display_name = display_name.strip()
        if not valid_role_id(role_id):
            errors.append(f"Custom role id '{role_id}' must match ^[a-z][a-z0-9_-]{{0,63}}$.")
            continue
        if not display_name:
            errors.append(f"Custom role '{role_id}' must include a display name.")
            continue

        roles.append(
            {
                "roleId": role_id,
                "owner": display_name,
                "primaryOwnership": "Project-specific role captured during registration.",
                "custom": True,
            }
        )

    return roles


def resolve_roles(args, validation_errors):
    custom_roles = parse_custom_roles(args.custom_role, validation_errors)
    role_by_id = {role["roleId"]: dict(role, custom=False) for role in default_roles}
    for optional_role in optional_role_definitions:
        role_by_id[optional_role["roleId"]] = dict(optional_role, custom=False, optional=True)

    for custom_role in custom_roles:
        if custom_role["roleId"] in role_by_id:
            validation_errors.append(f"Custom role '{custom_role['roleId']}' duplicates an existing role.")
            continue
        role_by_id[custom_role["roleId"]] = custom_role

    default_active_role_ids = {role["roleId"] for role in default_roles}
    requested_active = set(args.active_role or default_active_role_ids)
    requested_deferred = set(args.deferred_role or [])

    for role_id in sorted(requested_active | requested_deferred):
        if not valid_role_id(role_id):
            validation_errors.append(f"Role id '{role_id}' must match ^[a-z][a-z0-9_-]{{0,63}}$.")
        elif role_id not in role_by_id:
            validation_errors.append(f"Role id '{role_id}' is not defined as a default or custom project role.")

    active_role_ids = sorted(role_id for role_id in requested_active - requested_deferred if role_id in role_by_id)
    deferred_role_ids = sorted(role_id for role_id in requested_deferred if role_id in role_by_id)

    registration_roles = []
    for role_id, role in sorted(role_by_id.items()):
        item = dict(role)
        item["status"] = "active" if role_id in active_role_ids else "deferred"
        registration_roles.append(item)

    return [role_by_id[role_id] for role_id in active_role_ids], deferred_role_ids, registration_roles


def grant_permission(namespace_key, preset):
    if preset == "bootstrap-admin":
        return "admin"

    return {
        "goals": "write",
        "facts": "write",
        "decisions": "review",
        "rationale": "write",
        "risks": "review",
        "release-evidence": "write",
        "role_lens": "write",
    }[namespace_key]


def namespace_grants(project_id, roles, preset):
    shared = [
        {
            "namespace": f"/project/{project_id}/goals",
            "namespaceKey": "goals",
            "memoryTypes": ["goal", "target", "requirement"],
            "recommendedPermission": grant_permission("goals", preset),
            "reviewOwnerRoleId": "product_owner",
            "purpose": "Project goals, target users, outcomes, and acceptance criteria.",
        },
        {
            "namespace": f"/project/{project_id}/facts",
            "namespaceKey": "facts",
            "memoryTypes": ["fact", "requirement", "constraint"],
            "recommendedPermission": grant_permission("facts", preset),
            "reviewOwnerRoleId": "knowledge_steward",
            "purpose": "Stable project facts grounded in committed source evidence.",
        },
        {
            "namespace": f"/project/{project_id}/decisions",
            "namespaceKey": "decisions",
            "memoryTypes": ["decision"],
            "recommendedPermission": grant_permission("decisions", preset),
            "reviewOwnerRoleId": "cto",
            "purpose": "Accepted architecture, platform, release, and product decisions.",
        },
        {
            "namespace": f"/project/{project_id}/rationale",
            "namespaceKey": "rationale",
            "memoryTypes": ["rationale", "assumption"],
            "recommendedPermission": grant_permission("rationale", preset),
            "reviewOwnerRoleId": "product_owner",
            "purpose": "Reasoning behind product and implementation choices.",
        },
        {
            "namespace": f"/project/{project_id}/risks",
            "namespaceKey": "risks",
            "memoryTypes": ["risk", "constraint"],
            "recommendedPermission": grant_permission("risks", preset),
            "reviewOwnerRoleId": "security_professional",
            "purpose": "Security, operational, product, release, and technical risks.",
        },
        {
            "namespace": f"/project/{project_id}/release-evidence",
            "namespaceKey": "release-evidence",
            "memoryTypes": ["release_evidence"],
            "recommendedPermission": grant_permission("release-evidence", preset),
            "reviewOwnerRoleId": "release_manager",
            "purpose": "Payload-safe release, deploy, rollback, backup, benchmark, and go/no-go evidence.",
        },
    ]

    role_lens = [
        {
            "namespace": f"/project/{project_id}/role/{role['roleId']}/lens",
            "namespaceKey": "role_lens",
            "memoryTypes": ["role_lens"],
            "recommendedPermission": grant_permission("role_lens", preset),
            "reviewOwnerRoleId": role["roleId"],
            "purpose": f"Role-specific retrieval guidance for {role['owner']}.",
        }
        for role in roles
    ]

    return shared + role_lens


def owner_assignments(args):
    assignments = [
        {
            "principalLabel": "product_owner",
            "principalId": args.product_owner_principal_id or None,
            "required": True,
            "roleId": "product_owner",
            "projectMembershipLevel": args.day_to_day_access_level,
            "purpose": "Own goals, acceptance criteria, and project registration closeout.",
        },
        {
            "principalLabel": "knowledge_steward",
            "principalId": args.knowledge_steward_principal_id or None,
            "required": True,
            "roleId": "knowledge_steward",
            "projectMembershipLevel": args.day_to_day_access_level,
            "purpose": "Own source-backed seed quality, memory hygiene, and feedback closeout.",
        },
        {
            "principalLabel": "security_ops",
            "principalId": args.security_ops_principal_id or None,
            "required": True,
            "roleId": "security_professional",
            "projectMembershipLevel": args.day_to_day_access_level,
            "purpose": "Own least-privilege grants, access preview, and accepted admin exceptions.",
        },
    ]

    if args.operator_principal_id:
        assignments.append(
            {
                "principalLabel": "operator",
                "principalId": args.operator_principal_id,
                "required": False,
                "roleId": "it_manager",
                "projectMembershipLevel": "reviewer",
                "purpose": "Operate registration and health checks without day-to-day admin by default.",
            }
        )

    if args.break_glass_principal_id:
        assignments.append(
            {
                "principalLabel": "break_glass",
                "principalId": args.break_glass_principal_id,
                "required": False,
                "roleId": "security_professional",
                "projectMembershipLevel": "admin",
                "requiresAcceptedFinding": True,
                "purpose": "Time-bound recovery path only; must include owner, reason, review due, cleanup action, and audit evidence.",
            }
        )

    return assignments


def source_owner_for_path(path):
    if path.endswith("project-goal.md") or path.endswith("roadmap.md") or path.endswith("backlog.md"):
        return "product_owner"
    if path.endswith("architecture.md") or path.endswith("agent-facing-memory-contract.md"):
        return "cto"
    if path.endswith("memory-vs-markdown-policy.md") or path.endswith("project-memory-runbook.md"):
        return "knowledge_steward"
    if path.endswith("project-memory-boundary.md"):
        return "security_professional"
    return "knowledge_steward"


def source_doc_checks(seed_reports):
    documents = [
        {
            "path": report["path"],
            "exists": report["exists"],
            "sha256Present": bool(report.get("sha256")),
            "sourceOwnerRoleId": source_owner_for_path(report["path"]),
            "suggestedExcerptCount": report["suggestedExcerptCount"],
            "errors": report["errors"],
        }
        for report in seed_reports
    ]

    present = [document for document in documents if document["exists"]]
    hashed = [document for document in documents if document["sha256Present"]]
    return {
        "documentCount": len(documents),
        "missingDocumentCount": len(documents) - len(present),
        "sha256CoveragePercent": 100 if not documents else round(len(hashed) * 100 / len(documents)),
        "rawSourcePayloadsIncluded": False,
        "documents": documents,
    }


def registration_validation(args, project_id, organization_id, grants, assignments, seed_reports, role_validation_errors):
    blocking_errors = list(role_validation_errors)

    required_strings = [
        ("organizationName", args.organization_name),
        ("projectName", args.project_name),
        ("projectStatus", args.project_status),
        ("apiBaseUrl", args.api_base_url),
    ]
    for label, value in required_strings:
        if not str(value or "").strip():
            blocking_errors.append(f"{label} is required.")

    for assignment in assignments:
        principal_id = assignment.get("principalId")
        if assignment["required"] and not principal_id:
            blocking_errors.append(f"{assignment['principalLabel']} principal id is required.")
        elif principal_id and not valid_principal_id(principal_id):
            blocking_errors.append(f"{assignment['principalLabel']} principal id must be a UUID.")

    for grant in grants:
        namespace = grant["namespace"]
        if namespace in root_namespace_prefixes:
            blocking_errors.append(f"Root namespace grant is forbidden for registration: {namespace}.")

    admin_grants = [grant for grant in grants if grant.get("recommendedPermission") == "admin"]
    if admin_grants:
        for label, value in (
            ("adminAcceptedFindingOwner", args.admin_accepted_finding_owner),
            ("adminAcceptedReason", args.admin_accepted_reason),
            ("adminCleanupAction", args.admin_cleanup_action),
            ("adminReviewDue", args.admin_review_due),
            ("adminAuditEvidenceId", args.admin_audit_evidence_id),
        ):
            if not str(value or "").strip():
                blocking_errors.append(f"{label} is required when admin grants are requested.")

    if any(report["errors"] for report in seed_reports):
        blocking_errors.append("Every seed document must exist before registration closeout.")

    return {
        "status": "ready" if not blocking_errors else "needs_input",
        "blockingValidationErrors": blocking_errors,
        "validationRules": [
            "organization_and_project_ids_are_stable",
            "owner_principal_ids_are_required",
            "role_ids_match_lowercase_project_role_pattern",
            "root_namespace_grants_are_forbidden",
            "least_privilege_grant_preset_is_default",
            "admin_grants_require_owner_reason_review_due_cleanup_and_audit_evidence",
            "seed_documents_must_exist_and_have_sha256",
            "effective_access_preview_is_required_before_commit",
            "registration_closeout_requires_context_feedback",
        ],
    }


def registration_contract(args, project_id, organization_id, grants, assignments, seed_reports, validation):
    namespace_groups = [
        {
            "namespace": grant["namespace"],
            "permission": grant["recommendedPermission"],
            "reviewOwnerRoleId": grant["reviewOwnerRoleId"],
        }
        for grant in grants
    ]

    return {
        "contractId": "REG-01",
        "status": validation["status"],
        "targetDate": "2026-06-12",
        "apiContractPreview": {
            "plannedEndpoint": "POST /api/admin/projects/register",
            "implementedIn": "REG-02",
            "idempotencyKeyPattern": "project-registration:{projectId}:{sourceContentSha256}",
        },
        "requiredFields": [
            "organizationId",
            "organizationName",
            "projectId",
            "projectName",
            "projectStatus",
            "apiBaseUrl",
            "productOwnerPrincipalId",
            "knowledgeStewardPrincipalId",
            "securityOpsPrincipalId",
            "activeRoles",
            "deferredRoles",
            "customRoleDefinitions",
            "namespaceGrantPreset",
            "seedDocuments",
            "sourceOwners",
            "reviewCadence",
        ],
        "grantPresets": [
            {
                "id": "least_privilege_default",
                "default": args.namespace_grant_preset == "least-privilege",
                "description": "Day-to-day registration uses read/write/review grants by namespace owner; no root namespace admin grants.",
            },
            {
                "id": "bootstrap_admin_exception",
                "default": args.namespace_grant_preset == "bootstrap-admin",
                "description": "Admin grants are time-bound exceptions requiring owner, reason, review due date, cleanup action, and audit evidence.",
            },
        ],
        "ownerAssignments": assignments,
        "namespaceGrantMatrix": namespace_groups,
        "sourceDocChecks": source_doc_checks(seed_reports),
        "effectiveAccessPreviewPlan": {
            "endpoint": "/api/admin/access/effective-preview",
            "permissionDriftEndpoint": "/api/admin/access/permission-drift",
            "matrixRows": [
                {
                    "principalLabel": assignment["principalLabel"],
                    "principalId": assignment.get("principalId"),
                    "scopeType": "project",
                    "scopeId": project_id,
                    "projectMembershipLevel": assignment["projectMembershipLevel"],
                    "previewNamespaces": [grant["namespace"] for grant in grants[:6]],
                }
                for assignment in assignments
            ],
        },
        "auditEvidencePlan": {
            "endpoint": "/api/admin/audit-exports",
            "requiredEvidence": [
                "actorPrincipalId",
                "targetScope",
                "registrationRequestHash",
                "accessPreviewReportId",
                "acceptedFindingRuleIdForAnyAdminGrant",
                "adminAuditEvidenceIdForAnyAdminGrant",
                "registrationCloseoutTimestamp",
            ],
        },
        "plannedOperations": [
            "upsert_organization",
            "upsert_project",
            "upsert_project_role_definitions",
            "upsert_owner_memberships",
            "upsert_namespace_grants_from_preset",
            "append_source_evidence_for_seed_docs",
            "propose_seed_memory",
            "run_effective_access_preview",
            "run_access_boundary_review",
            "record_context_feedback",
            "attach_audit_export",
        ],
        "preflightChecklist": [
            "scope_ids_valid",
            "owner_principals_present",
            "roles_defined_or_deferred",
            "least_privilege_grants_selected",
            "seed_docs_hashed",
            "effective_access_preview_planned",
            "audit_evidence_planned",
            "closeout_feedback_planned",
        ],
        "closeoutCriteria": [
            "registrationValidation.status is ready",
            "sourceDocChecks.sha256CoveragePercent is 100",
            "effective access preview is attached",
            "access-boundary review has zero unaccepted high-severity findings",
            "one project context retrieval check succeeds",
            "memory context feedback is recorded",
            "audit export id is attached to the registration record",
        ],
    }


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
    role_validation_errors = []
    active_roles, deferred_role_ids, registration_roles = resolve_roles(args, role_validation_errors)
    seed_docs = args.seed_doc if args.seed_doc else default_seed_docs
    seed_reports = [seed_document_report(path) for path in seed_docs]
    errors = [error for report in seed_reports for error in report["errors"]]
    grants = namespace_grants(project_id, active_roles, args.namespace_grant_preset)
    assignments = owner_assignments(args)
    validation = registration_validation(
        args,
        project_id,
        organization_id,
        grants,
        assignments,
        seed_reports,
        role_validation_errors,
    )

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
            "projectStatus": args.project_status,
            "apiBaseUrl": args.api_base_url,
        },
        "registrationContract": registration_contract(
            args,
            project_id,
            organization_id,
            grants,
            assignments,
            seed_reports,
            validation,
        ),
        "registrationValidation": validation,
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
        "roleDefinitions": active_roles,
        "registrationRoles": registration_roles,
        "deferredRoleIds": deferred_role_ids,
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
    parser.add_argument("--project-status", choices=["planned", "active"], default=os.environ.get("MEMORYSYSTEM_ONBOARDING_PROJECT_STATUS", "active"))
    parser.add_argument("--api-base-url", default=os.environ.get("MEMORYSYSTEM_ONBOARDING_API_BASE_URL", "http://127.0.0.1:8081"))
    parser.add_argument("--product-owner-principal-id", default=os.environ.get("MEMORYSYSTEM_REGISTRATION_PRODUCT_OWNER_PRINCIPAL_ID", ""))
    parser.add_argument("--knowledge-steward-principal-id", default=os.environ.get("MEMORYSYSTEM_REGISTRATION_KNOWLEDGE_STEWARD_PRINCIPAL_ID", ""))
    parser.add_argument("--security-ops-principal-id", default=os.environ.get("MEMORYSYSTEM_REGISTRATION_SECURITY_OPS_PRINCIPAL_ID", ""))
    parser.add_argument("--operator-principal-id", default=os.environ.get("MEMORYSYSTEM_REGISTRATION_OPERATOR_PRINCIPAL_ID", ""))
    parser.add_argument("--break-glass-principal-id", default=os.environ.get("MEMORYSYSTEM_REGISTRATION_BREAK_GLASS_PRINCIPAL_ID", ""))
    parser.add_argument("--active-role", action="append", help="Active project role id. Repeat to override the default active roles.")
    parser.add_argument("--deferred-role", action="append", help="Deferred project role id. Repeat for roles not active at registration.")
    parser.add_argument("--custom-role", action="append", help="Custom project role as role_id=Display Name. Repeat for multiple roles.")
    parser.add_argument("--day-to-day-access-level", choices=["reader", "contributor", "reviewer"], default="reviewer")
    parser.add_argument("--namespace-grant-preset", choices=["least-privilege", "bootstrap-admin"], default="least-privilege")
    parser.add_argument("--admin-accepted-finding-owner", default=os.environ.get("MEMORYSYSTEM_REGISTRATION_ADMIN_ACCEPTED_FINDING_OWNER", ""))
    parser.add_argument("--admin-accepted-reason", default=os.environ.get("MEMORYSYSTEM_REGISTRATION_ADMIN_ACCEPTED_REASON", ""))
    parser.add_argument("--admin-review-due", default=os.environ.get("MEMORYSYSTEM_REGISTRATION_ADMIN_REVIEW_DUE", ""))
    parser.add_argument("--admin-cleanup-action", default=os.environ.get("MEMORYSYSTEM_REGISTRATION_ADMIN_CLEANUP_ACTION", ""))
    parser.add_argument("--admin-audit-evidence-id", default=os.environ.get("MEMORYSYSTEM_REGISTRATION_ADMIN_AUDIT_EVIDENCE_ID", ""))
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
