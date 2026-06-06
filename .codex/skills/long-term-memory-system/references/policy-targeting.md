# Policy Targeting

## Secrets

Never store or expose:

- API keys
- passwords
- bearer tokens
- private credentials
- raw connection strings with passwords
- secrets copied from `.env.production`

`OPENAI_API_KEY` is only for embedding generation. It is not a caller key for this API.

Use `MEMORYSYSTEM_OPERATOR_API_KEY` or a dedicated service key as the caller key, mapped to an active principal.

## Scopes

Supported scope types include:

```text
global
org
project
user
role
agent
session
```

Use the narrowest scope that matches the work. Do not switch principals or scopes to bypass policy.

## Namespaces

Namespaces are path-like strings. They must start with `/`, avoid empty path segments, match the request scope for durable writes, and match authorized grant prefixes for reads, writes, reviews, and admin actions.

Canonical patterns:

```text
/global/{category}
/org/{orgId}/{category}
/project/{projectId}/{category}
/user/{principalId}/{category}
/role/{roleId}/shared
/agent/{agentPrincipalId}/{category}
/session/{sessionId}/{category}
```

Role-lens patterns:

```text
/org/{orgId}/role/{roleId}/lens
/project/{projectId}/role/{roleId}/lens
```

Grant prefixes apply to the exact namespace and its children. A grant on `/project/{projectId}/decisions` covers `/project/{projectId}/decisions/api`, but not `/project/{projectId}/role/cto/lens`.

## Role Targeting

`roleId` meanings:

```text
POST /api/events                    valid for role-scoped events only
POST /api/memory/proposals          required for role-lens proposals
GET  /api/memory/context            optional retrieval perspective
POST /api/memory/query-facts        optional role-specific fact filtering
POST /api/memory/context/feedback   optional retrieval-perspective metadata
```

Supported role ids are `product_owner`, `cto`, `security_professional`,
`it_manager`, `developer`, `tester_qa`, `release_manager`,
`knowledge_steward`, `designer`, `cfo`, `coo`, and `ceo`.

Role-specific memory is an access boundary. Do not leak Project A CTO memory to Project A Product Owner callers or Project B callers.

## Trust, Retention, And Sensitivity

Common `trustLevel` values:

```text
user_scoped
human_approved
system_trusted
agent_private
tool_output
retrieved_untrusted
web_content
```

Common `retentionClass` values:

```text
standard
ephemeral
audit
legal_hold
erasure_requested
```

Common `sensitivity` values:

```text
none
personal
secret
regulated
```

Classify conservatively. If uncertain, avoid storing the memory or route it for review.

## Idempotency

Use `Idempotency-Key` for:

```text
POST /api/events
POST /api/memory/proposals
```

Retry with the same key only when the request body is identical. Reusing a key with a different body should be treated as a client bug.

`POST /api/memory/context/feedback` is append-only and may create duplicate observations if blindly retried.

## Memory Use In Answers

When memory materially affects an answer:

- prefer current user instructions over older memory
- cite or mention source evidence when useful
- mention uncertainty when memory is tentative, contradicted, stale, or under review
- do not reveal hidden excluded candidates or unauthorized memory

## Failure Handling

`401` means authentication failed. Ask for a valid API key or key mapping.

`403` means authorization failed. Ask for the correct principal, scope, role assignment, or namespace grant. Do not bypass policy.

`400` usually means invalid scope, namespace, request body, idempotency conflict, or unsupported enum value. Fix the request rather than retrying blindly.
