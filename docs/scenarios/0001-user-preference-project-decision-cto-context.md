# Scenario 0001: User Preference, Project Decision, and CTO Context

## Purpose

This is the first end-to-end scenario for the MVP. It is confirmed in M0 so M1-M6 can build toward one concrete throughline instead of separate feature demos.

The scenario proves that the system can store a user preference, store a project decision, add CTO role context, and later build a permission-aware context packet without leaking another project's memory.

## Scenario Boundary

This document defines exactly one first scenario:

- a user-level preference from one human principal
- a project-level decision in one organization project
- a shared CTO role principle
- a project-specific CTO lens over that project's decision
- one authorized context packet for the project CTO request
- one blocked cross-project read check

This scenario must stay grounded in the core system constraints:

- Every durable memory has source evidence.
- Durable memory writes are broker-controlled.
- Access is executable through principals, organization membership, project membership, role assignments, and grants. Namespace strings are not authorization by themselves.
- Shared role principles do not contain project-specific facts.
- Project-role lenses interpret project truth. They cannot replace project facts or leak those facts into other projects.
- Context retrieval is permission-aware before candidate selection, ranking, and packet construction.

## Sample Actors and Access

Principal:

```yaml
principal:
  id: "11111111-1111-4111-8111-111111111111"
  principal_type: "human"
  display_name: "Jack Tam"
  api_key_label: "local-jack"
```

Organization:

```yaml
organization:
  id: "22222222-2222-4222-8222-222222222222"
  slug: "memory-lab"
  name: "Memory Lab"
```

Project A:

```yaml
project:
  id: "33333333-3333-4333-8333-333333333333"
  org_id: "22222222-2222-4222-8222-222222222222"
  slug: "long-term-memory-system"
  name: "Long-Term Memory System"
```

Project B, used only for the non-leakage check:

```yaml
project:
  id: "44444444-4444-4444-8444-444444444444"
  org_id: "22222222-2222-4222-8222-222222222222"
  slug: "private-finance-tool"
  name: "Private Finance Tool"
```

Role:

```yaml
role:
  id: "cto"
  name: "CTO"
```

Executable access for the principal:

```yaml
org_membership:
  org_id: "22222222-2222-4222-8222-222222222222"
  principal_id: "11111111-1111-4111-8111-111111111111"
  access_level: "owner"

project_membership:
  project_id: "33333333-3333-4333-8333-333333333333"
  principal_id: "11111111-1111-4111-8111-111111111111"
  access_level: "admin"

role_assignment:
  principal_id: "11111111-1111-4111-8111-111111111111"
  role_id: "cto"
  scope_type: "project"
  scope_id: "33333333-3333-4333-8333-333333333333"

role_assignment:
  principal_id: "11111111-1111-4111-8111-111111111111"
  role_id: "cto"
  scope_type: "org"
  scope_id: "22222222-2222-4222-8222-222222222222"

memory_grants:
  - principal_id: "11111111-1111-4111-8111-111111111111"
    permission: "read"
    namespace_prefix: "/user/11111111-1111-4111-8111-111111111111/preferences"
  - principal_id: "11111111-1111-4111-8111-111111111111"
    permission: "write"
    namespace_prefix: "/user/11111111-1111-4111-8111-111111111111/preferences"
  - role_id: "cto"
    permission: "read"
    namespace_prefix: "/org/22222222-2222-4222-8222-222222222222/policies"
  - role_id: "cto"
    permission: "read"
    namespace_prefix: "/project/33333333-3333-4333-8333-333333333333/decisions"
  - role_id: "cto"
    permission: "read"
    namespace_prefix: "/role/cto/shared"
  - role_id: "cto"
    permission: "read"
    namespace_prefix: "/org/22222222-2222-4222-8222-222222222222/role/cto/lens"
  - role_id: "cto"
    permission: "read"
    namespace_prefix: "/project/33333333-3333-4333-8333-333333333333/role/cto/lens"
```

There is intentionally no Project B membership, Project B CTO role assignment, or grant for the principal.

## Source Events

User preference event:

```json
{
  "id": "66666666-6666-4666-8666-666666666666",
  "eventType": "user_message",
  "principalId": "11111111-1111-4111-8111-111111111111",
  "scopeType": "user",
  "scopeId": "11111111-1111-4111-8111-111111111111",
  "payload": {
    "message": "For technical planning, I prefer concise decision logs with a short rationale and explicit tradeoffs."
  }
}
```

Project decision event:

```json
{
  "id": "77777777-7777-4777-8777-777777777777",
  "eventType": "user_message",
  "principalId": "11111111-1111-4111-8111-111111111111",
  "scopeType": "project",
  "scopeId": "33333333-3333-4333-8333-333333333333",
  "payload": {
    "decision": "Use SQL-first migrations plus raw Npgsql for the M1-M3 initial backend path.",
    "rationale": "The first slice needs explicit schema control, migration repeatability, and clear authorization predicates before adding heavier ORM behavior.",
    "tradeoffs": [
      "More manual mapping in early repositories.",
      "Less abstraction around permission-aware SQL."
    ]
  }
}
```

CTO shared principle source event:

```json
{
  "id": "88888888-8888-4888-8888-888888888888",
  "eventType": "user_message",
  "principalId": "11111111-1111-4111-8111-111111111111",
  "scopeType": "org",
  "scopeId": "22222222-2222-4222-8222-222222222222",
  "payload": {
    "principle": "A CTO context packet should foreground architecture risk, operational reversibility, delivery sequencing, and security boundaries."
  }
}
```

Project CTO lens source event:

```json
{
  "id": "99999999-9999-4999-8999-999999999999",
  "eventType": "user_message",
  "principalId": "11111111-1111-4111-8111-111111111111",
  "scopeType": "project",
  "scopeId": "33333333-3333-4333-8333-333333333333",
  "payload": {
    "role": "cto",
    "baseMemoryFactId": "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb",
    "lens": "For the CTO view, the SQL-first Npgsql decision should be treated as a risk-reduction move: it keeps authorization predicates visible while the schema and event provenance model are still stabilizing."
  }
}
```

## Expected Durable Memory

User preference:

```yaml
memory_fact:
  id: "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"
  memory_type: "preference"
  scope_type: "user"
  scope_id: "11111111-1111-4111-8111-111111111111"
  namespace: "/user/11111111-1111-4111-8111-111111111111/preferences"
  subject: "technical planning format"
  predicate: "prefers"
  object: "concise decision logs with short rationale and explicit tradeoffs"
  source_event_id: "66666666-6666-4666-8666-666666666666"
  proposed_by_principal_id: "11111111-1111-4111-8111-111111111111"
  status: "active"
```

Project decision:

```yaml
memory_fact:
  id: "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"
  memory_type: "decision"
  scope_type: "project"
  scope_id: "33333333-3333-4333-8333-333333333333"
  org_id: "22222222-2222-4222-8222-222222222222"
  project_id: "33333333-3333-4333-8333-333333333333"
  namespace: "/project/33333333-3333-4333-8333-333333333333/decisions"
  subject: "M1-M3 data access"
  predicate: "uses"
  object: "SQL-first migrations plus raw Npgsql"
  source_event_id: "77777777-7777-4777-8777-777777777777"
  proposed_by_principal_id: "11111111-1111-4111-8111-111111111111"
  status: "active"
```

Shared CTO base fact:

```yaml
memory_fact:
  id: "cccccccc-cccc-4ccc-8ccc-cccccccccccc"
  memory_type: "role_principle"
  scope_type: "org"
  scope_id: "22222222-2222-4222-8222-222222222222"
  org_id: "22222222-2222-4222-8222-222222222222"
  namespace: "/org/22222222-2222-4222-8222-222222222222/policies"
  subject: "CTO context packet"
  predicate: "should_foreground"
  object: "architecture risk, operational reversibility, delivery sequencing, and security boundaries"
  source_event_id: "88888888-8888-4888-8888-888888888888"
  proposed_by_principal_id: "11111111-1111-4111-8111-111111111111"
  status: "active"
```

Shared CTO principle:

```yaml
role_memory_lens:
  id: "dddddddd-dddd-4ddd-8ddd-dddddddddddd"
  role_id: "cto"
  scope_type: "org"
  scope_id: "22222222-2222-4222-8222-222222222222"
  org_id: "22222222-2222-4222-8222-222222222222"
  project_id: null
  base_memory_fact_id: "cccccccc-cccc-4ccc-8ccc-cccccccccccc"
  interpretation: "A CTO context packet should foreground architecture risk, operational reversibility, delivery sequencing, and security boundaries."
  source_event_id: "88888888-8888-4888-8888-888888888888"
  status: "active"
```

Project CTO lens:

```yaml
role_memory_lens:
  id: "efefefef-efef-4efe-8efe-efefefefefef"
  role_id: "cto"
  scope_type: "project"
  scope_id: "33333333-3333-4333-8333-333333333333"
  org_id: "22222222-2222-4222-8222-222222222222"
  project_id: "33333333-3333-4333-8333-333333333333"
  base_memory_fact_id: "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"
  interpretation: "For the CTO view, the SQL-first Npgsql decision should be treated as a risk-reduction move: it keeps authorization predicates visible while the schema and event provenance model are still stabilizing."
  source_event_id: "99999999-9999-4999-8999-999999999999"
  status: "active"
```

The shared CTO principle contains no Project A or Project B fact. The project CTO lens references Project A's project decision and cannot stand in for that decision if the base project fact is not readable.

## Expected Context Request

Request:

```json
{
  "principalId": "11111111-1111-4111-8111-111111111111",
  "projectId": "33333333-3333-4333-8333-333333333333",
  "roleId": "cto",
  "task": "Prepare CTO context for the next M1 implementation step."
}
```

Expected context packet content:

```yaml
context_packet:
  principal_id: "11111111-1111-4111-8111-111111111111"
  project_id: "33333333-3333-4333-8333-333333333333"
  role_id: "cto"
  memories:
    - kind: "user_preference"
      content: "Jack prefers concise decision logs with short rationale and explicit tradeoffs for technical planning."
      source_event_id: "66666666-6666-4666-8666-666666666666"
      source_link: "/api/events/66666666-6666-4666-8666-666666666666"
    - kind: "project_decision"
      content: "Long-Term Memory System uses SQL-first migrations plus raw Npgsql for the M1-M3 initial backend path."
      source_event_id: "77777777-7777-4777-8777-777777777777"
      source_link: "/api/events/77777777-7777-4777-8777-777777777777"
    - kind: "shared_cto_principle"
      content: "CTO context should foreground architecture risk, operational reversibility, delivery sequencing, and security boundaries."
      source_event_id: "88888888-8888-4888-8888-888888888888"
      source_link: "/api/events/88888888-8888-4888-8888-888888888888"
    - kind: "project_cto_lens"
      content: "For this project, the SQL-first Npgsql decision is a risk-reduction move because it keeps authorization predicates visible while schema and provenance stabilize."
      source_event_id: "99999999-9999-4999-8999-999999999999"
      source_link: "/api/events/99999999-9999-4999-8999-999999999999"
      base_memory_fact_id: "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"
```

The packet must include source links and must be built only from rows the principal can read.

## Cross-Project Non-Leakage Check

Project B has a private project decision:

```yaml
memory_fact:
  id: "eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee"
  memory_type: "decision"
  scope_type: "project"
  scope_id: "44444444-4444-4444-8444-444444444444"
  org_id: "22222222-2222-4222-8222-222222222222"
  project_id: "44444444-4444-4444-8444-444444444444"
  namespace: "/project/44444444-4444-4444-8444-444444444444/decisions"
  subject: "funding strategy"
  predicate: "uses"
  object: "confidential runway model"
  source_event_id: "12121212-1212-4121-8121-121212121212"
  status: "active"
```

When the same principal requests Project A CTO context, Project B's decision must not be retrieved, ranked, counted, summarized, source-linked, or used inside a shared CTO principle. The expected result is either no Project B row in the candidate set or an explicit authorization failure if the request directly targets Project B.

## Milestone Acceptance Expectations

M1-M3 must prove the foundational runtime and safety path:

- The schema/runtime can represent principals, organizations, projects, memberships, role assignments, grants, source events, memory facts, role lenses, chunks, idempotency keys, and outbox jobs needed by this scenario.
- User preference and project decision writes are traceable to source events or proposals.
- CTO principle and CTO lens source events are representable for later structured role-memory storage.
- Repeated event/proposal requests with the same principal, endpoint, idempotency key, and request hash return the original result.
- Durable memory write commits event, memory fact, chunk, outbox job, and broker response transactionally.
- Scope and access checks use the principal's memberships, role assignments, and grants.
- The Project A CTO request cannot read Project B memory without Project B access.

M4 must prove structured memory and role memory:

- User preference, project decision, agent-private memory, shared role principle, and project-role lens storage are structurally distinct.
- Shared CTO principle and project CTO lens writes are traceable to source events or proposals.
- The shared CTO principle can be reused without project-specific facts.
- The Project A CTO lens references Project A truth and is unavailable as a substitute when Project A truth is not readable.

M5 must prove broker intelligence:

- The broker classifies the sample events as user preference, project decision, shared role principle, project-role lens, or session-only when appropriate.
- Durable memories are stored only when supported by source evidence and write permission.
- One-off instructions stay session-only.
- Duplicate or near-duplicate preference and decision proposals are deduped, updated, ignored, or routed to review instead of blindly inserted.
- Contradictory decisions or preferences are flagged, superseded, or routed to review.

M6 must prove hybrid retrieval:

- Structured, full-text, and vector retrieval can produce the expected Project A CTO context packet.
- Retrieval predicates are permission-aware before candidate selection and ranking.
- The packet is compact, source-linked, and explainable.
- Project B memory does not appear in results, counts, ranking explanations, source links, or role summaries.
