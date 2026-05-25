# Documentation Index

## Brief Summary

This project is a long-term memory system for AI agents. Its purpose is to store durable memory safely, retrieve only relevant memory for each task, preserve evidence for every memory, and give humans a way to review, correct, expire, or delete what the system remembers.

The chosen long-term stack is C# / ASP.NET Core, PostgreSQL, pgvector, SQL-first migrations, TypeScript for UI and tooling, and Python only for experiments or evaluations.

## Document Index

| Document | Purpose |
| --- | --- |
| [Project Goal](project-goal.md) | Defines the north star for the project: trustworthy, auditable, permission-aware long-term memory for AI agents. |
| [Architecture Overview](architecture.md) | Provides the short component, write-path, read-path, trust, and MVP architecture guide. |
| [Long-Term AI Memory System Plan](long-term-memory-system-plan.md) | Describes the architecture, stack, schema direction, API surface, memory broker, context builder, phases, risks, and first build steps. |
| [Folder Structure](folder-structure.md) | Defines the target repository layout, ownership boundaries, and where new code, tests, migrations, tools, and docs should live. |
| [Testing Commands](testing.md) | Lists local and CI-ready restore, build, test, and database-backed integration commands. |
| [Roadmap](roadmap.md) | Breaks the architecture plan into delivery milestones, dependencies, decision gates, and first build sequence. |
| [Backlog](backlog.md) | Lists actionable work items by milestone with priorities, statuses, and acceptance criteria. |
| [Scenario 0001: User Preference, Project Decision, and CTO Context](scenarios/0001-user-preference-project-decision-cto-context.md) | Defines the first M1-M6 implementation throughline and sample data. |
| [Decision 0001: Data-Access Approach](decisions/0001-data-access-approach.md) | Records the initial backend data-access choice for the M1-M3 path. |
| [Decision 0002: Migration Runner Approach](decisions/0002-migration-runner-approach.md) | Records the initial SQL migration runner choice for local development and integration tests. |
| [Decision 0003: Local Database Runtime](decisions/0003-local-database-runtime.md) | Records the pinned PostgreSQL plus pgvector Docker image for local development and integration tests. |
| [Decision 0004: API Idempotency Request Hash](decisions/0004-api-idempotency-request-hash.md) | Records the M2 idempotency key scope, request hash rule, retry behavior, and expiry default. |
| [Decision 0005: Event Append Contract](decisions/0005-event-append-contract.md) | Records the M2 `POST /api/events` request shape, scope mapping, inline payload storage, and retry behavior. |
| [Decision 0006: Minimal Memory Proposal Broker](decisions/0006-minimal-memory-proposal-broker.md) | Records the M2 `POST /api/memory/proposals` contract and deterministic broker decision rules. |
| [Decision 0007: Transactional Memory Proposal Write](decisions/0007-transactional-memory-proposal-write.md) | Records the M2 stored proposal transaction that creates memory facts, chunks, outbox jobs, and idempotent responses. |
| [Decision 0008: Scope Resolver Contract](decisions/0008-scope-resolver-contract.md) | Records the M3 scope resolver boundary for global, org, project, user, role, agent, and session requests. |
| [Decision 0009: Membership and Grant Access Checks](decisions/0009-membership-and-grant-access-checks.md) | Records the M3 access policy for memberships, role assignments, namespace grants, and write-path enforcement. |
| [Decision 0010: Memory Fact Scope Consistency](decisions/0010-memory-fact-scope-consistency.md) | Records the M3 database constraint that keeps memory fact scope ids, namespace prefixes, and owner columns aligned. |
| [Decision 0011: Direct Memory Fact Read Access](decisions/0011-direct-memory-fact-read-access.md) | Records the M3 direct memory fact read endpoint and blocked cross-project read behavior. |
| [Decision 0012: Memory Namespace Parser](decisions/0012-memory-namespace-parser.md) | Records the M3 parser for turning namespace strings into canonical scope metadata. |
| [Decision 0013: Memory Facts Repository](decisions/0013-memory-facts-repository.md) | Records the M4 repository boundary for storing and retrieving structured memory facts. |
| [Decision 0014: Memory Status Lifecycle](decisions/0014-memory-status-lifecycle.md) | Records the M4 memory fact lifecycle states and default active-only retrieval policy. |
| [Decision 0015: Role Memory Lens Repository](decisions/0015-role-memory-lens-repository.md) | Records the M4 repository boundary for shared role principles and project-role lenses. |
| [Decision 0016: Role Lens Base Fact Validation](decisions/0016-role-lens-base-fact-validation.md) | Records the M4 validation rules connecting role lenses to allowed base memory fact scopes. |
| [Decision 0017: Structured Memory Fact Search](decisions/0017-structured-memory-fact-search.md) | Records the M4 repository search path for filtering memory facts by scope, type, subject, and status without vector retrieval. |
| [Decision 0018: Memory Candidate Classification](decisions/0018-memory-candidate-classification.md) | Records the M5 broker classification kinds returned with memory proposal decisions. |
| [Decision 0019: Session-Only Task Instructions](decisions/0019-session-only-task-instructions.md) | Records the M5 broker rule that keeps one-off task instructions out of durable memory. |
| [Decision 0020: Similar Active Memory Deduplication](decisions/0020-similar-active-memory-deduplication.md) | Records the M5 workflow rule that sends similar active memory proposals to review instead of blindly inserting duplicates. |
| [Decision 0021: Memory Proposal Contradiction Detection](decisions/0021-memory-proposal-contradiction-detection.md) | Records the M5 workflow rule that sends clear conflicts with active memory to review before writing another durable fact. |
| [Decision 0022: Memory Proposal Confidence Scoring](decisions/0022-memory-proposal-confidence-scoring.md) | Records the M5 broker confidence scoring table and review threshold for durable proposal decisions. |
| [Decision 0023: Authorized Full-Text Memory Search](decisions/0023-authorized-full-text-memory-search.md) | Records the M6 full-text search path over `memory_chunks.search_vector` with authorization predicates inside SQL. |
| [Decision 0024: Embedding Provider Adapter](decisions/0024-embedding-provider-adapter.md) | Records the M6 embedding provider contract, deterministic local adapter, selected model/dimension configuration, and idempotent embedding storage path. |
| [Decision 0025: Authorized pgvector Semantic Search](decisions/0025-authorized-pgvector-semantic-search.md) | Records the M6 semantic search endpoint, cosine distance operator, authorization-before-ranking query shape, and deferred ANN index choice. |
| [Decision 0026: Hybrid Memory Ranking](decisions/0026-hybrid-memory-ranking.md) | Records the M6 hybrid search endpoint and final-score formula combining relevance, confidence, recency, authority, and scope match. |
| [Decision 0027: Context Packet Builder](decisions/0027-context-packet-builder.md) | Records the M6 context packet endpoint, packet grouping, compactness limit, source links, and ranking explanation fields. |
| [Decision 0028: Retrieval Evaluation Tests](decisions/0028-retrieval-evaluation-tests.md) | Records the M6 retrieval evaluation scorecard for relevance, compactness, write precision, false positives, and contradiction quality. |
| [Decision 0029: Pending Review API](decisions/0029-pending-review-api.md) | Records the M7 pending review queue endpoint and review-permission filtering rule. |
| [Decision 0030: Review Dashboard](decisions/0030-review-dashboard.md) | Records the M7 TypeScript review dashboard and review action endpoint behavior. |
| [Decision 0031: Obsidian Export](decisions/0031-obsidian-export.md) | Records the M7 Obsidian export endpoint, Markdown source metadata, and vault-sync tool behavior. |
| [Decision 0032: Stale Vault Exports](decisions/0032-stale-vault-exports.md) | Records the M7 stale marker workflow for deleted, redacted, expired, superseded, and contradicted vault exports. |
| [Decision 0033: Archive Vault Exports](decisions/0033-archive-vault-exports.md) | Records the M7 archive export endpoint for readable superseded, expired, and contradicted memory. |
| [Decision 0034: Operational Health Checks](decisions/0034-operational-health-checks.md) | Records the M8 API liveness/readiness split, worker heartbeat freshness check, and embedding provider health behavior. |
| [Decision 0035: Structured Operational Logging](decisions/0035-structured-operational-logging.md) | Records the M8 structured logging contract for proposal, retrieval, review, and redaction decision points without payload leakage. |

## Dictionary

| Term | Meaning |
| --- | --- |
| Agent | An AI process or role that can use memory to complete tasks. |
| Agent-private memory | Memory scoped to one agent and not automatically shared with other agents. |
| Archive export | A readable Markdown projection of inactive but non-redacted memory, such as superseded, expired, or contradicted memory. |
| Candidate kind | The broker's classification for a proposed memory, such as preference, project fact, decision, role lens, agent-private memory, or session-only instruction. |
| Confidence score | The broker-assigned effective confidence used for review and storage decisions. Request confidence is capped or defaulted according to source trust level. |
| Conflicting active memory | An active memory fact in the same scope and memory type with the same normalized subject and predicate, a different object, and a deterministic contradiction such as enabled/disabled or use/do-not-use. |
| Context Builder | The read-control component that retrieves, filters, ranks, and compresses relevant memory before an LLM call. |
| Context packet | A compact, source-linked, explainable memory bundle built from authorized hybrid retrieval results. |
| Durable memory | Memory intended to persist beyond the current session or task. |
| Embedding | A vector representation of text used for semantic similarity search. |
| Event log | Append-only evidence of raw user messages, assistant messages, tool calls, and memory changes. |
| Full-text memory search | Keyword retrieval over `memory_chunks.search_vector` using PostgreSQL full-text search, with scope and namespace authorization predicates applied before ranking. |
| Hybrid memory search | Retrieval that combines full-text and semantic relevance with confidence, recency, authority, and scope-match scores. |
| Memory Broker | The write-control component that decides whether proposed memory should be stored, rejected, reviewed, expired, or treated as session-only. |
| Memory fact | A structured memory record stored in PostgreSQL with scope, provenance, confidence, status, and lifecycle metadata. |
| Memory facts repository | The application data-access boundary for storing and querying structured memory facts by id or resolved scope. |
| Memory grant | A permission record that allows a principal or assigned role to read, write, review, or administer a namespace prefix. |
| Memory owner columns | Typed columns such as `org_id`, `project_id`, `user_principal_id`, `role_id`, and `agent_principal_id` that mirror a memory fact's canonical scope. |
| Memory read service | The application service that loads a memory fact and returns it only after the caller has read access to its scope and namespace. |
| Memory status lifecycle | The supported memory fact states: active, tentative, superseded, contradicted, expired, deleted, and redacted. Normal retrieval includes active facts only. |
| Namespace | A parsed path-like scope used to separate global, organization, user, project, role, agent, and session memory. |
| Obsidian export | The authorized projection of approved decision and summary memory into source-linked Markdown documents for the vault. |
| Obsidian vault | The human-readable Markdown workspace used for notes, summaries, decisions, and review exports. |
| One-off task instruction | A short-lived instruction for the current answer, request, task, temporary file, or bug. The broker treats it as session-only rather than durable memory. |
| Outbox job | A retry-safe background work item used for embedding, indexing, export, review, expiry, redaction, or summary generation. |
| Pending review | A `memory_reviews` row with `pending` status that points at a memory fact awaiting human review. |
| pgvector | PostgreSQL extension used to store and search vector embeddings. |
| Postgres truth | The rule that PostgreSQL is the authoritative source for structured memory. |
| Principal | A human, agent, or service account making a request to the memory system. |
| Project-role lens | A role-specific interpretation of one project's truth, such as the CTO perspective on a specific project decision. |
| Provenance | Evidence showing where a memory came from, usually through a source event. |
| Redaction | Removal or masking of sensitive content from facts, chunks, exports, and event payloads where policy requires erasure. |
| Review dashboard | The TypeScript UI for listing pending memory reviews and completing approve, reject, edit, expire, delete, or supersede actions. |
| Role lens | A role-specific interpretation of shared truth, such as CTO, CFO, COO, CEO, Designer, or Developer perspective. |
| Role lens base fact | The memory fact that a role lens interprets. Its scope must match the lens type: global for global role lenses, same organization for organization role lenses, or target project/same organization for project-role lenses. |
| Role memory lens repository | The application data-access boundary for storing and querying shared role principles and project-role lenses. |
| Retrieval evaluation | A deterministic scorecard over context-packet items and write observations that measures relevance, compactness, write precision, false positives, and contradiction quality. |
| Semantic memory search | Vector retrieval over `memory_embeddings` using pgvector cosine distance, with authorized chunk filtering applied before distance ranking. |
| Semantic recall | Retrieval by meaning rather than exact keyword match, usually through vector search. |
| Session memory | Temporary memory for the current task or conversation only. |
| Similar active memory | An active memory fact in the same scope and memory type with the same normalized subject and predicate but a different object. |
| Structured memory search | A repository query over structured `memory_facts` columns such as scope, memory type, subject, and status. It does not use chunks, full-text search, embeddings, or vectors. |
| Stale vault export | A Markdown marker that replaces an exported vault note when the authoritative memory fact is no longer active. |
| Supersession | The process of replacing an outdated or contradicted memory with a newer memory while preserving audit history. |
| Trust level | Metadata that separates trusted system or human-approved content from user-scoped, agent-private, tool, web, or retrieved content. |
| Vector index | Search index used for semantic recall. It is not the source of truth. |
