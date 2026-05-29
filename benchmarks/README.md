# Benchmarks

This folder contains benchmark fixtures, runners, rubrics, and generated report
templates for the long-term memory system.

The benchmark documentation lives in
[docs/benchmarking.md](../docs/benchmarking.md). This folder is where benchmark
work is executed.

## Current Suites

| Suite | Purpose | Status |
| --- | --- | --- |
| [llm-outcome-v0](llm-outcome-v0/README.md) | Manual v0 benchmark for whether governed memory improves LLM task output. | Initial fixture and prompt-pack generator. |
| [agent-contract-usefulness-v1](agent-contract-usefulness-v1/README.md) | LMSS v1 benchmark tasks for fact finding, evidence use, contradiction handling, and safe scoped answers. | Initial fixture and prompt-pack generator. |

## Generated Output

Generated prompt packs, answer captures, local run metadata, and reports should
go under `benchmarks/outputs/` or use one of the ignored local filename
patterns. Commit only fixtures, rubrics, runners, and intentionally curated
summary reports.
