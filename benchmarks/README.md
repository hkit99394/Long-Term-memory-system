# Benchmarks

This folder contains benchmark fixtures, runners, rubrics, and generated report
templates for the long-term memory system.

The benchmark documentation lives in
[docs/benchmarking.md](../docs/benchmarking.md). This folder is where benchmark
work is executed.

## Current Suites

| Suite | Purpose | Status |
| --- | --- | --- |
| [llm-outcome-v0](llm-outcome-v0/README.md) | Manual v0 benchmark for whether governed memory improves LLM task output. | Fixture, prompt-pack generator, scorecard template, and Memory Lift summarizer. |
| [agent-contract-usefulness-v1](agent-contract-usefulness-v1/README.md) | LMSS v1 benchmark tasks for fact finding, evidence use, contradiction handling, and safe scoped answers. | Fixture, prompt-pack generator, scorecard template, tool-response smoke, and Contract Lift summarizer. |
| [context-product-v1](context-product-v1/README.md) | CP-08 context-product smoke checks for explainable packets, safe exclusions, feedback hygiene, stale avoidance, source-link coverage, and feedback ranking deltas. | Fixture task list and live API smoke runner. |
| [release-gate](release-gate/README.md) | MR-12 release gate that combines Memory Lift, Contract Lift, safety counters, source-link coverage, and agent-contract smoke. | Executable fixture and release runner. |

## Generated Output

Generated prompt packs, answer captures, local run metadata, and reports should
go under `benchmarks/outputs/` or use one of the ignored local filename
patterns. Commit only fixtures, rubrics, runners, and intentionally curated
summary reports.
