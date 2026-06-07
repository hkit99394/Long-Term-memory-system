#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

python3 - "$ROOT_DIR" "$@" <<'PY'
import argparse
import json
import sys
from pathlib import Path

repo_root = Path(sys.argv[1])
argv = sys.argv[2:]

project_id = "9f8e7d6c-5b4a-4321-9123-abcdef123002"

source_documents = {
    "productPlan": "docs/product-improvement-plan.md",
    "enterpriseGate": "docs/enterprise-access-gate.md",
    "ea10Evaluation": "docs/enterprise-directory-sync-evaluation-ea10.md",
    "decision0047": "docs/decisions/0047-directory-sync-provisioning-only.md",
    "accessBoundaryReview": "docs/access-boundary-review-ip11.md",
    "projectRoles": "docs/project-defined-roles-ip05.md",
}

required_fragments = {
    "human_login_stable": ("productPlan", "| IP-02 | P0 | Done | Security Professional + Developer | Admin Human Login For Console |"),
    "local_project_roles_stable": ("productPlan", "| IP-05 | P0 | Done | Product Owner + CTO | Project-Defined Roles |"),
    "access_boundary_review_stable": ("productPlan", "| IP-11 | P1 | Done | Security Professional | Access Boundary Review |"),
    "ea10_complete": ("enterpriseGate", "| EA-10 | P1 | Done | Evaluate directory sync."),
    "decision_accepted": ("decision0047", "Status: Accepted"),
    "provisioning_only": ("ea10Evaluation", "must be provisioning-only"),
    "no_runtime_grants": ("decision0047", "directly grant memory read, write, review, or admin access at request time"),
    "local_authorizer_source": ("ea10Evaluation", "IMemoryAccessAuthorizer"),
}

thresholds = {
    "activeHumanUsers": 25,
    "operatorMinutesPerWeek": 30,
    "manualProvisioningErrors": 1,
    "serviceAccounts": 2,
}


class EvaluationError(RuntimeError):
    pass


def load_documents():
    loaded = {}
    for key, relative_path in source_documents.items():
        path = repo_root / relative_path
        if not path.exists():
            loaded[key] = None
            continue
        loaded[key] = path.read_text(encoding="utf-8")
    return loaded


def policy_checks(documents):
    checks = []
    for check_id, (document_key, fragment) in required_fragments.items():
        text = documents.get(document_key)
        checks.append(
            {
                "id": check_id,
                "source": source_documents[document_key],
                "present": bool(text and fragment in text),
            }
        )
    return checks


def signal_checks(args):
    weekly_or_faster = args.group_churn in {"weekly", "daily", "continuous"}
    return [
        {
            "id": "user_volume",
            "observed": args.active_human_users,
            "threshold": f">{thresholds['activeHumanUsers']} active human users",
            "reconsider": args.active_human_users > thresholds["activeHumanUsers"],
        },
        {
            "id": "group_churn",
            "observed": args.group_churn,
            "threshold": "weekly or faster",
            "reconsider": weekly_or_faster,
        },
        {
            "id": "operator_load",
            "observed": args.operator_minutes_per_week,
            "threshold": f">{thresholds['operatorMinutesPerWeek']} minutes per week",
            "reconsider": args.operator_minutes_per_week > thresholds["operatorMinutesPerWeek"],
        },
        {
            "id": "manual_error_rate",
            "observed": args.manual_provisioning_errors,
            "threshold": "one or more repeated provisioning mistakes",
            "reconsider": args.manual_provisioning_errors >= thresholds["manualProvisioningErrors"],
        },
        {
            "id": "compliance_demand",
            "observed": args.compliance_demand,
            "threshold": "customer requires directory-sourced provisioning evidence",
            "reconsider": args.compliance_demand,
        },
        {
            "id": "service_scale",
            "observed": args.service_accounts,
            "threshold": f">={thresholds['serviceAccounts']} service accounts needing owner, expiry, and credential review automation",
            "reconsider": args.service_accounts >= thresholds["serviceAccounts"],
        },
    ]


def build_report(args):
    documents = load_documents()
    checks = policy_checks(documents)
    missing_checks = [check for check in checks if not check["present"]]
    signals = signal_checks(args)
    triggered_signals = [signal for signal in signals if signal["reconsider"]]

    if missing_checks:
        recommendation = "repair_policy_sources_before_evaluating_sync"
    elif triggered_signals:
        recommendation = "reconsider_provisioning_only_directory_sync"
    else:
        recommendation = "defer_directory_sync"

    return {
        "status": "dry_run" if args.dry_run else "evaluated",
        "payloadSafe": True,
        "rawDirectoryPayloadsIncluded": False,
        "rawSourcePayloadsIncluded": False,
        "targetScopeType": "project",
        "targetScopeId": project_id,
        "recommendation": recommendation,
        "authorizedSourceOfAccess": "local memberships, role assignments, namespace grants, effective-access previews, and audit records",
        "directorySyncBoundary": "provisioning_only",
        "policyChecks": checks,
        "pilotSignals": signals,
        "triggeredSignalIds": [signal["id"] for signal in triggered_signals],
        "allowedFutureSyncBehavior": [
            "create or update local principals and identity bindings",
            "stage proposed organization memberships",
            "stage proposed project memberships",
            "stage proposed role assignments",
            "stage proposed namespace grants",
            "produce dry-run output before applying changes",
            "write access audit events for every applied change",
        ],
        "disallowedFutureSyncBehavior": [
            "authorize memory reads or writes from token group claims at request time",
            "grant read, write, review, or admin access directly from provider groups",
            "create broad namespace grants without explicit local mapping and approval",
            "delete memory, source events, audit events, reviews, or vault exports",
            "bypass admin effective-access preview semantics",
            "skip audit records for access-affecting changes",
        ],
        "requiredEvidenceBeforeImplementation": [
            "pilot user count",
            "group churn cadence",
            "operator load in minutes per week",
            "manual provisioning mistake count",
            "customer compliance demand",
            "service account scale",
            "access-boundary review showing local grants remain stable",
        ],
        "commands": {
            "dryRun": "scripts/directory-sync-evaluation.sh --dry-run",
            "withPilotSignals": "scripts/directory-sync-evaluation.sh --dry-run --active-human-users 30 --group-churn weekly --operator-minutes-per-week 45",
            "accessBoundaryReview": f"scripts/access-boundary-review.sh --scope-type project --scope-id {project_id}",
        },
        "sourceDocuments": source_documents,
        "errors": [
            f"Missing required policy fragment for {check['id']} in {check['source']}"
            for check in missing_checks
        ],
    }


def build_parser():
    parser = argparse.ArgumentParser(
        prog="directory-sync-evaluation.sh",
        description="Evaluate the IP-18 provisioning-only directory sync posture.",
    )
    parser.add_argument("--dry-run", action="store_true", help="Emit a payload-safe evaluation report without writing files.")
    parser.add_argument("--active-human-users", type=int, default=0)
    parser.add_argument("--group-churn", choices=["none", "monthly", "weekly", "daily", "continuous"], default="none")
    parser.add_argument("--operator-minutes-per-week", type=int, default=0)
    parser.add_argument("--manual-provisioning-errors", type=int, default=0)
    parser.add_argument("--compliance-demand", action="store_true")
    parser.add_argument("--service-accounts", type=int, default=0)
    parser.add_argument("--output", help="Optional path to write the JSON report.")
    return parser


parsed = build_parser().parse_args(argv)
report = build_report(parsed)
payload = json.dumps(report, indent=2, sort_keys=True)
if parsed.output:
    output_path = Path(parsed.output)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(payload + "\n", encoding="utf-8")
print(payload)

if report["errors"]:
    sys.exit(2)
PY
