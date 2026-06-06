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

Task ACU-003 requires the `fact_finding_contradiction_overlay` fixture. Load the
full benchmark seed when running the complete smoke so the active Project A
decision has a related superseded fact and a redacted excluded fact.

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

## Tool-Response Smoke

Seed Scenario 0001 with the benchmark overlay:

```bash
../../scripts/seed-agent-contract-benchmark-demo.sh
```

Run the API with the seeded local principal and API key:

```bash
env \
  'Authentication__ApiKey__Keys__local_demo__Key=private-alpha-local-key' \
  'Authentication__ApiKey__Keys__local_demo__PrincipalId=11111111-1111-4111-8111-111111111111' \
  'Authentication__ApiKey__Keys__local_demo__DisplayName=Local Demo User' \
  ASPNETCORE_URLS=http://127.0.0.1:5099 \
  dotnet run --project ../../src/MemorySystem.Api --no-launch-profile
```

`private-alpha-local-key` is the committed local-demo key. Set
`MEMORYSYSTEM_BENCHMARK_API_KEY` explicitly for any non-local API.

Run all eight benchmark task tool calls, including ACU-003:

```bash
MEMORYSYSTEM_API_BASE_URL=http://127.0.0.1:5099 \
MEMORYSYSTEM_BENCHMARK_API_KEY=private-alpha-local-key \
python3 run_smoke.py
```

The smoke checks governed tool responses only. It does not replace the manual
LLM answer scoring in the prompt pack and scorecard.

## Manual Run Steps

1. Seed Scenario 0001 with the benchmark overlay:

   ```bash
   ../../scripts/seed-agent-contract-benchmark-demo.sh
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

8. Summarize the scorecard:

   ```bash
   python3 summarize_scores.py \
     --scorecard ../outputs/agent-contract-usefulness-v1/scorecard-my-run.json
   ```

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

MR-12 combines this summary with the LLM outcome scorecard and the tool-response
smoke output through [../release-gate](../release-gate/README.md).
