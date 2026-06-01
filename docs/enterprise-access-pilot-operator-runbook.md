# Enterprise Access Pilot Operator Runbook

Status: EA-09 pilot runbook

## Purpose

This runbook turns the enterprise access gate into an operator workflow for the
first pilot team. It covers OIDC setup, identity binding, service-account
bootstrap, role and namespace grant review, audit export, rollback, and
break-glass API-key handling.

The invariant stays the same throughout the pilot:

```text
Authentication resolves an internal principal.
Authorization still uses local memberships, role assignments, and namespace
grants.
```

OIDC claims, directory groups, and service credentials must never create memory
access implicitly.

## Preconditions

- The target environment has completed the normal release checklist in
  [Production Release Checklists PI-07](production-release-checklists-pi07.md).
- The migrator has applied the current migrations, including
  `026_identity_bindings.sql`, `027_access_audit_events.sql`, and
  `028_service_account_lifecycle.sql`.
- The API, worker, and migrator use production-shaped secrets from the platform
  secret store.
- `/health/live`, `/health/ready`, and `/api/operations/summary` pass for an
  authenticated operator.
- `./scripts/enterprise-access-migration-rollback-smoke.sh` passes against the
  release candidate or target-shaped rehearsal environment.
- A rollback owner, break-glass owner, and audit reviewer are named before the
  pilot window opens.

## Pilot Inputs

Record these values in the pilot change ticket or release manifest. Do not put
secret values in the ticket.

| Input | Source |
| --- | --- |
| Pilot organization id | `organizations.id` |
| Pilot project id | `projects.id` |
| Operator principal id | `principals.id` for the human operator |
| Pilot user principal ids | `principals.id` for bound human users |
| Service principal ids | `principals.id` with `principal_type = 'service'` |
| OIDC issuer | Provider discovery document |
| OIDC audience | API application/client registration |
| JWKS URI | Provider discovery document or configured JWKS endpoint |
| API base URL | Target API ingress |
| Secret-store object names | Deployment platform |
| Rollback owner and deadline | Release manifest |

## Provider Setup

1. Create an OIDC application or API resource for the memory service.
2. Configure the token audience expected by the API.
3. Confirm the issuer string exactly matches the token `iss` claim.
4. Confirm the JWKS URI returns RS256 signing keys.
5. Keep HTTPS metadata required outside `Development` and `Testing`.
6. Store non-secret provider metadata in the change ticket: issuer, audience,
   JWKS URI, tenant or directory id, and provider owner.
7. Store no client secrets in the repository or ticket.

Configure the API through the platform secret/configuration system:

```text
Authentication:Oidc:Enabled=true
Authentication:Oidc:Issuer=<issuer>
Authentication:Oidc:Audience=<audience>
Authentication:Oidc:JwksUri=<https-jwks-uri>
Authentication:Oidc:RequireHttpsMetadata=true
Authentication:Oidc:ClockSkewSeconds=300
```

Keep at least one break-glass API key configured during the pilot:

```text
Authentication:ApiKey:Keys:{keyId}:Key=<secret-api-key>
Authentication:ApiKey:Keys:{keyId}:PrincipalId=<operator-principal-guid>
Authentication:ApiKey:Keys:{keyId}:DisplayName=<operator-display-name>
```

For service-account API keys, also configure the credential id:

```text
Authentication:ApiKey:Keys:{keyId}:CredentialId=<service-credential-guid>
```

## Identity Binding

Bind each external human subject to one existing internal human principal. The
binding key is `(provider, issuer, subject)` and only active bindings resolve.

Current pilot bootstrap is a controlled database change. Use an approved
migration or operator SQL session; do not let application callers create
bindings at request time.

```sql
INSERT INTO identity_bindings (
    id,
    provider,
    issuer,
    subject,
    principal_id,
    status,
    external_display_name,
    external_email,
    external_tenant_id,
    provider_metadata
)
VALUES (
    '<identity-binding-guid>',
    'oidc',
    '<issuer>',
    '<subject>',
    '<principal-guid>',
    'active',
    '<display-name>',
    '<email>',
    '<tenant-id>',
    '{"source":"pilot-runbook"}'::jsonb
);
```

Verification:

```bash
curl -fsS \
  -H "Authorization: Bearer $OIDC_ACCESS_TOKEN" \
  "$MEMORYSYSTEM_API_BASE_URL/api/operations/summary"
```

Expected result: the request succeeds, the caller resolves to the bound
principal, and an `authentication` audit event is written with `auth_method =
'oidc'` and the identity binding id as credential evidence.

If the token is valid but the subject is unbound, the request must fail closed.
Do not create a principal or grant during the failed login path.

## Access Setup

Use the admin console at `/admin/` when possible. The API equivalents are
available for scripted pilot changes.

All access-management calls require an authenticated operator that already has
`admin` permission for the target organization or project scope.

### Membership

Organization membership:

```bash
curl -fsS -X POST \
  -H "X-Api-Key: $BREAK_GLASS_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "orgId": "<org-guid>",
    "principalId": "<principal-guid>",
    "accessLevel": "admin"
  }' \
  "$MEMORYSYSTEM_API_BASE_URL/api/admin/access/organization-memberships"
```

Project membership:

```bash
curl -fsS -X POST \
  -H "X-Api-Key: $BREAK_GLASS_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "projectId": "<project-guid>",
    "principalId": "<principal-guid>",
    "accessLevel": "reader"
  }' \
  "$MEMORYSYSTEM_API_BASE_URL/api/admin/access/project-memberships"
```

Use the narrowest membership level that supports the pilot task. Do not grant
owner/admin access to a pilot user merely because their OIDC token has a
directory admin group.

### Role Assignment

```bash
curl -fsS -X POST \
  -H "X-Api-Key: $BREAK_GLASS_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "principalId": "<principal-guid>",
    "roleId": "cto",
    "scopeType": "project",
    "scopeId": "<project-guid>"
  }' \
  "$MEMORYSYSTEM_API_BASE_URL/api/admin/access/role-assignments"
```

Supported roles are `designer`, `developer`, `cto`, `cfo`, `coo`, and `ceo`.
Role namespaces still require matching namespace grants.

### Namespace Grant

```bash
curl -fsS -X POST \
  -H "X-Api-Key: $BREAK_GLASS_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "principalId": "<principal-guid>",
    "roleId": null,
    "namespacePrefix": "/project/<project-guid>/decisions",
    "permission": "read",
    "scopeType": "project",
    "scopeId": "<project-guid>"
  }' \
  "$MEMORYSYSTEM_API_BASE_URL/api/admin/access/namespace-grants"
```

Grant permissions are `read`, `write`, `review`, and `admin`. Prefer project
and role-specific prefixes over broad organization prefixes. Avoid `/` and
`/global` for service accounts.

### Effective Access Preview

Preview before saving a broad grant and after every access-management change:

```bash
curl -fsS -X POST \
  -H "X-Api-Key: $BREAK_GLASS_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "principalId": "<principal-guid>",
    "permission": "read",
    "scopeType": "project",
    "scopeId": "<project-guid>",
    "namespacePrefix": "/project/<project-guid>/decisions"
  }' \
  "$MEMORYSYSTEM_API_BASE_URL/api/admin/access/effective-preview"
```

Expected result: `allowed` is true only when membership, role assignment, and
namespace grant policy all match the request.

## Service-Account Bootstrap

Service accounts are internal principals with `principal_type = 'service'`.
They are for agents, integrations, and automation, not shared human access.

Current pilot provisioning uses a controlled database change plus secret-store
configuration. The runtime already enforces active service profile, active
credential, allowed auth method, expiry/review posture, and last-used audit
evidence.

1. Create or confirm the service principal.
2. Create the `service_accounts` profile with an owner org or project, admin
   contact or owner principal, allowed auth method, and review or expiry date.
3. Create one active `service_account_credentials` row.
4. Store the API key value in the secret store with the service principal id and
   credential id.
5. Grant only the namespaces needed by the integration.
6. Run the effective-access preview for every granted namespace.
7. Run one authenticated read path and confirm `last_used_at` is populated for
   the credential.

Example bootstrap SQL:

```sql
INSERT INTO service_accounts (
    principal_id,
    owner_project_id,
    admin_contact,
    allowed_auth_method,
    review_due_at,
    created_by_principal_id
)
VALUES (
    '<service-principal-guid>',
    '<project-guid>',
    '<owner-email>',
    'api_key',
    now() + interval '90 days',
    '<operator-principal-guid>'
);

INSERT INTO service_account_credentials (
    id,
    service_principal_id,
    credential_label,
    auth_method,
    credential_fingerprint,
    status,
    review_due_at,
    created_by_principal_id
)
VALUES (
    '<service-credential-guid>',
    '<service-principal-guid>',
    '<credential-label>',
    'api_key',
    '<non-secret-fingerprint>',
    'active',
    now() + interval '90 days',
    '<operator-principal-guid>'
);
```

Secret-store shape:

```text
Authentication:ApiKey:Keys:{serviceKeyId}:Key=<secret-service-api-key>
Authentication:ApiKey:Keys:{serviceKeyId}:PrincipalId=<service-principal-guid>
Authentication:ApiKey:Keys:{serviceKeyId}:DisplayName=<service-display-name>
Authentication:ApiKey:Keys:{serviceKeyId}:CredentialId=<service-credential-guid>
```

## Audit Export

Export pilot audit evidence after setup, after rollback tests, and before
closing the pilot window.

```bash
curl -fsS -X POST \
  -H "X-Api-Key: $BREAK_GLASS_API_KEY" \
  -H "Content-Type: application/json" \
  -o access-audit.ndjson \
  -d '{
    "occurredFrom": "2026-06-01T00:00:00Z",
    "occurredTo": "2026-06-02T00:00:00Z",
    "scopeType": "project",
    "scopeId": "<project-guid>",
    "actionTypes": [
      "authentication",
      "authorization_denied",
      "organization_membership_change",
      "project_membership_change",
      "role_assignment_change",
      "namespace_grant_change",
      "service_credential_change",
      "audit_export"
    ],
    "outcomes": null,
    "limit": 5000
  }' \
  "$MEMORYSYSTEM_API_BASE_URL/api/admin/audit-exports"
```

The first NDJSON line is a manifest. Verify `rowCount`, `contentSha256`, time
window, scope, and filters before attaching the export to pilot evidence.

Audit exports intentionally omit raw memory and raw source payloads. Use source
links and ids for follow-up review rather than copying payload text into the
audit record.

## Rollback

Rollback from OIDC to API-key operation is configuration-only:

```text
Authentication:Oidc:Enabled=false
```

Steps:

1. Keep API-key configuration present before disabling OIDC.
2. Set `Authentication:Oidc:Enabled=false` in the secret/configuration store.
3. Redeploy or restart API instances.
4. Verify bearer tokens fail with `401`.
5. Verify break-glass API key access still reaches `/api/operations/summary`.
6. Run an authenticated memory read for the pilot project.
7. Run `./scripts/enterprise-access-migration-rollback-smoke.sh` in the closest
   available target-shaped environment.
8. Confirm no `memory_access_grants` rows were added, removed, or broadened as
   part of rollback.
9. Export audit evidence for the rollback window.

Do not delete identity bindings, service-account rows, memberships, role
assignments, or namespace grants merely to roll back OIDC authentication. Disable
OIDC first, then decide separately whether stale access records should be
disabled as a governed cleanup.

## Break-Glass API-Key Handling

Break-glass keys are for continuity during OIDC provider incidents, binding
mistakes, or pilot rollback.

Rules:

- Keep the key in the managed secret store only.
- Map the key to a named active human operator principal.
- Give the operator only the memberships and namespace grants needed for
  recovery.
- Use a distinct key id that includes the owner and review window.
- Rotate or remove the key after each break-glass event.
- Export audit evidence for the break-glass window.

Break-glass activation checklist:

1. Record incident id, operator, reason, start time, and expected expiry.
2. Confirm OIDC failure mode or access-management issue.
3. Use the break-glass key only for recovery actions.
4. Confirm service health and pilot access after recovery.
5. Rotate or remove the key.
6. Attach audit export and rotation evidence to the incident record.

## Pilot Closeout

Before considering the pilot access setup complete:

- OIDC users can authenticate and access only their intended scopes.
- API keys remain available for break-glass and service accounts only.
- Service accounts have owners, review or expiry dates, narrow grants, and
  observed credential use.
- Effective-access previews match the expected project and role boundaries.
- Cross-project reads still fail.
- `./scripts/enterprise-access-migration-rollback-smoke.sh` passes.
- Audit export includes authentication, denial if tested, membership, role,
  grant, service credential, rollback, and export evidence.
- Follow-up decisions are recorded for directory sync, group provisioning, and
  future service-account lifecycle UI.
