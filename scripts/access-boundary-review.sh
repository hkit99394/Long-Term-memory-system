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
from datetime import date, datetime, timezone
from pathlib import Path

repo_root = Path(sys.argv[1])
env_file = Path(sys.argv[2])
argv = sys.argv[3:]

default_org_id = "9f8e7d6c-5b4a-4321-9123-abcdef123001"
default_project_id = "9f8e7d6c-5b4a-4321-9123-abcdef123002"
default_accepted_findings_path = "docs/access-boundary-accepted-findings.json"


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


def safe_findings(findings_or_report, limit):
    if isinstance(findings_or_report, dict):
        findings = findings_or_report.get("findings") or []
    else:
        findings = findings_or_report or []

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
                "acceptedFindingRuleId": finding.get("acceptedFindingRuleId"),
                "acceptedOwnerRole": finding.get("acceptedOwnerRole"),
                "reviewDue": finding.get("reviewDue"),
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


def relative_path(path):
    try:
        return str(path.relative_to(repo_root))
    except ValueError:
        return str(path)


def as_list(value):
    if value is None:
        return []
    if isinstance(value, list):
        return [str(item) for item in value]
    return [str(value)]


def parse_review_due(value, rule_id):
    if not value:
        raise WorkflowError(f"Accepted finding rule {rule_id} is missing reviewDue.")

    text = str(value)
    try:
        if "T" in text:
            return datetime.fromisoformat(text.replace("Z", "+00:00")).date()
        return date.fromisoformat(text)
    except ValueError as exc:
        raise WorkflowError(f"Accepted finding rule {rule_id} has invalid reviewDue: {text}") from exc


def load_accepted_findings(path_text):
    path = Path(path_text)
    if not path.is_absolute():
        path = repo_root / path

    source = {
        "path": relative_path(path),
        "exists": path.exists(),
        "schemaVersion": None,
        "ruleCount": 0,
        "activeRuleCount": 0,
        "expiredRuleIds": [],
    }

    if not path.exists():
        return {"source": source, "rules": []}

    try:
        document = json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        raise WorkflowError(f"Accepted findings file is not valid JSON: {relative_path(path)}") from exc

    if document.get("kind") != "memorysystem.access_boundary_accepted_findings":
        raise WorkflowError("Accepted findings file has the wrong kind.")
    if document.get("schemaVersion") != 1:
        raise WorkflowError("Accepted findings file must use schemaVersion 1.")
    if document.get("payloadSafe") is not True or document.get("rawSourcePayloadsIncluded") is not False:
        raise WorkflowError("Accepted findings file must be payload-safe and omit raw source payloads.")

    rules = document.get("rules")
    if not isinstance(rules, list):
        raise WorkflowError("Accepted findings file must contain a rules array.")

    today = datetime.now(timezone.utc).date()
    active_rules = []
    expired_rule_ids = []
    source["schemaVersion"] = document.get("schemaVersion")
    source["ruleCount"] = len(rules)

    for rule in rules:
        if not isinstance(rule, dict):
            raise WorkflowError("Every accepted finding rule must be an object.")

        rule_id = str(rule.get("id") or "")
        if not rule_id:
            raise WorkflowError("Every accepted finding rule must include an id.")

        for required in ("ownerRole", "acceptedByRole", "acceptedReason", "cleanupAction"):
            if not str(rule.get(required) or "").strip():
                raise WorkflowError(f"Accepted finding rule {rule_id} is missing {required}.")

        review_due = parse_review_due(rule.get("reviewDue"), rule_id)
        if review_due < today:
            expired_rule_ids.append(rule_id)
            continue

        if str(rule.get("status") or "active") != "active":
            continue

        active_rules.append(rule)

    source["activeRuleCount"] = len(active_rules)
    source["expiredRuleIds"] = expired_rule_ids
    return {"source": source, "rules": active_rules}


def namespace_matches(candidate, allowed_prefix):
    if not candidate:
        return False

    allowed = allowed_prefix.rstrip("/")
    return candidate == allowed or candidate.startswith(allowed + "/")


def field_matches(finding, field_name, allowed_values):
    if not allowed_values:
        return True

    value = finding.get(field_name)
    if value is None:
        return False

    return str(value) in allowed_values


def finding_matches_rule(finding, rule):
    if not field_matches(finding, "code", as_list(rule.get("findingCodes"))):
        return False
    if not field_matches(finding, "severity", as_list(rule.get("severities"))):
        return False
    if not field_matches(finding, "resourceType", as_list(rule.get("resourceTypes"))):
        return False
    if not field_matches(finding, "principalId", as_list(rule.get("principalIds"))):
        return False
    if not field_matches(finding, "scopeType", as_list(rule.get("scopeTypes"))):
        return False
    if not field_matches(finding, "scopeId", as_list(rule.get("scopeIds"))):
        return False

    namespace_prefixes = as_list(rule.get("namespacePrefixes"))
    if namespace_prefixes:
        finding_namespace = finding.get("namespacePrefix")
        if not any(namespace_matches(finding_namespace, prefix) for prefix in namespace_prefixes):
            return False

    return True


def classify_findings(findings, accepted_findings):
    accepted = []
    unaccepted = []

    for finding in findings:
        matching_rule = next(
            (rule for rule in accepted_findings["rules"] if finding_matches_rule(finding, rule)),
            None,
        )
        if matching_rule is None:
            unaccepted.append(finding)
            continue

        enriched = dict(finding)
        enriched["acceptedFindingRuleId"] = matching_rule["id"]
        enriched["acceptedOwnerRole"] = matching_rule.get("ownerRole")
        enriched["reviewDue"] = matching_rule.get("reviewDue")
        accepted.append(enriched)

    return accepted, unaccepted


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


def summarize(report, args, accepted_findings):
    findings = report.get("findings") or []
    accepted, unaccepted = classify_findings(findings, accepted_findings)
    severity_counts = Counter(finding.get("severity") for finding in findings if finding.get("severity"))
    code_counts = Counter(finding.get("code") for finding in findings if finding.get("code"))
    accepted_severity_counts = Counter(finding.get("severity") for finding in accepted if finding.get("severity"))
    accepted_code_counts = Counter(finding.get("code") for finding in accepted if finding.get("code"))
    unaccepted_severity_counts = Counter(finding.get("severity") for finding in unaccepted if finding.get("severity"))
    unaccepted_code_counts = Counter(finding.get("code") for finding in unaccepted if finding.get("code"))

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
        "findings": len(unaccepted),
        "rawPermissionDriftFindings": len(findings),
        "acceptedFindings": len(accepted),
        "unacceptedFindings": len(unaccepted),
        "highSeverityFindings": unaccepted_severity_counts.get("high", 0),
        "rawHighSeverityFindings": severity_counts.get("high", 0),
        "acceptedHighSeverityFindings": accepted_severity_counts.get("high", 0),
        "unacceptedHighSeverityFindings": unaccepted_severity_counts.get("high", 0),
        "breakGlassManualChecks": len(break_glass_checks()),
    }

    return {
        "status": "needs_review" if unaccepted else "clear",
        "generatedAt": utc_now(),
        "payloadSafe": True,
        "rawSourcePayloadsIncluded": False,
        "targetScopeType": args.scope_type,
        "targetScopeId": args.scope_id,
        "namespacePrefix": args.namespace_prefix or default_namespace_prefix(args.scope_type, args.scope_id),
        "acceptedFindingSource": accepted_findings["source"],
        "acceptedFindings": len(accepted),
        "unacceptedFindings": len(unaccepted),
        "permissionDriftReport": {
            "reportId": report.get("reportId"),
            "generatedAt": report.get("generatedAt"),
            "staleAfterDays": report.get("staleAfterDays"),
            "counts": queue_counts,
            "rawFindingsBySeverity": dict(sorted(severity_counts.items())),
            "rawFindingsByCode": dict(sorted(code_counts.items())),
            "acceptedFindingsBySeverity": dict(sorted(accepted_severity_counts.items())),
            "acceptedFindingsByCode": dict(sorted(accepted_code_counts.items())),
            "findingsBySeverity": dict(sorted(unaccepted_severity_counts.items())),
            "findingsByCode": dict(sorted(unaccepted_code_counts.items())),
            "topFindings": safe_findings(unaccepted, args.finding_limit),
            "topAcceptedFindings": safe_findings(accepted, args.finding_limit),
        },
        "reviewSections": review_sections(report),
        "queueCounts": queue_counts,
        "nextActions": [
            "Open /admin/ and review the permission-drift report findings.",
            "Confirm memberships, role assignments, and namespace grants with the narrowest practical access.",
            "Confirm service-account owners, review dates, expiry dates, credential rotation, and least-privilege grants.",
            "Confirm OIDC identity bindings still map to active human principals and do not grant memory access from token claims.",
            "Review break-glass key owner, scope, last use, audit export, and rotation/removal evidence.",
            "Review accepted access findings before their reviewDue date and complete the recorded cleanup action.",
            "Attach an audit export for access-management changes or break-glass activation windows.",
        ],
    }


def dry_run(args, accepted_findings):
    request = report_request(args)
    return {
        "status": "dry_run",
        "payloadSafe": True,
        "rawSourcePayloadsIncluded": False,
        "targetScopeType": args.scope_type,
        "targetScopeId": args.scope_id,
        "namespacePrefix": request["namespacePrefix"],
        "acceptedFindingSource": accepted_findings["source"],
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
            "accepted_finding_owner_review",
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
            "acceptedFindings",
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
    parser.add_argument(
        "--accepted-findings",
        default=os.environ.get("MEMORYSYSTEM_ACCESS_BOUNDARY_ACCEPTED_FINDINGS", default_accepted_findings_path),
        help="Payload-safe accepted finding rules with owner, reason, cleanup action, and review due date.",
    )
    parser.add_argument("--dry-run", action="store_true", help="Print planned checks without API calls.")
    return parser


try:
    parser = build_parser()
    parsed = parser.parse_args(argv)
    if parsed.scope_type == "org" and parsed.scope_id == default_project_id:
        parsed.scope_id = os.environ.get("MEMORYSYSTEM_CANONICAL_ORG_ID", default_org_id)

    accepted_findings = load_accepted_findings(parsed.accepted_findings)
    payload = dry_run(parsed, accepted_findings) if parsed.dry_run else summarize(
        request_json("POST", "/api/admin/access/permission-drift", report_request(parsed)),
        parsed,
        accepted_findings,
    )
    print(json.dumps(payload, indent=2, sort_keys=True))
except WorkflowError as exc:
    print(str(exc), file=sys.stderr)
    sys.exit(2)
PY
