#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENV_FILE="${MEMORYSYSTEM_ACCESS_BOUNDARY_ENV_FILE:-$ROOT_DIR/.env.production}"

python3 - "$ROOT_DIR" "$ENV_FILE" "$@" <<'PY'
import argparse
import json
import os
import sys
import urllib.error
import urllib.request
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path

repo_root = Path(sys.argv[1])
env_file = Path(sys.argv[2])
argv = sys.argv[3:]

default_org_id = "9f8e7d6c-5b4a-4321-9123-abcdef123001"
default_project_id = "9f8e7d6c-5b4a-4321-9123-abcdef123002"


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


def request_json(method, path, body):
    data = json.dumps(body, separators=(",", ":"), sort_keys=True).encode("utf-8")
    request = urllib.request.Request(
        effective_base_url() + path,
        data=data,
        headers={
            "X-Api-Key": require_api_key(),
            "Content-Type": "application/json",
        },
        method=method,
    )
    try:
        with urllib.request.urlopen(request, timeout=30) as response:
            payload = response.read().decode("utf-8")
            return json.loads(payload) if payload else {}
    except urllib.error.HTTPError as exc:
        payload = exc.read().decode("utf-8", errors="replace")
        raise WorkflowError(f"{method} {path} failed with HTTP {exc.code}: {payload}") from exc
    except urllib.error.URLError as exc:
        raise WorkflowError(f"{method} {path} failed: {exc.reason}") from exc


def default_namespace_prefix(scope_type, scope_id):
    return f"/org/{scope_id}" if scope_type == "org" else f"/project/{scope_id}"


def report_request(args):
    return {
        "scopeType": args.scope_type,
        "scopeId": args.scope_id,
        "namespacePrefix": args.namespace_prefix or default_namespace_prefix(args.scope_type, args.scope_id),
        "staleAfterDays": args.stale_after_days,
        "maxPreviewPrincipals": args.max_preview_principals,
    }


def count_by(rows, field_name):
    return dict(sorted(Counter(row.get(field_name) for row in rows if row.get(field_name)).items()))


def safe_findings(report, limit):
    findings = report.get("findings") or []
    safe = []
    for finding in findings[:limit]:
        safe.append(
            {
                "severity": finding.get("severity"),
                "code": finding.get("code"),
                "resourceType": finding.get("resourceType"),
                "resourceId": finding.get("resourceId"),
                "principalId": finding.get("principalId"),
                "scopeType": finding.get("scopeType"),
                "scopeId": finding.get("scopeId"),
                "namespacePrefix": finding.get("namespacePrefix"),
                "detail": finding.get("detail"),
                "recommendedAction": finding.get("recommendedAction"),
            }
        )
    return safe


def break_glass_checks():
    return [
        {
            "id": "break_glass_key_owner",
            "source": "secret-store configuration and access audit export",
            "requiredEvidence": "key id, owner principal id, owner role, activation reason, review window, and audit export id",
            "suggestedAction": "Confirm every break-glass key maps to one active human principal and a named owner.",
        },
        {
            "id": "break_glass_key_scope",
            "source": "permission-drift report plus secret-store configuration",
            "requiredEvidence": "operator membership/grant ids and effective-admin preview result",
            "suggestedAction": "Confirm the break-glass principal has only the recovery memberships and namespace grants it needs.",
        },
        {
            "id": "break_glass_key_rotation",
            "source": "change ticket or incident record",
            "requiredEvidence": "last use timestamp, rotation/removal decision, and next review due date",
            "suggestedAction": "Rotate or remove the key after every break-glass activation and before stale review windows close.",
        },
    ]


def review_sections(report):
    identity_bindings = report.get("identityBindings") or []
    service_accounts = report.get("serviceAccounts") or []
    service_credentials = report.get("serviceCredentials") or []
    organization_memberships = report.get("organizationMemberships") or []
    project_memberships = report.get("projectMemberships") or []
    role_assignments = report.get("roleAssignments") or []
    namespace_grants = report.get("namespaceGrants") or []
    findings = report.get("findings") or []

    finding_codes = Counter(finding.get("code") for finding in findings if finding.get("code"))
    oidc_binding_count = sum(1 for binding in identity_bindings if binding.get("provider") == "oidc")

    return {
        "memberships": {
            "source": "organizationMemberships and projectMemberships from /api/admin/access/permission-drift",
            "organizationMembershipCount": len(organization_memberships),
            "projectMembershipCount": len(project_memberships),
            "accessLevels": {
                "organization": count_by(organization_memberships, "accessLevel"),
                "project": count_by(project_memberships, "accessLevel"),
            },
            "findingCodes": {
                code: finding_codes.get(code, 0)
                for code in ("over_broad_org_membership", "over_broad_project_membership", "inactive_principal_has_access")
            },
            "suggestedActions": ["confirm_admin_owner_need", "remove_inactive_principal_access", "record_owner_decision"],
        },
        "roleAssignments": {
            "source": "roleAssignments from /api/admin/access/permission-drift",
            "count": len(role_assignments),
            "scopeTypes": count_by(role_assignments, "scopeType"),
            "findingCodes": {"global_role_assignment": finding_codes.get("global_role_assignment", 0)},
            "suggestedActions": ["prefer_project_or_org_roles", "confirm_role_namespace_grants"],
        },
        "namespaceGrants": {
            "source": "namespaceGrants and effectiveAccessPreviews from /api/admin/access/permission-drift",
            "count": len(namespace_grants),
            "permissions": count_by(namespace_grants, "permission"),
            "findingCodes": {
                code: finding_codes.get(code, 0)
                for code in ("over_broad_namespace_admin_grant", "broad_namespace_prefix", "effective_admin_access")
            },
            "suggestedActions": ["narrow_admin_grants", "confirm_effective_admin_access", "prefer_specific_namespace_prefixes"],
        },
        "serviceAccounts": {
            "source": "serviceAccounts and serviceCredentials from /api/admin/access/permission-drift",
            "serviceAccountCount": len(service_accounts),
            "serviceCredentialCount": len(service_credentials),
            "accountStatuses": count_by(service_accounts, "status"),
            "credentialStatuses": count_by(service_credentials, "status"),
            "findingCodes": {
                code: finding_codes.get(code, 0)
                for code in (
                    "inactive_service_account",
                    "service_account_review_due",
                    "service_account_expired",
                    "inactive_service_credential",
                    "service_credential_review_due",
                    "service_credential_expired",
                    "stale_service_credential",
                )
            },
            "suggestedActions": ["confirm_owner_and_review_due", "rotate_or_disable_stale_credentials", "verify_least_privilege_grants"],
        },
        "oidcIdentityBindings": {
            "source": "identityBindings from /api/admin/access/permission-drift and OIDC login audit evidence",
            "identityBindingCount": len(identity_bindings),
            "oidcBindingCount": oidc_binding_count,
            "providers": count_by(identity_bindings, "provider"),
            "statuses": count_by(identity_bindings, "status"),
            "findingCodes": {
                code: finding_codes.get(code, 0)
                for code in ("inactive_identity_binding", "stale_identity_binding")
            },
            "suggestedActions": ["confirm_bound_human_principal", "disable_stale_bindings", "verify_token_claims_do_not_grant_access"],
        },
        "breakGlassKeys": {
            "source": "managed secret-store configuration plus console break-glass audit export",
            "manualCheckCount": len(break_glass_checks()),
            "checks": break_glass_checks(),
            "suggestedActions": ["confirm_human_owner", "verify_narrow_recovery_grants", "rotate_after_use"],
        },
    }


def summarize(report, args):
    findings = report.get("findings") or []
    severity_counts = Counter(finding.get("severity") for finding in findings if finding.get("severity"))
    code_counts = Counter(finding.get("code") for finding in findings if finding.get("code"))

    queue_counts = {
        "principals": len(report.get("principals") or []),
        "identityBindings": len(report.get("identityBindings") or []),
        "serviceAccounts": len(report.get("serviceAccounts") or []),
        "serviceCredentials": len(report.get("serviceCredentials") or []),
        "organizationMemberships": len(report.get("organizationMemberships") or []),
        "projectMemberships": len(report.get("projectMemberships") or []),
        "roleAssignments": len(report.get("roleAssignments") or []),
        "namespaceGrants": len(report.get("namespaceGrants") or []),
        "effectiveAccessPreviews": len(report.get("effectiveAccessPreviews") or []),
        "findings": len(findings),
        "highSeverityFindings": severity_counts.get("high", 0),
        "breakGlassManualChecks": len(break_glass_checks()),
    }

    return {
        "status": "needs_review" if findings else "clear",
        "generatedAt": utc_now(),
        "payloadSafe": True,
        "rawSourcePayloadsIncluded": False,
        "targetScopeType": args.scope_type,
        "targetScopeId": args.scope_id,
        "namespacePrefix": args.namespace_prefix or default_namespace_prefix(args.scope_type, args.scope_id),
        "permissionDriftReport": {
            "reportId": report.get("reportId"),
            "generatedAt": report.get("generatedAt"),
            "staleAfterDays": report.get("staleAfterDays"),
            "counts": queue_counts,
            "findingsBySeverity": dict(sorted(severity_counts.items())),
            "findingsByCode": dict(sorted(code_counts.items())),
            "topFindings": safe_findings(report, args.finding_limit),
        },
        "reviewSections": review_sections(report),
        "queueCounts": queue_counts,
        "nextActions": [
            "Open /admin/ and review the permission-drift report findings.",
            "Confirm memberships, role assignments, and namespace grants with the narrowest practical access.",
            "Confirm service-account owners, review dates, expiry dates, credential rotation, and least-privilege grants.",
            "Confirm OIDC identity bindings still map to active human principals and do not grant memory access from token claims.",
            "Review break-glass key owner, scope, last use, audit export, and rotation/removal evidence.",
            "Attach an audit export for access-management changes or break-glass activation windows.",
        ],
    }


def dry_run(args):
    request = report_request(args)
    return {
        "status": "dry_run",
        "payloadSafe": True,
        "rawSourcePayloadsIncluded": False,
        "targetScopeType": args.scope_type,
        "targetScopeId": args.scope_id,
        "namespacePrefix": request["namespacePrefix"],
        "permissionDriftRequest": {
            "method": "POST",
            "endpoint": "/api/admin/access/permission-drift",
            "body": request,
        },
        "checks": [
            "membership_audit",
            "role_assignment_audit",
            "namespace_grant_audit",
            "service_account_owner_review",
            "service_credential_rotation_review",
            "oidc_identity_binding_review",
            "break_glass_key_review",
            "permission_drift_findings",
            "audit_export_evidence",
        ],
        "endpoints": [
            "/api/admin/access/permission-drift",
            "/api/admin/access/effective-preview",
            "/api/admin/audit-exports",
            "/api/auth/console/oidc-token",
            "/api/auth/console/break-glass-key",
            "/admin/",
        ],
        "manualChecks": break_glass_checks(),
        "reportSections": [
            "principals",
            "identityBindings",
            "serviceAccounts",
            "serviceCredentials",
            "organizationMemberships",
            "projectMemberships",
            "roleAssignments",
            "namespaceGrants",
            "effectiveAccessPreviews",
            "findings",
        ],
    }


def build_parser():
    parser = argparse.ArgumentParser(
        prog="access-boundary-review.sh",
        description="Collect a payload-safe access boundary review queue.",
    )
    parser.add_argument("--scope-type", choices=["org", "project"], default="project", help="Scope type to review.")
    parser.add_argument(
        "--scope-id",
        default=os.environ.get("MEMORYSYSTEM_CANONICAL_PROJECT_ID", default_project_id),
        help="Organization or project id to review.",
    )
    parser.add_argument("--namespace-prefix", help="Namespace prefix for permission-drift review.")
    parser.add_argument("--stale-after-days", type=int, default=90, help="Stale identity/credential window.")
    parser.add_argument("--max-preview-principals", type=int, default=20, help="Effective-access preview principal cap.")
    parser.add_argument("--finding-limit", type=int, default=25, help="Maximum finding details to include.")
    parser.add_argument("--dry-run", action="store_true", help="Print planned checks without API calls.")
    return parser


try:
    parser = build_parser()
    parsed = parser.parse_args(argv)
    if parsed.scope_type == "org" and parsed.scope_id == default_project_id:
        parsed.scope_id = os.environ.get("MEMORYSYSTEM_CANONICAL_ORG_ID", default_org_id)

    payload = dry_run(parsed) if parsed.dry_run else summarize(
        request_json("POST", "/api/admin/access/permission-drift", report_request(parsed)),
        parsed,
    )
    print(json.dumps(payload, indent=2, sort_keys=True))
except WorkflowError as exc:
    print(str(exc), file=sys.stderr)
    sys.exit(2)
PY
