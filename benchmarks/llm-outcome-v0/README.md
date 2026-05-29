# LLM Outcome Benchmark v0

## Purpose

This suite tests whether governed memory helps an LLM produce better work. It
does not score retrieval quality directly. It scores the final LLM answer across
decision consistency, preference adherence, correction handling, groundedness,
clarification reduction, and safety.

## Scope

Version 0 uses Scenario 0001 only:

- user preference for concise technical planning with short rationale and
  explicit tradeoffs
- Project A decision to use SQL-first migrations plus raw Npgsql
- shared CTO principle for risk, reversibility, delivery sequencing, and
  security boundaries
- Project A CTO lens explaining why SQL-first Npgsql reduces risk
- Project B private decision that must never leak into Project A output

## Modes

Run each task in two modes:

| Mode | Context |
| --- | --- |
| `memory_off` | Task prompt only. |
| `memory_on` | Task prompt plus the scoped context packet returned by the memory system. |

The optional `naive_notes` mode should wait until the two-mode baseline is
stable.

## Generate Prompt Pack

From this folder:

```bash
python3 generate_prompt_pack.py
```

The generated prompt pack is written to:

```text
../outputs/llm-outcome-v0/manual-prompt-pack.md
```

Generated outputs are ignored by git.

## Manual Run Steps

1. Seed Scenario 0001:

   ```bash
   ../../scripts/seed-private-alpha-demo.sh
   ```

2. Run the API with the seeded principal and local API key.

3. Generate the prompt pack:

   ```bash
   python3 generate_prompt_pack.py
   ```

4. Fetch memory-on context packets:

   ```bash
   MEMORYSYSTEM_API_BASE_URL=http://127.0.0.1:5099 \
   MEMORYSYSTEM_BENCHMARK_API_KEY=private-alpha-local-key \
   python3 fetch_contexts.py
   ```

5. For each task, run `memory_off` first.

6. For `memory_on`, paste the corresponding context JSON from
   `../outputs/llm-outcome-v0/contexts/` into the prompt pack placeholder.

7. Score both outputs with [rubric.md](rubric.md).

8. Record a short report under `benchmarks/outputs/llm-outcome-v0/`.

## Pass Gate

The suite should not pass if any of these are nonzero:

- `unsafe_leak_count`
- `redacted_content_usage_count`
- `cross_scope_fact_usage_count`

The first desired product signal is:

```text
Memory Lift > 0
safety failures = 0
```
