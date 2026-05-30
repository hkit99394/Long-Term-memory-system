# Benchmarking Plan

## Purpose

Benchmarks should answer two different questions:

1. Is the memory system technically safe, fast, and reliable?
2. Does the memory system actually help an LLM produce better work?

The second question is the product benchmark. Retrieval quality and latency are
important, but they are intermediate signals. The headline metric should be
downstream task lift: whether the same model performs better with governed
memory than without it.

The working benchmark fixtures and runners live under
[`benchmarks/`](../benchmarks/README.md). Start with
[`benchmarks/llm-outcome-v0`](../benchmarks/llm-outcome-v0/README.md) for
general product outcome lift and
[`benchmarks/agent-contract-usefulness-v1`](../benchmarks/agent-contract-usefulness-v1/README.md)
for LMSS v1 agent-contract usefulness.

## Benchmark Layers

| Layer | Question | Primary Output |
| --- | --- | --- |
| Retrieval quality | Did the system retrieve the right memory? | Precision, recall, stale/noisy rate, source-link coverage |
| Broker write quality | Did the system remember the right things? | Durable write precision, duplicate handling, contradiction routing |
| LLM outcome quality | Did the LLM do better work with memory? | Memory Lift over no-memory and naive-notes baselines |
| Agent contract usefulness | Did the v1 tool contract help the LLM find, verify, and safely use memory? | Contract Lift over a memory-off baseline |
| Safety and governance | Did memory stay scoped, current, and auditable? | Leak count, stale fact usage, redaction failures |
| Performance | Did the system stay responsive as memory grows? | p50/p95 latency and worker throughput |
| Operations | Can operators trust the system under load and recovery? | Outbox age, dead-letter rate, backup/restore success |

## Baseline Modes

Every LLM outcome benchmark should compare at least two modes:

| Mode | Description | Purpose |
| --- | --- | --- |
| Memory off | The LLM receives only the task prompt and normal system instructions. | Establishes the baseline model capability. |
| Memory on | The LLM receives a scoped context packet from this memory system. | Measures the value of governed memory. |
| Agent contract | The LLM receives LMSS v1 tool responses such as `memory.queryFacts`, `memory.readEvidence`, and `memory.getContext`. | Measures whether the explicit contract improves factuality, evidence use, lifecycle handling, and scoped safety. |
| Naive notes | Optional mode where the LLM receives a raw or lightly curated note dump. | Tests whether governance and ranking beat simple context stuffing. |

Use the same model, temperature, task prompt, and grading rubric across modes.
Randomize or blind the mode labels when a human scores outputs.

## Headline Metric

Use Memory Lift as the top-level product metric:

```text
Memory Lift = score(memory_on) - score(memory_off)
```

When the naive-notes mode is included:

```text
Governed Memory Lift = score(memory_on) - score(naive_notes)
```

A useful benchmark report should show both the total score and the category
breakdown. A memory system that improves task success but increases unsafe
leaks should not be considered a win.

## LLM Outcome Benchmarks

### 1. Task Success

Measure whether the LLM completes realistic project tasks correctly.

Example tasks:

- Design the next migration for retrieval feedback metrics.
- Propose the next operator summary endpoint improvement.
- Write a release checklist for a private-alpha deployment.
- Explain how to add an admin console memory detail view.

Memory should help the LLM remember project constraints such as SQL-first
migrations, PostgreSQL as truth, no raw query-text storage, source-linked memory,
and the current private-alpha baseline.

Metrics:

- pass/fail against a task rubric
- number of missed project constraints
- number of wrong assumptions
- number of implementation steps that contradict accepted decisions

### 2. Preference Adherence

Measure whether the LLM follows stored user, team, or project preferences.

Example tasks:

- Write a technical plan in the expected project style.
- Choose between two implementation approaches when prior preferences exist.
- Generate a review comment that follows established tone and evidence rules.

Metrics:

- preference adherence score
- repeated-question count for facts already stored in memory
- style or process violations
- unnecessary clarification questions

### 3. Decision Consistency

Measure whether the LLM stays aligned with accepted architecture decisions.

Example tasks:

- Decide whether to switch schema management to EF Core migrations.
- Decide whether vector search should rank unauthorized rows and filter later.
- Decide whether vault Markdown can become the source of truth.

Expected behavior:

- cite or follow existing decisions
- explain tradeoffs without casually violating accepted constraints
- identify when a new decision record would be required

Metrics:

- ADR alignment score
- contradiction count
- missing-decision count
- unsupported recommendation count

### 4. Correction Handling

Measure whether the LLM stops using stale or corrected memory after review,
expiry, deletion, redaction, or supersession.

Example tasks:

- Ask for a recommendation after a prior preference has been superseded.
- Ask about a project decision after the old decision has expired.
- Ask for a summary after a memory has been deleted or redacted.

Metrics:

- outdated-memory usage rate
- corrected-fact usage rate
- stale claim count
- unsafe redacted-content usage count

This benchmark is critical because trustworthy memory must improve continuity
without freezing old mistakes into future answers.

### 5. Cross-Scope Decision Safety

Measure whether the LLM acts safely when tempting but unauthorized memory exists.

Example tasks:

- Ask a Project A planning question while Project B contains similar but private
  decisions.
- Ask a role-specific question where another role has access to different
  context.
- Ask a user-scoped preference question while organization memory contains a
  conflicting preference.

Metrics:

- leaked fact count
- cross-project recommendation count
- unauthorized source mention count
- correct uncertainty or refusal count

Target: zero unauthorized facts in final LLM output.

### 6. Human Edit Distance

Measure how much human correction is needed before the LLM output is shippable.

Metrics:

- required edit count
- severity-weighted correction count
- reviewer score: "Would I ship this?"
- time-to-acceptable-answer

This is useful when strict automatic grading is hard. It also reflects the real
product promise: memory should reduce human cleanup work.

### 7. Clarification Reduction

Measure whether memory reduces repeated questions.

Metrics:

- unnecessary clarification questions per task
- first-pass completion rate
- follow-up turns needed before a usable answer

Memory should not eliminate all clarifying questions. It should eliminate
questions whose answers are already known, current, and authorized.

### 8. Groundedness

Measure whether important project claims are backed by source-linked memory.

Metrics:

- source-backed claim rate
- unsupported project-fact count
- hallucinated project-fact count
- source-link coverage for memory-derived claims

The benchmark should distinguish general reasoning from project-specific claims.
Project-specific claims should be grounded in memory or docs.

## Agent Contract Usefulness Benchmarks

The LMSS v1 contract should improve the final answer because it gives agents
structured fact, source, lifecycle, contradiction, and policy information. This
is different from generic context-packet lift: a contract-aware agent should be
able to say what is known, why it is known, what is stale, and what must stay
unknown.

The current task fixture lives in
[`benchmarks/agent-contract-usefulness-v1`](../benchmarks/agent-contract-usefulness-v1/README.md).
It covers:

- direct fact finding through `memory.queryFacts`
- evidence-backed rationale through `memory.readEvidence`
- contradiction and lifecycle handling when an overlay fixture provides stale
  or superseded facts
- Project A answers that do not leak Project B private memory
- role-targeted CTO guidance through `memory.getContext`
- policy-aware abstention when no authorized fact is returned
- source-backed preference use
- safe retrieval feedback call generation

Use Contract Lift as the top-level metric:

```text
Contract Lift = score(agent_contract) - score(memory_off)
```

Safety is a gate. A run fails if it leaks unauthorized memory, reveals withheld
counts, invents source ids, uses redacted content, or treats inactive memory as
current. Memory-derived claims should preserve source ids or source links when
the contract returns them.

## Technical Benchmarks

### Retrieval Quality

Use Scenario 0001 plus additional golden scenarios.

Metrics:

- context packet precision: useful returned memories divided by returned memories
- recall: expected memories returned divided by expected memories
- cross-scope leakage count
- stale or noisy memory rate
- source-link coverage
- compactness against item and token budgets

Initial targets:

- precision >= 0.85
- recall >= 0.80
- cross-scope leaks = 0
- source-link coverage = 100%

### Broker Write Quality

Measure whether the Memory Broker stores, rejects, reuses, or routes candidates
correctly.

Metrics:

- durable write precision
- false durable memory rate
- exact duplicate reuse rate
- similar-memory review routing rate
- contradiction review routing rate
- session-only rejection correctness

Initial targets:

- session-only false storage = 0
- exact duplicate creates new memory = 0
- contradiction routed to review >= 0.90
- durable write precision >= 0.90

### API and Retrieval Performance

Run performance checks against seeded sizes:

- 1k memory facts and chunks
- 10k memory facts and chunks
- 100k memory facts and chunks

Track p50 and p95 latency for:

- `POST /api/events`
- `POST /api/memory/proposals`
- `GET /api/memory/search`
- `GET /api/memory/search/semantic`
- `GET /api/memory/search/hybrid`
- `GET /api/memory/context`
- `GET /api/operations/summary`

Initial private-alpha targets:

- event append p95 < 150 ms
- stored proposal p95 < 300 ms
- full-text search p95 at 10k chunks < 300 ms
- hybrid/context packet p95 at 10k chunks < 750 ms
- operations summary p95 < 250 ms

At 100k rows, exact pgvector search may be slower. Use the benchmark to decide
when a model-specific ANN index becomes necessary.

### Worker and Outbox

Metrics:

- indexing jobs per second
- failed job rate
- retry success rate
- lease expiry recovery
- oldest ready job age
- worker heartbeat freshness

Initial targets:

- dead-letter rate < 0.1%
- duplicate completed indexing jobs = 0
- stale worker heartbeat during normal run = 0
- oldest ready outbox age under light load < 60 seconds

### Governance and Safety

Metrics:

- unauthorized read attempts blocked
- redacted, deleted, expired, superseded, and contradicted memory excluded from
  normal retrieval
- raw query text absent from retrieval feedback storage
- raw memory text absent from operational logs
- backup/restore smoke success
- ephemeral retention minimization correctness

Target: zero tolerance for safety regressions.

## Scenario Suite

Start with 20 benchmark tasks across 5-10 scenarios:

| Scenario Type | Example |
| --- | --- |
| User preference | The user prefers concise release notes and SQL-first migrations. |
| Project decision | Project A chose direct Npgsql for core memory queries. |
| Role lens | CTO context emphasizes reversibility and operational risk. |
| Correction | A prior decision is superseded or expired. |
| Cross-scope trap | Project B has similar but unauthorized memory. |
| Sensitive data | A memory or source event is redacted and must not be used. |

Each scenario should define:

- seed data
- task prompts
- expected useful memory
- forbidden memory
- scoring rubric
- safety assertions
- expected source links

## Scoring Rubric

Use a 0-5 score per category:

| Category | Meaning |
| --- | --- |
| Task success | Did the answer solve the requested task? |
| Decision alignment | Did it respect accepted project decisions? |
| Preference adherence | Did it follow known user/project preferences? |
| Corrected-memory handling | Did it use current facts and avoid stale ones? |
| Groundedness | Were project-specific claims supported? |
| Safety | Did it avoid unauthorized, redacted, or sensitive memory? |
| Human edit burden | How much correction was needed before shipping? |

Safety should be a gate, not just an average. A run with a serious leak should
fail even if other categories score well.

## Report Shape

A benchmark run should produce machine-readable JSON and a short Markdown
summary:

```json
{
  "runId": "2026-05-29T180000Z",
  "model": "example-model",
  "scenarioCount": 10,
  "taskCount": 20,
  "modes": {
    "memory_off": {
      "overallScore": 0.62
    },
    "memory_on": {
      "overallScore": 0.78
    },
    "naive_notes": {
      "overallScore": 0.70
    }
  },
  "memoryLift": 0.16,
  "governedMemoryLift": 0.08,
  "unsafeLeakCount": 0,
  "staleFactUsageRate": 0.02,
  "unnecessaryClarificationRate": 0.10
}
```

The Markdown summary should include:

- pass/fail status
- Memory Lift
- governed-memory lift over naive notes
- safety gate status
- strongest and weakest categories
- top regressions
- links to scenario results

## First Implementation Slice

Start with an LLM outcome benchmark before building a broad performance suite:

1. Add 20 benchmark tasks across Scenario 0001 and 4-5 new golden scenarios.
2. Run each task in memory-off and memory-on modes.
3. Add naive-notes mode only after the first two modes are stable.
4. Score decision consistency, preference adherence, correction handling,
   groundedness, clarification reduction, and safety.
5. Store results as JSON plus a Markdown summary under a generated benchmark
   output directory that is excluded from git unless a report is intentionally
   committed.

The first product target should be positive Memory Lift with zero safety
regressions:

```text
Memory Lift > 0
unsafe_leak_count = 0
redacted_content_usage_count = 0
cross_scope_fact_usage_count = 0
```

## MR-12 Benchmark Release Gate

The Middle Run release gate is executable from
`benchmarks/release-gate/run_release_gate.py` or the repository wrapper:

```bash
./scripts/benchmark-release-gate.sh \
  --llm-scorecard benchmarks/outputs/llm-outcome-v0/scorecard-my-run.json \
  --contract-scorecard benchmarks/outputs/agent-contract-usefulness-v1/scorecard-my-run.json \
  --agent-smoke benchmarks/outputs/agent-contract-usefulness-v1/latest-smoke.run.json
```

The gate writes JSON and Markdown reports under
`benchmarks/outputs/release-gates/` by default. A release candidate fails when
Memory Lift or Contract Lift is not positive, scoped-safety leak count is
nonzero, stale-memory usage is nonzero, source-link coverage is below 100%, or
the agent-contract smoke has failed tasks.

Use the fixture command in
`benchmarks/release-gate/README.md` to verify the gate without a running API.

The first local baseline is recorded in
[`docs/benchmark-release-gate-lr03.md`](benchmark-release-gate-lr03.md).
Generated raw reports remain under `benchmarks/outputs/`, which is ignored by
git unless a specific artifact is intentionally promoted.

After that baseline exists, add larger retrieval-quality, latency, worker, and
backup/recovery benchmark runs.
