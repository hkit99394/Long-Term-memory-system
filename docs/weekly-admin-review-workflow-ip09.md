# Weekly Admin Review Workflow IP-09

Status: implemented for improvement plan item IP-09.

Owner: Knowledge Steward + Role Owners.

## Purpose

IP-09 turns weekly `/admin/` and `/reviews/` checks into a structured queue for
memory hygiene. The workflow collects payload-safe review signals for:

- stale feedback
- wrong feedback
- missing feedback
- sensitive feedback
- over-broad feedback
- duplicate memory candidates
- source drift
- access-boundary permission drift

Run it once per week, and after high-volume seeding or review work:

```bash
scripts/weekly-admin-review-workflow.sh
```

The output is JSON intended for operator triage. It contains ids, counts,
statuses, namespaces, source links, and suggested actions. It does not include
raw source payloads or memory body text.

## Queue Sources

| Queue | Source | Review path |
| --- | --- | --- |
| Pending reviews | `GET /api/reviews/pending` | Open `/reviews/` and approve, reject, edit, expire, delete, or supersede. |
| Stale feedback | `GET /api/reviews/context-observations?feedbackType=stale` | Open or reuse a review, then expire/edit/supersede as appropriate. |
| Wrong feedback | `GET /api/reviews/context-observations?feedbackType=wrong` | Open or reuse a review, then reject/edit/supersede as appropriate. |
| Sensitive feedback | `GET /api/reviews/context-observations?feedbackType=sensitive` | Open or reuse a review, then delete/expire or review sensitivity policy. |
| Over-broad feedback | `GET /api/reviews/context-observations?feedbackType=over_broad` | Triage manually; narrow namespace, role lens, source, or query behavior. |
| Missing feedback | `GET /api/operations/summary` retrieval feedback counts | Convert repeated misses into source-backed docs, backlog, or seed work. |
| Duplicate candidates | `GET /api/admin/memory/facts` active facts grouped by normalized identity | Compare source evidence, then supersede, expire, or keep with rationale. |
| Source drift | `scripts/source-backed-memory-hygiene.sh` | Recurate excerpts, refresh source hashes, and review affected source-backed memory. |
| Access boundary | `scripts/access-boundary-review.sh` using `POST /api/admin/access/permission-drift` | Route permission drift, service-account, OIDC binding, break-glass, and grant findings to Security/Ops. |

Only `stale`, `wrong`, and `sensitive` context observations can currently open
memory reviews directly. `missing` and `over_broad` are still weekly review
signals, but they need owner triage rather than automatic review creation.

## Command

Dry-run the planned checks without calling the API:

```bash
scripts/weekly-admin-review-workflow.sh --dry-run
```

Collect the canonical project queue:

```bash
scripts/weekly-admin-review-workflow.sh \
  --scope-type project \
  --scope-id 9f8e7d6c-5b4a-4321-9123-abcdef123002
```

The script reads `MEMORYSYSTEM_API_BASE_URL`, `MEMORYSYSTEM_API_KEY`,
`MEMORYSYSTEM_OPERATOR_API_KEY`, and `MEMORYSYSTEM_LOCAL_ACCESS_PORT` using the
same secret-safe pattern as the other local operator scripts. It never prints
API keys.

## Weekly Operating Procedure

1. Run `scripts/weekly-admin-review-workflow.sh`.
2. If `sourceDrift.status` is `needs_review`, fix source-backed seed drift
   before writing or approving related project memory.
3. Open `/reviews/` and clear pending reviews by role owner.
4. For reviewable `stale`, `wrong`, and `sensitive` observations, open or reuse
   review records and apply the appropriate review action.
5. For `over_broad`, inspect `/admin/` source links and decide whether the
   memory should move to a narrower namespace, role lens, or be superseded.
6. For `missing`, add or update canonical Markdown, backlog, or source-backed
   seed entries before proposing new durable memory.
7. For duplicate candidates, compare source evidence and either supersede,
   expire, or keep both with a documented rationale.
8. Run `scripts/access-boundary-review.sh` and route permission-drift findings,
   service-account posture, OIDC binding, break-glass, and grant-boundary
   issues to Security/Ops.
9. Close the weekly review only when every nonzero queue count has an owner,
   action, or follow-up issue.

## Completion Evidence

IP-09 is complete when:

- `scripts/weekly-admin-review-workflow.sh` emits a payload-safe weekly queue.
- the queue covers pending reviews, stale/wrong/sensitive feedback,
  over-broad feedback, missing feedback metrics, duplicate candidates, and
  source drift.
- [Project Memory Runbook](project-memory-runbook.md) points the weekly habit
  to the structured workflow.
- [Access Boundary Review IP-11](access-boundary-review-ip11.md) provides the
  weekly permission-drift and access-boundary companion workflow.
- tests cover the script contract and dry-run output.
