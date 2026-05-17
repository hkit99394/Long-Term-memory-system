# Project Goal

Build a durable, auditable long-term memory system for AI agents that can remember useful facts, preferences, decisions, role-specific perspectives, and project history without turning memory into an uncontrolled prompt dump.

The system will use C# / ASP.NET Core as the long-term service backbone, PostgreSQL as the source of truth, pgvector for semantic recall, SQL-first migrations for explicit schema control, Obsidian-compatible Markdown for human review, TypeScript for UI and tooling, and Python only for experiments or evaluations.

The goal is not simply to store more context. The goal is to decide carefully:

- what should be remembered
- who can access it
- where truth lives
- how memories are proven
- how memory is retrieved selectively
- how stale, wrong, sensitive, or contradicted memories are corrected or removed

A successful system should let AI agents build continuity over time while remaining safe, inspectable, permission-aware, and human-correctable.

## Short Version

Build a trustworthy long-term memory layer for AI agents where Postgres stores truth, pgvector enables recall, events preserve evidence, the Memory Broker controls writes, the Context Builder controls reads, and humans can review, correct, and govern memory over time.
