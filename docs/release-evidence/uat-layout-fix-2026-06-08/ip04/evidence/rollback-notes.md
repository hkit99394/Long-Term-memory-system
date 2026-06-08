# UAT Rollback Notes IP-04

Release: uat-layout-fix-2026-06-08
Environment: UAT
Status: passed
Payload safe: true
Generated at UTC: 2026-06-08T00:38:25Z

Rollback owner: local-uat-operator
Previous production image digest: sha256:120c219417d3977d8748dd316dfeb7b7f09ba7d8116f3dc32ec57401c05dd893
Candidate UAT image digest: sha256:ab02ec060909260e9bbbdf4040b37f708d72210110f67a0ecbe0141b913b0b51

Rollback boundary: application image rollback before first production write after deploy smoke; database restore is a separate owner-approved recovery action.

Communication route: Codex workspace release thread and local operator console.

Procedure:

1. Record NO-GO or rollback decision in the release thread.
2. Repoint production image selection to memorysystem:1.0.0 or the recorded previous digest.
3. Restart production API and worker roles through scripts/production-container.sh up.
4. Run scripts/production-container.sh health and authenticated admin smoke.
5. If database restore is required, use the backup/restore runbook and record the accepted data-loss window before switching traffic.

Omitted payload classes: API keys, database passwords, raw source payloads, memory bodies, backup contents.
