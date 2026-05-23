# 0017 Structured Memory Fact Search

## Status

Accepted.

## Context

M4-05 needs a simple structured search path before M6 introduces full-text, embeddings, vector retrieval, and hybrid ranking. The repository already supports direct lookup and active scoped reads, but callers need to filter memory facts by scope, memory type, subject text, and lifecycle status without touching the chunk or embedding tables.

## Decision

Add `MemoryFactSearchQuery` and `IMemoryFactRepository.SearchAsync`.

Structured search filters on:

- resolved memory scope.
- optional memory type.
- optional case-insensitive subject substring.
- memory fact status.
- bounded result limit.

The PostgreSQL implementation searches only `memory_facts` columns. Subject matching uses a case-insensitive SQL substring predicate over `memory_facts.subject`; it does not read `memory_chunks`, full-text search vectors, embeddings, or pgvector indexes.

`FindByScopeAsync` now delegates to `SearchAsync` with no subject filter so existing scoped reads keep their behavior while sharing validation and SQL policy.

## Consequences

- M4 has a repository-level structured search primitive for exact-scope memory retrieval.
- Normal search still defaults to active facts through `MemoryFactStatuses.Active`.
- Later M6 retrieval can build full-text, vector, and hybrid ranking paths without changing this simple structured contract.
