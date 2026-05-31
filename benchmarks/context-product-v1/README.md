# Context Product Benchmark v1

## Purpose

This suite is the CP-08 benchmark smoke for the context productization gate. It
checks the product contract around context packets rather than general retrieval
quality or latency.

The smoke asserts:

- inclusion explanations and rank components are present
- safe exclusion summaries avoid hidden source ids, counts, and payloads
- source-link coverage for returned context items is 100 percent
- stale, superseded, and redacted benchmark overlay memory is not revived
- context feedback does not echo raw query text
- packet-level missing feedback produces a bounded before/after ranking delta

## Prerequisites

Seed Scenario 0001 with the benchmark overlay:

```bash
../../scripts/seed-agent-contract-benchmark-demo.sh
```

Run the API with the seeded local principal and API key:

```bash
env \
  'Authentication__ApiKey__Keys__local-jack__Key=private-alpha-local-key' \
  'Authentication__ApiKey__Keys__local-jack__PrincipalId=11111111-1111-4111-8111-111111111111' \
  'Authentication__ApiKey__Keys__local-jack__DisplayName=Jack Tam' \
  ASPNETCORE_URLS=http://127.0.0.1:5099 \
  dotnet run --project ../../src/MemorySystem.Api --no-launch-profile
```

## Run

From this folder:

```bash
MEMORYSYSTEM_API_BASE_URL=http://127.0.0.1:5099 \
MEMORYSYSTEM_BENCHMARK_API_KEY=private-alpha-local-key \
python3 run_smoke.py \
  --output ../outputs/context-product-v1/latest-smoke.run.json
```

Or use the repository wrapper:

```bash
../../scripts/context-product-benchmark-smoke.sh \
  --output benchmarks/outputs/context-product-v1/latest-smoke.run.json
```

When `--output` is omitted, the runner writes
`benchmarks/outputs/context-product-v1/latest.json`. The operations metrics
endpoint uses that file as the default source for context-product benchmark
deltas. Generated outputs are ignored by git.

## Pass Gate

The smoke fails if any benchmark task fails. A passing CP-08 run means:

- explanation coverage is 100 percent for returned context items
- source-link coverage is 100 percent for returned context items
- withheld exclusions do not disclose unsafe counts or source identifiers
- disclosed exclusion counts are aggregated by safe reason
- stale-memory usage count is 0
- feedback ranking deltas are present and bounded
