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

## Dictionary

| Term | Meaning |
| --- | --- |
| Agent | An AI process or role that can use memory to complete tasks. |
| Agent-private memory | Memory scoped to one agent and not automatically shared with other agents. |
| Context Builder | The read-control component that retrieves, filters, ranks, and compresses relevant memory before an LLM call. |
| Durable memory | Memory intended to persist beyond the current session or task. |
| Embedding | A vector representation of text used for semantic similarity search. |
| Event log | Append-only evidence of raw user messages, assistant messages, tool calls, and memory changes. |
| Memory Broker | The write-control component that decides whether proposed memory should be stored, rejected, reviewed, expired, or treated as session-only. |
| Memory fact | A structured memory record stored in PostgreSQL with scope, provenance, confidence, status, and lifecycle metadata. |
| Namespace | A path-like scope used to separate global, organization, user, project, role, agent, and session memory. |
| Obsidian vault | The human-readable Markdown workspace used for notes, summaries, decisions, and review exports. |
| Outbox job | A retry-safe background work item used for embedding, indexing, export, review, expiry, redaction, or summary generation. |
| pgvector | PostgreSQL extension used to store and search vector embeddings. |
| Postgres truth | The rule that PostgreSQL is the authoritative source for structured memory. |
| Principal | A human, agent, or service account making a request to the memory system. |
| Project-role lens | A role-specific interpretation of one project's truth, such as the CTO perspective on a specific project decision. |
| Provenance | Evidence showing where a memory came from, usually through a source event. |
| Redaction | Removal or masking of sensitive content from facts, chunks, exports, and event payloads where policy requires erasure. |
| Role lens | A role-specific interpretation of shared truth, such as CTO, CFO, COO, CEO, Designer, or Developer perspective. |
| Semantic recall | Retrieval by meaning rather than exact keyword match, usually through vector search. |
| Session memory | Temporary memory for the current task or conversation only. |
| Supersession | The process of replacing an outdated or contradicted memory with a newer memory while preserving audit history. |
| Trust level | Metadata that separates trusted system or human-approved content from user-scoped, agent-private, tool, web, or retrieved content. |
| Vector index | Search index used for semantic recall. It is not the source of truth. |
