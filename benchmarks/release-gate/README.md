# Benchmark Release Gate

MR-12 turns the benchmark suites into a release gate. A candidate release must
provide:

- a filled `llm-outcome-v0` scorecard
- a filled `agent-contract-usefulness-v1` scorecard
- the JSON output from the agent-contract tool-response smoke

The gate records Memory Lift, Contract Lift, scoped-safety leaks, stale-memory
usage, and source-link coverage. It exits nonzero when any release-blocking
check fails.

## Run With Real Benchmark Results

From the repository root:

```bash
MEMORYSYSTEM_API_BASE_URL=http://127.0.0.1:5099 \
MEMORYSYSTEM_BENCHMARK_API_KEY=private-alpha-local-key \
python3 benchmarks/agent-contract-usefulness-v1/run_smoke.py \
  --output benchmarks/outputs/agent-contract-usefulness-v1/latest-smoke.run.json

./scripts/benchmark-release-gate.sh \
  --llm-scorecard benchmarks/outputs/llm-outcome-v0/scorecard-my-run.json \
  --contract-scorecard benchmarks/outputs/agent-contract-usefulness-v1/scorecard-my-run.json \
  --agent-smoke benchmarks/outputs/agent-contract-usefulness-v1/latest-smoke.run.json
```

`private-alpha-local-key` is the committed local-demo key. Set
`MEMORYSYSTEM_BENCHMARK_API_KEY` explicitly for any non-local API.

The default reports are written to:

```text
benchmarks/outputs/release-gates/latest.json
benchmarks/outputs/release-gates/latest.md
```

Generated outputs remain ignored by git unless a report is intentionally
curated for a release note.

## Local Fixture Smoke

Use the fixture smoke to validate the gate without a running API or manual LLM
run:

```bash
./scripts/benchmark-release-gate.sh \
  --llm-scorecard benchmarks/release-gate/fixtures/llm-outcome-passing-scorecard.json \
  --contract-scorecard benchmarks/release-gate/fixtures/agent-contract-passing-scorecard.json \
  --agent-smoke benchmarks/release-gate/fixtures/agent-contract-smoke-passing.json \
  --output-dir /tmp/memorysystem-release-gate
```

The fixture with `agent-contract-smoke-failing.json` should fail the gate. Keep
that fixture so future changes prove failed smoke results stay release-blocking.

## Release Checks

The gate currently requires:

- `Memory Lift > 0`
- `Contract Lift > 0`
- scoped-safety leak count is `0`
- stale-memory usage count is `0`
- source-link coverage is `100%` with at least one memory-derived claim
- agent-contract smoke has no failed tasks
