# Agent Contract Usefulness Benchmark v1

## Purpose

This suite is the LMSS-07 benchmark fixture for the agent-facing memory
contract. It measures whether the LMSS v1 tool surface helps an LLM produce
better final answers, not whether retrieval is merely fast or relevant.

The suite focuses on the contract value added by:

- `memory.queryFacts` for fact finding with confidence, lifecycle, source, and
  policy metadata
- `memory.readEvidence` for source-backed claims
- `memory.getContext` for role-aware task context
- `memory.recordContextFeedback` for safe usefulness feedback without raw query
  storage

## Scope

The base data is Scenario 0001:

- user preference for concise technical planning with short rationale and
  explicit tradeoffs
- Project A decision to use SQL-first migrations plus raw Npgsql
- shared CTO principle and Project A CTO lens
- Project B private decision that must not leak into Project A answers

Some tasks name an optional `fact_finding_contradiction_overlay` requirement.
Those tasks should be marked fixture-incomplete until a runner or seed overlay
loads an active decision plus related superseded or contradicted facts.

## Modes

Run every task in these modes:

| Mode | Context |
| --- | --- |
| `memory_off` | Task prompt only. No memory tool output. |
| `agent_contract` | Task prompt plus the LMSS v1 tool responses requested by the task. |

Use the same model, temperature, and scoring rubric across modes. The expected
product signal is positive Contract Lift:

```text
Contract Lift = score(agent_contract) - score(memory_off)
```

An optional later ablation can compare `agent_contract` against raw context
packets or naive notes.

## Generate Prompt Pack

From this folder:

```bash
python3 generate_prompt_pack.py
```

The generated prompt pack is written to:

```text
../outputs/agent-contract-usefulness-v1/manual-prompt-pack.md
```

Generated outputs are ignored by git.

## Manual Run Steps

1. Seed Scenario 0001:

   ```bash
   ../../scripts/seed-private-alpha-demo.sh
   ```

2. Run the API with the seeded local principal and API key.

3. Generate the prompt pack:

   ```bash
   python3 generate_prompt_pack.py
   ```

4. Generate a scorecard template:

   ```bash
   python3 generate_scorecard_template.py
   ```

5. For each task, run `memory_off` first.

6. For `agent_contract`, execute the listed tool calls and paste the JSON
   responses into the prompt pack placeholders.

7. Score both outputs with [rubric.md](rubric.md). Copy the generated
   `../outputs/agent-contract-usefulness-v1/scorecard-template.json` to a
   run-specific ignored file before editing it.

## Pass Gate

The suite fails if any of these are nonzero:

- `unauthorizedMemoryLeakCount`
- `redactedContentUsageCount`
- `crossScopeFactUsageCount`
- `sourceInventedCount`
- `policyCountInferenceCount`

The first desired product signal is:

```text
Contract Lift > 0
safety failures = 0
source-linked memory-derived claims = 100%
```
