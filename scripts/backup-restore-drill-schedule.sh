#!/usr/bin/env bash
set -euo pipefail

python3 - "$@" <<'PY'
import argparse
import json
import sys
from datetime import datetime, timezone


environment_profiles = {
    "local": {
        "rpo": "best-effort local developer data; no product RPO claim",
        "rto": "<=30 minutes to complete the local restore smoke once Docker PostgreSQL is available",
        "evidencePrefix": "/tmp/memorysystem-backup-evidence/local",
        "protectedVolumeChecks": [
            "Keep the default Docker PostgreSQL volume protected unless an operator explicitly chooses a reset.",
            "Create local logical dumps under /tmp/memorysystem-backups, not inside git.",
            "Do not attach local dumps to shared evidence because they can contain raw payloads and memory text.",
        ],
        "exportChecks": [
            "Verify pg_restore --list succeeds for the local custom-format dump.",
            "Record the backup file path only when the dump is kept intentionally for manual inspection.",
        ],
    },
    "ci": {
        "rpo": "not applicable for ephemeral CI databases",
        "rto": "<=30 minutes for a database-backed restore validation job when CI PostgreSQL is provisioned",
        "evidencePrefix": "ci-artifacts/backup-restore",
        "protectedVolumeChecks": [
            "Use disposable CI databases or isolated service containers.",
            "Never persist CI dumps beyond the configured artifact retention window.",
        ],
        "exportChecks": [
            "Attach shell syntax results for backup and restore scripts.",
            "Attach restore smoke output only from an isolated CI database.",
        ],
    },
    "pilot": {
        "rpo": "<=24 hours from the latest successful backup export or managed recovery point",
        "rto": "<=4 hours for restore-to-new-database validation and application readiness proof",
        "evidencePrefix": "s3://<release-evidence-bucket>/pilot/backup-restore/",
        "protectedVolumeChecks": [
            "Use the managed PostgreSQL profile; do not fall back to the local Docker volume for pilot evidence.",
            "Restore validation must target a fresh validation database, not the active pilot database.",
            "Confirm retention and legal-hold status before pruning any backup or validation artifact.",
        ],
        "exportChecks": [
            "Upload backup export evidence JSON, restore validation evidence JSON, metrics, and erasure replay ledger evidence to the release evidence bucket.",
            "Keep logical backup files encrypted at rest and outside the application image.",
            "Set MEMORYSYSTEM_RESTORE_VALIDATION_REQUIRE_ERASURE_REPLAY=true for backup validation.",
        ],
    },
    "production": {
        "rpo": "<=1 hour when managed PITR evidence is attached; <=24 hours from backup export until PITR is proven",
        "rto": "<=2 hours for restore-to-new-database validation; break-glass production cutover needs incident commander sign-off",
        "evidencePrefix": "s3://<controlled-audit-store>/production/backup-restore/",
        "protectedVolumeChecks": [
            "Use the managed PostgreSQL production profile; the local Docker volume is never production recovery evidence.",
            "Restore into a new database first and switch the connection-string secret only after validation passes.",
            "Confirm retention, legal hold, erasure replay, and backup deletion constraints before any restore or prune action.",
        ],
        "exportChecks": [
            "Attach managed backup/PITR status, backup export evidence JSON, restore validation evidence JSON, metrics, and erasure replay ledger evidence.",
            "Keep backup objects encrypted, access-restricted, and retained under the production audit policy.",
            "Set MEMORYSYSTEM_RESTORE_VALIDATION_REQUIRE_ERASURE_REPLAY=true for every production restore validation.",
        ],
    },
}

drill_profiles = {
    "weekly": {
        "cadence": "Every week before the release window.",
        "nextDuePolicy": "Run on the first operations review day of the week, and rerun before a release if backup age exceeds the RPO.",
        "commands": [
            "/app/scripts/platform-backup-export.sh",
            "check memorysystem_backup_age_seconds against the environment RPO",
            "check memorysystem_backup_export_success == 1",
        ],
        "requiredEvidence": [
            "backup export evidence JSON",
            "backup metrics file with memorysystem_backup_export_success and memorysystem_backup_age_seconds",
            "operator, environment, backup id, backup timestamp, and evidence prefix",
        ],
        "passCriteria": [
            "Latest successful backup or managed recovery point is inside the RPO.",
            "Backup evidence is uploaded to the configured evidence prefix.",
            "No retention or legal-hold issue blocks future restore validation.",
        ],
        "failureActions": [
            "Page the IT/Ops owner when backup age exceeds RPO.",
            "Block production release gates until a fresh backup export or managed recovery point is attached.",
            "Open an incident if backup export failures repeat across two scheduled attempts.",
        ],
    },
    "monthly": {
        "cadence": "Every month in the first approved maintenance window.",
        "nextDuePolicy": "Run after the weekly backup freshness check and before closing the monthly operations review.",
        "commands": [
            "/app/scripts/platform-backup-export.sh",
            "/app/scripts/platform-erasure-replay-ledger-export.sh",
            "MEMORYSYSTEM_RESTORE_VALIDATION_REQUIRE_ERASURE_REPLAY=true /app/scripts/platform-restore-validation.sh",
            "compare memorysystem_restore_validation_table_rows with the source snapshot",
        ],
        "requiredEvidence": [
            "backup export evidence JSON",
            "erasure replay ledger evidence JSON and CSV hash",
            "restore validation evidence JSON",
            "restore validation metrics with table-row and pgvector checks",
            "observed RPO and observed RTO values",
        ],
        "passCriteria": [
            "Restore validation succeeds against a fresh database.",
            "The vector extension check passes.",
            "Every table in scripts/restore-validation-tables.txt is counted.",
            "Erasure replay is present and successful for pilot and production.",
            "Observed RPO and RTO are inside the environment expectations.",
        ],
        "failureActions": [
            "Keep the drill open until restore validation evidence is passing.",
            "Block production releases when restore validation is missing, stale, or failed.",
            "Create a follow-up for any table-count drift without a documented migration or retention reason.",
        ],
    },
    "quarterly": {
        "cadence": "Every quarter in a named recovery rehearsal window.",
        "nextDuePolicy": "Run once per quarter after owner sign-off, and after major platform or database-provider changes.",
        "commands": [
            "/app/scripts/platform-backup-export.sh",
            "/app/scripts/platform-erasure-replay-ledger-export.sh",
            "MEMORYSYSTEM_RESTORE_VALIDATION_REQUIRE_ERASURE_REPLAY=true /app/scripts/platform-restore-validation.sh",
            "scripts/production-container.sh preflight",
            "scripts/production-container.sh health",
            "MEMORYSYSTEM_API_BASE_URL=<target-url> ./scripts/operations-metrics-smoke.sh",
        ],
        "requiredEvidence": [
            "monthly restore validation evidence",
            "application readiness proof for the restored database",
            "rollback owner, communication route, decision deadline, and audit-store prefix",
            "known data-loss window and recovery-point note",
        ],
        "passCriteria": [
            "Restore validation and application readiness both pass.",
            "Rollback owner signs the RTO/RPO result and the known data-loss window.",
            "Evidence includes the backup id, restore database, operator, and validation timestamps.",
        ],
        "failureActions": [
            "Treat a missed quarterly rehearsal as a release readiness blocker.",
            "Escalate failed readiness checks through the incident route.",
            "Do not switch production traffic to a restored database until health, metrics, and authenticated reads pass.",
        ],
    },
    "post-erasure": {
        "cadence": "Within 7 days of an erasure or redaction batch that may be newer than retained backups.",
        "nextDuePolicy": "Run before the next release gate when the erasure batch affects data covered by retained backups.",
        "commands": [
            "/app/scripts/platform-erasure-replay-ledger-export.sh",
            "MEMORYSYSTEM_RESTORE_VALIDATION_REQUIRE_ERASURE_REPLAY=true /app/scripts/platform-restore-validation.sh",
            "verify memorysystem_restore_erasure_replay_validation_success == 1",
        ],
        "requiredEvidence": [
            "erasure replay ledger evidence JSON",
            "restore evidence erasureReplay section with ledger hash and target-set hash",
            "legal-hold skip count and validation failure count",
        ],
        "passCriteria": [
            "Restore validation refuses to pass without a backup timestamp and erasure replay ledger.",
            "Post-backup redaction and erasure actions are replayed or verified.",
            "No restored source event, memory fact, chunk, embedding, review note, or vault export exposes erased payloads.",
        ],
        "failureActions": [
            "Block restore use of affected backups until erasure replay passes.",
            "Open a governance review for legal-hold skips or replay validation failures.",
            "Refresh the backup inventory with erasure coverage notes.",
        ],
    },
    "release-gate": {
        "cadence": "Before pilot or production go/no-go.",
        "nextDuePolicy": "Run after migration evidence and before final approval is signed.",
        "commands": [
            "/app/scripts/platform-backup-export.sh",
            "/app/scripts/platform-erasure-replay-ledger-export.sh",
            "MEMORYSYSTEM_RESTORE_VALIDATION_REQUIRE_ERASURE_REPLAY=true /app/scripts/platform-restore-validation.sh",
            "./scripts/target-environment-evidence-verify.sh <target-environment-evidence-manifest.json>",
        ],
        "requiredEvidence": [
            "managed backup/PITR status or latest backup export evidence",
            "latest restore-to-new-database validation result",
            "backup/restore metrics",
            "target evidence manifest verifier output",
            "go/no-go approval note with known data-loss window",
        ],
        "passCriteria": [
            "Backup evidence predates migration.",
            "Restore validation is recent, successful, and attached to the release evidence prefix.",
            "Target evidence verifier passes for backup/restore artifacts.",
            "The release record names the rollback owner and database restore boundary.",
        ],
        "failureActions": [
            "Stop the release until backup and restore evidence is complete.",
            "Keep IP-04 target-environment evidence Doing if the real target manifest is missing or fails verification.",
            "Escalate missing backup/PITR evidence to IT/Ops before any production migration.",
        ],
    },
}

local_command_overrides = {
    "weekly": [
        "docker compose up -d --wait postgres",
        "./scripts/backup-restore-smoke.sh",
    ],
    "monthly": [
        "docker compose up -d --wait postgres",
        "./scripts/backup-restore-smoke.sh",
        "bash -n scripts/platform-backup-export.sh",
        "bash -n scripts/platform-erasure-replay-ledger-export.sh",
        "bash -n scripts/platform-restore-validation.sh",
    ],
    "quarterly": [
        "docker compose up -d --wait postgres",
        "./scripts/backup-restore-smoke.sh",
        "scripts/production-container.sh preflight",
    ],
}

metrics = [
    "memorysystem_backup_export_success",
    "memorysystem_backup_age_seconds",
    "memorysystem_backup_export_timestamp_seconds",
    "memorysystem_backup_export_bytes",
    "memorysystem_erasure_replay_ledger_export_success",
    "memorysystem_restore_validation_success",
    "memorysystem_restore_validation_age_seconds",
    "memorysystem_restore_validation_table_rows",
    "memorysystem_restore_erasure_replay_validation_success",
]

runbooks = [
    "docs/backup-restore.md",
    "docs/external-managed-postgres-profile.md",
    "docs/production-release-checklists-pi07.md",
    "docs/target-environment-evidence-hardening-ip04.md",
]


def utc_now():
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def build_payload(args):
    environment = environment_profiles[args.environment]
    drill = drill_profiles[args.drill_type]
    evidence_prefix = args.evidence_prefix or environment["evidencePrefix"]
    commands = drill["commands"]
    if args.environment == "local":
        commands = local_command_overrides.get(args.drill_type, commands)

    return {
        "status": "dry_run" if args.dry_run else "schedule",
        "generatedAt": utc_now(),
        "owner": "IT/Ops",
        "payloadSafe": True,
        "rawSourcePayloadsIncluded": False,
        "environment": args.environment,
        "drillType": args.drill_type,
        "cadence": drill["cadence"],
        "nextDuePolicy": drill["nextDuePolicy"],
        "rpo": {
            "expectation": environment["rpo"],
            "measurement": "Compare backup timestamp or managed recovery point with the drill start time.",
        },
        "rto": {
            "expectation": environment["rto"],
            "measurement": "Measure elapsed time from restore drill start to passing restore validation and readiness evidence.",
        },
        "commands": commands,
        "requiredEvidence": drill["requiredEvidence"],
        "evidencePrefix": evidence_prefix,
        "passCriteria": drill["passCriteria"],
        "failureActions": drill["failureActions"],
        "protectedVolumeChecks": environment["protectedVolumeChecks"],
        "exportChecks": environment["exportChecks"],
        "metrics": metrics,
        "runbooks": runbooks,
        "notes": [
            "This script prints the drill contract only; it does not execute backup, restore, or network commands.",
            "Evidence must contain ids, timestamps, hashes, row counts, metric names, operator names, and storage prefixes rather than raw payloads.",
        ],
    }


def build_parser():
    parser = argparse.ArgumentParser(
        prog="backup-restore-drill-schedule.sh",
        description="Print a payload-safe backup and restore drill schedule for operators.",
    )
    parser.add_argument("--environment", choices=sorted(environment_profiles), default="production")
    parser.add_argument("--drill-type", choices=sorted(drill_profiles), default="monthly")
    parser.add_argument("--evidence-prefix", help="Override the evidence prefix shown in the output.")
    parser.add_argument("--dry-run", action="store_true", help="Mark output as a dry run; no commands are executed.")
    return parser


parser = build_parser()
parsed = parser.parse_args(sys.argv[1:])
print(json.dumps(build_payload(parsed), indent=2, sort_keys=True))
PY
