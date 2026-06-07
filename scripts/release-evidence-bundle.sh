#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

python3 - "$ROOT_DIR" "$@" <<'PY'
import argparse
import hashlib
import json
import os
import re
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path

repo_root = Path(sys.argv[1])
argv = sys.argv[2:]

default_output_dir = Path(os.environ.get("MEMORYSYSTEM_RELEASE_EVIDENCE_DIR", "/tmp/memorysystem-release-evidence"))

evidence_definitions = [
    {
        "id": "tests",
        "gate": "tests",
        "type": "test_report",
        "arg": "tests_report",
        "env": "MEMORYSYSTEM_RELEASE_EVIDENCE_TESTS_REPORT",
        "description": "Unit, integration, TypeScript, smoke, or CI test report for the release candidate.",
    },
    {
        "id": "migration_status",
        "gate": "migration_status",
        "type": "migration_status",
        "arg": "migration_status",
        "env": "MEMORYSYSTEM_RELEASE_EVIDENCE_MIGRATION_STATUS",
        "description": "Migrator output, migration status, or explicit no-migration evidence.",
    },
    {
        "id": "health",
        "gate": "health",
        "type": "health_report",
        "arg": "health_report",
        "env": "MEMORYSYSTEM_RELEASE_EVIDENCE_HEALTH_REPORT",
        "description": "Health/live, health/ready, worker heartbeat, and authenticated smoke evidence.",
    },
    {
        "id": "operations_summary",
        "gate": "operations_summary",
        "type": "operations_summary",
        "arg": "operations_summary",
        "env": "MEMORYSYSTEM_RELEASE_EVIDENCE_OPERATIONS_SUMMARY",
        "description": "Payload-safe operations summary or metrics snapshot for the release window.",
    },
    {
        "id": "benchmark",
        "gate": "benchmark",
        "type": "benchmark_release_gate",
        "arg": "benchmark_report",
        "env": "MEMORYSYSTEM_RELEASE_EVIDENCE_BENCHMARK_REPORT",
        "description": "Benchmark release-gate report or explicit release-manager skip record.",
    },
    {
        "id": "backup_restore",
        "gate": "backup_restore",
        "type": "backup_restore_evidence",
        "arg": "backup_restore_report",
        "env": "MEMORYSYSTEM_RELEASE_EVIDENCE_BACKUP_RESTORE_REPORT",
        "description": "Backup freshness, backup export, restore validation, or backup/restore smoke evidence.",
    },
    {
        "id": "rollback",
        "gate": "rollback",
        "type": "rollback_plan",
        "arg": "rollback_report",
        "env": "MEMORYSYSTEM_RELEASE_EVIDENCE_ROLLBACK_REPORT",
        "description": "Named rollback owner, rollback boundary, command/procedure, and decision point.",
    },
]


class BundleError(RuntimeError):
    pass


def utc_now():
    return datetime.now(timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")


def sanitize(value):
    sanitized = re.sub(r"[^A-Za-z0-9_.-]+", "-", value.strip())
    return sanitized.strip("-") or "unknown"


def sha256_bytes(data):
    return hashlib.sha256(data).hexdigest()


def sha256_file(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def git_value(args):
    if args.git_commit:
        return args.git_commit

    try:
        return subprocess.check_output(
            ["git", "rev-parse", "--short=12", "HEAD"],
            cwd=repo_root,
            stderr=subprocess.DEVNULL,
            text=True,
            timeout=5,
        ).strip()
    except Exception:
        return "unknown"


def git_dirty():
    try:
        result = subprocess.run(
            ["git", "status", "--short"],
            cwd=repo_root,
            stdout=subprocess.PIPE,
            stderr=subprocess.DEVNULL,
            text=True,
            timeout=5,
            check=False,
        )
        return bool(result.stdout.strip())
    except Exception:
        return None


def display_path(path):
    try:
        return str(path.relative_to(repo_root))
    except ValueError:
        return str(path)


def resolve_artifact_path(args, definition):
    configured = getattr(args, definition["arg"])
    if configured:
        return Path(configured)

    configured = os.environ.get(definition["env"])
    if configured:
        return Path(configured)

    return default_output_dir / "inputs" / f"{definition['id']}.json"


def artifact_record(args, definition):
    path = resolve_artifact_path(args, definition)
    required = True
    if path.exists() and path.is_file():
        return {
            "id": definition["id"],
            "gate": definition["gate"],
            "type": definition["type"],
            "required": required,
            "status": "present",
            "path": display_path(path),
            "bytes": path.stat().st_size,
            "sha256": sha256_file(path),
            "description": definition["description"],
        }

    return {
        "id": definition["id"],
        "gate": definition["gate"],
        "type": definition["type"],
        "required": required,
        "status": "missing",
        "path": str(path),
        "bytes": 0,
        "sha256": None,
        "description": definition["description"],
    }


def write_text(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(value, encoding="utf-8")


def build_summary(manifest):
    lines = [
        f"# Release Evidence Bundle {manifest['release']['releaseId']}",
        "",
        f"Status: {manifest['bundleStatus']}",
        "",
        "| Gate | Status | Path | SHA-256 |",
        "| --- | --- | --- | --- |",
    ]
    for artifact in manifest["artifacts"]:
        lines.append(
            f"| {artifact['gate']} | {artifact['status']} | `{artifact['path']}` | `{artifact['sha256'] or ''}` |"
        )

    lines.extend(
        [
            "",
            "Payload safety: raw artifact payloads, source event payloads, memory bodies, review notes, queries, embeddings, provider payloads, API keys, and secret values are not embedded in this bundle.",
            "",
        ]
    )
    return "\n".join(lines)


def build_manifest(args):
    release_id = args.release_id or os.environ.get("MEMORYSYSTEM_RELEASE_ID") or "local-draft"
    environment = args.environment or os.environ.get("MEMORYSYSTEM_ENVIRONMENT") or "local"
    operator_id = args.operator_id or os.environ.get("MEMORYSYSTEM_RELEASE_OPERATOR_ID") or "unknown"
    rollback_owner = args.rollback_owner or os.environ.get("MEMORYSYSTEM_RELEASE_ROLLBACK_OWNER") or "unknown"
    generated_at = args.generated_at_utc or utc_now()
    bundle_id = args.bundle_id or f"memorysystem-release-{sanitize(environment)}-{sanitize(release_id)}-{sanitize(generated_at)}"
    output_dir = Path(args.output_dir) if args.output_dir else default_output_dir
    manifest_path = output_dir / f"{bundle_id}.json"
    artifact_index_path = output_dir / f"{bundle_id}-artifacts.ndjson"
    summary_path = output_dir / f"{bundle_id}.md"
    hash_path = output_dir / f"{bundle_id}.json.sha256"

    artifacts = [artifact_record(args, definition) for definition in evidence_definitions]
    missing_required = [
        artifact
        for artifact in artifacts
        if artifact["required"] and artifact["status"] != "present"
    ]
    errors = [
        f"Missing required release evidence artifact: {artifact['id']} ({artifact['path']})"
        for artifact in missing_required
    ]

    if args.mode == "strict" and rollback_owner == "unknown":
        errors.append("Strict release evidence bundles require --rollback-owner or MEMORYSYSTEM_RELEASE_ROLLBACK_OWNER.")

    artifact_set_sha256 = sha256_bytes(
        json.dumps(artifacts, separators=(",", ":"), sort_keys=True).encode("utf-8")
    )
    status = "complete" if not errors else "incomplete"

    return {
        "kind": "memorysystem.release_evidence_bundle",
        "schemaVersion": 1,
        "status": "dry_run" if args.dry_run else status,
        "bundleStatus": status,
        "mode": args.mode,
        "payloadSafe": True,
        "rawArtifactPayloadsIncluded": False,
        "rawSourcePayloadsIncluded": False,
        "release": {
            "releaseId": release_id,
            "environment": environment,
            "operatorId": operator_id,
            "rollbackOwner": rollback_owner,
            "gitCommit": git_value(args),
            "gitDirty": git_dirty(),
            "imageDigest": args.image_digest or "",
            "changeSummary": args.change_summary or "",
            "generatedAtUtc": generated_at,
        },
        "bundle": {
            "bundleId": bundle_id,
            "outputDir": str(output_dir),
            "manifestPath": str(manifest_path),
            "artifactIndexPath": str(artifact_index_path),
            "summaryPath": str(summary_path),
            "sha256SidecarPath": str(hash_path),
            "artifactSetSha256": artifact_set_sha256,
        },
        "requiredGates": [definition["gate"] for definition in evidence_definitions],
        "artifacts": artifacts,
        "counts": {
            "artifactCount": len(artifacts),
            "presentArtifacts": sum(1 for artifact in artifacts if artifact["status"] == "present"),
            "missingArtifacts": sum(1 for artifact in artifacts if artifact["status"] != "present"),
            "requiredArtifacts": sum(1 for artifact in artifacts if artifact["required"]),
            "missingRequiredArtifacts": len(missing_required),
        },
        "commands": {
            "generateDraft": "scripts/release-evidence-bundle.sh --mode draft --release-id {releaseId}",
            "generateStrict": "scripts/release-evidence-bundle.sh --mode strict --release-id {releaseId} --rollback-owner {owner}",
            "benchmarkGate": "scripts/benchmark-release-gate.sh",
            "operationsMetricsSmoke": "scripts/operations-metrics-smoke.sh",
            "backupRestoreSmoke": "scripts/backup-restore-smoke.sh",
            "targetEvidenceVerify": "scripts/target-environment-evidence-verify.sh /path/to/target-environment-evidence-manifest.json",
        },
        "promotionRule": "Pilot or production promotion requires strict mode, zero missing required artifacts, a named rollback owner, and archived JSON, NDJSON, Markdown, and SHA-256 sidecar outputs.",
        "omittedPayloadClasses": [
            "raw artifact contents",
            "source event payloads",
            "memory bodies",
            "review notes",
            "raw queries",
            "embedding inputs",
            "provider payload bytes",
            "API keys",
            "secret values",
        ],
        "errors": errors,
    }


def write_outputs(manifest):
    manifest_path = Path(manifest["bundle"]["manifestPath"])
    artifact_index_path = Path(manifest["bundle"]["artifactIndexPath"])
    summary_path = Path(manifest["bundle"]["summaryPath"])
    hash_path = Path(manifest["bundle"]["sha256SidecarPath"])

    manifest_json = json.dumps(manifest, indent=2, sort_keys=True) + "\n"
    artifact_index = "\n".join(json.dumps(artifact, sort_keys=True) for artifact in manifest["artifacts"]) + "\n"
    summary = build_summary(manifest)
    manifest_sha256 = sha256_bytes(manifest_json.encode("utf-8"))

    write_text(manifest_path, manifest_json)
    write_text(artifact_index_path, artifact_index)
    write_text(summary_path, summary)
    write_text(hash_path, f"{manifest_sha256}  {manifest_path.name}\n")


def build_parser():
    parser = argparse.ArgumentParser(
        prog="release-evidence-bundle.sh",
        description="Generate the IP-17 payload-safe release evidence bundle.",
    )
    parser.add_argument("--mode", choices=["draft", "strict"], default=os.environ.get("MEMORYSYSTEM_RELEASE_EVIDENCE_MODE", "draft"))
    parser.add_argument("--dry-run", action="store_true", help="Print the bundle manifest without writing output files.")
    parser.add_argument("--release-id", help="Release id to bind the evidence bundle to.")
    parser.add_argument("--environment", help="Release environment such as local, ci, pilot, or production.")
    parser.add_argument("--operator-id", help="Release operator id.")
    parser.add_argument("--rollback-owner", help="Named rollback owner for the release.")
    parser.add_argument("--bundle-id", help="Stable bundle id. Defaults to release/environment/timestamp.")
    parser.add_argument("--output-dir", help="Directory for JSON, NDJSON, Markdown, and SHA-256 outputs.")
    parser.add_argument("--git-commit", help="Git commit to record. Defaults to git rev-parse --short=12 HEAD.")
    parser.add_argument("--image-digest", default=os.environ.get("MEMORYSYSTEM_RELEASE_IMAGE_DIGEST", ""))
    parser.add_argument("--change-summary", default=os.environ.get("MEMORYSYSTEM_RELEASE_CHANGE_SUMMARY", ""))
    parser.add_argument("--generated-at-utc", help="Override generated timestamp, mainly for deterministic tests.")
    parser.add_argument("--tests-report")
    parser.add_argument("--migration-status")
    parser.add_argument("--health-report")
    parser.add_argument("--operations-summary")
    parser.add_argument("--benchmark-report")
    parser.add_argument("--backup-restore-report")
    parser.add_argument("--rollback-report")
    return parser


parsed = build_parser().parse_args(argv)
manifest = build_manifest(parsed)
if not parsed.dry_run:
    write_outputs(manifest)

print(json.dumps(manifest, indent=2, sort_keys=True))

if parsed.mode == "strict" and manifest["errors"]:
    sys.exit(2)
PY
