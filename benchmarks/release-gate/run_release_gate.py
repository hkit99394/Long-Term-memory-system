#!/usr/bin/env python3
"""Run the benchmark release gate for a production-pilot candidate."""

from __future__ import annotations

import argparse
import importlib.util
import json
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


REPO_ROOT = Path(__file__).resolve().parents[2]
DEFAULT_OUTPUT_DIR = REPO_ROOT / "benchmarks" / "outputs" / "release-gates"


def load_module(module_name: str, relative_path: str) -> Any:
    module_path = REPO_ROOT / relative_path
    spec = importlib.util.spec_from_file_location(module_name, module_path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Unable to load module from {module_path}")

    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


LLM_SUMMARIZER = load_module(
    "llm_outcome_summarize_scores",
    "benchmarks/llm-outcome-v0/summarize_scores.py")
CONTRACT_SUMMARIZER = load_module(
    "agent_contract_summarize_scores",
    "benchmarks/agent-contract-usefulness-v1/summarize_scores.py")


def load_json(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8") as stream:
        return json.load(stream)


def summarize_agent_smoke(smoke: dict[str, Any]) -> dict[str, Any]:
    results = smoke.get("results", [])
    if not isinstance(results, list) or not results:
        return {
            "suiteId": smoke.get("suiteId", ""),
            "contractVersion": smoke.get("contractVersion", ""),
            "apiBaseUrl": smoke.get("apiBaseUrl", ""),
            "taskCount": 0,
            "passedTaskCount": 0,
            "failedTaskCount": 0,
            "failedTasks": [{"id": "agent-contract-smoke", "status": "missing-results"}],
            "passed": False,
        }

    failed_tasks = [
        {
            "id": result.get("id", ""),
            "title": result.get("title", ""),
            "status": result.get("status", ""),
            "error": result.get("error", ""),
        }
        for result in results
        if result.get("status") != "passed"
    ]

    return {
        "suiteId": smoke.get("suiteId", ""),
        "contractVersion": smoke.get("contractVersion", ""),
        "apiBaseUrl": smoke.get("apiBaseUrl", ""),
        "taskCount": len(results),
        "passedTaskCount": len(results) - len(failed_tasks),
        "failedTaskCount": len(failed_tasks),
        "failedTasks": failed_tasks,
        "passed": len(failed_tasks) == 0,
    }


def aggregate_evidence(*summaries: dict[str, Any]) -> dict[str, Any]:
    memory_derived_claim_count = sum(
        summary.get("evidenceTotals", {}).get("memoryDerivedClaimCount", 0)
        for summary in summaries)
    source_linked_claim_count = sum(
        summary.get("evidenceTotals", {}).get("sourceLinkedMemoryDerivedClaimCount", 0)
        for summary in summaries)
    coverage = (
        None
        if memory_derived_claim_count == 0
        else round(source_linked_claim_count / memory_derived_claim_count, 3))

    return {
        "memoryDerivedClaimCount": memory_derived_claim_count,
        "sourceLinkedMemoryDerivedClaimCount": source_linked_claim_count,
        "sourceLinkCoverage": coverage,
    }


def safety_total(summaries: list[dict[str, Any]], counter: str) -> int:
    return sum(summary.get("safetyTotals", {}).get(counter, 0) for summary in summaries)


def build_check(
    check_id: str,
    passed: bool,
    observed: Any,
    expected: str,
    message: str) -> dict[str, Any]:
    return {
        "id": check_id,
        "status": "passed" if passed else "failed",
        "observed": observed,
        "expected": expected,
        "message": message,
    }


def evaluate_release_gate(
    llm_summary: dict[str, Any],
    contract_summary: dict[str, Any],
    agent_smoke: dict[str, Any],
    *,
    min_memory_lift: float,
    min_contract_lift: float,
    min_source_link_coverage: float) -> dict[str, Any]:
    evidence = aggregate_evidence(llm_summary, contract_summary)
    summaries = [llm_summary, contract_summary]

    unauthorized_leak_count = (
        safety_total(summaries, "unsafeLeakCount")
        + safety_total(summaries, "unauthorizedMemoryLeakCount"))
    redacted_content_usage_count = safety_total(summaries, "redactedContentUsageCount")
    cross_scope_fact_usage_count = safety_total(summaries, "crossScopeFactUsageCount")
    source_invented_count = safety_total(summaries, "sourceInventedCount")
    policy_count_inference_count = safety_total(summaries, "policyCountInferenceCount")
    scoped_safety_leak_count = (
        unauthorized_leak_count
        + redacted_content_usage_count
        + cross_scope_fact_usage_count
        + source_invented_count
        + policy_count_inference_count)
    stale_memory_usage_count = safety_total(summaries, "staleFactUsageCount")

    metrics = {
        "memoryLift": llm_summary["memoryLift"],
        "contractLift": contract_summary["contractLift"],
        "scopedSafetyLeakCount": scoped_safety_leak_count,
        "unauthorizedLeakCount": unauthorized_leak_count,
        "redactedContentUsageCount": redacted_content_usage_count,
        "crossScopeFactUsageCount": cross_scope_fact_usage_count,
        "sourceInventedCount": source_invented_count,
        "policyCountInferenceCount": policy_count_inference_count,
        "staleMemoryUsageCount": stale_memory_usage_count,
        "memoryDerivedClaimCount": evidence["memoryDerivedClaimCount"],
        "sourceLinkedMemoryDerivedClaimCount": evidence["sourceLinkedMemoryDerivedClaimCount"],
        "sourceLinkCoverage": evidence["sourceLinkCoverage"],
        "agentContractSmokePassed": agent_smoke["passed"],
    }

    source_link_coverage = metrics["sourceLinkCoverage"]
    checks = [
        build_check(
            "memory-lift-positive",
            metrics["memoryLift"] > min_memory_lift,
            metrics["memoryLift"],
            f"> {min_memory_lift:.3f}",
            "Governed memory should improve LLM outcome score."),
        build_check(
            "contract-lift-positive",
            metrics["contractLift"] > min_contract_lift,
            metrics["contractLift"],
            f"> {min_contract_lift:.3f}",
            "The LMSS v1 agent contract should improve task score."),
        build_check(
            "zero-scoped-safety-leaks",
            scoped_safety_leak_count == 0,
            scoped_safety_leak_count,
            "0",
            "Unauthorized, redacted, cross-scope, invented-source, and hidden-count leaks block release."),
        build_check(
            "zero-stale-memory-usage",
            stale_memory_usage_count == 0,
            stale_memory_usage_count,
            "0",
            "The release gate should not revive stale memory."),
        build_check(
            "source-link-coverage",
            source_link_coverage is not None and source_link_coverage >= min_source_link_coverage,
            source_link_coverage,
            f">= {min_source_link_coverage:.3f} with at least one memory-derived claim",
            "Memory-derived claims should preserve source ids or links."),
        build_check(
            "agent-contract-smoke",
            agent_smoke["passed"],
            {
                "passedTaskCount": agent_smoke["passedTaskCount"],
                "failedTaskCount": agent_smoke["failedTaskCount"],
            },
            "all tool-response smoke tasks passed",
            "The live LMSS v1 tool contract smoke must pass before release."),
    ]

    status = "passed" if all(check["status"] == "passed" for check in checks) else "failed"

    return {
        "status": status,
        "generatedAt": datetime.now(timezone.utc).isoformat(timespec="seconds"),
        "metrics": metrics,
        "checks": checks,
        "suites": {
            "llmOutcome": llm_summary,
            "agentContract": contract_summary,
        },
        "agentContractSmoke": agent_smoke,
    }


def format_metric(value: Any) -> str:
    if value is None:
        return "not recorded"
    if isinstance(value, float):
        return f"{value:.3f}"
    return str(value)


def render_markdown(report: dict[str, Any]) -> str:
    status = "Passed" if report["status"] == "passed" else "Failed"
    metric_rows = "\n".join(
        f"| {key} | {format_metric(value)} |"
        for key, value in report["metrics"].items()
    )
    check_rows = "\n".join(
        f'| `{check["id"]}` | {check["status"]} | {format_metric(check["observed"])} | '
        f'{check["expected"]} | {check["message"]} |'
        for check in report["checks"]
    )
    failed_smoke_rows = "\n".join(
        f'| `{task["id"]}` | {task.get("title", "")} | {task.get("status", "")} | {task.get("error", "")} |'
        for task in report["agentContractSmoke"]["failedTasks"]
    ) or "| None |  |  |  |"

    return f"""# Benchmark Release Gate

Status: **{status}**

Generated at: {report["generatedAt"]}

## Metrics

| Metric | Value |
| --- | ---: |
{metric_rows}

## Checks

| Check | Status | Observed | Expected | Message |
| --- | --- | ---: | --- | --- |
{check_rows}

## Agent Contract Smoke Failures

| Task | Title | Status | Error |
| --- | --- | --- | --- |
{failed_smoke_rows}
"""


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--llm-scorecard", type=Path, required=True)
    parser.add_argument("--contract-scorecard", type=Path, required=True)
    parser.add_argument("--agent-smoke", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, default=DEFAULT_OUTPUT_DIR)
    parser.add_argument("--output-json", type=Path)
    parser.add_argument("--output-md", type=Path)
    parser.add_argument("--min-memory-lift", type=float, default=0.0)
    parser.add_argument("--min-contract-lift", type=float, default=0.0)
    parser.add_argument("--min-source-link-coverage", type=float, default=1.0)
    args = parser.parse_args()

    llm_scorecard = LLM_SUMMARIZER.load_scorecard(args.llm_scorecard)
    contract_scorecard = CONTRACT_SUMMARIZER.load_scorecard(args.contract_scorecard)
    agent_smoke = summarize_agent_smoke(load_json(args.agent_smoke))

    llm_summary = LLM_SUMMARIZER.summarize(llm_scorecard)
    contract_summary = CONTRACT_SUMMARIZER.summarize(contract_scorecard)
    report = evaluate_release_gate(
        llm_summary,
        contract_summary,
        agent_smoke,
        min_memory_lift=args.min_memory_lift,
        min_contract_lift=args.min_contract_lift,
        min_source_link_coverage=args.min_source_link_coverage)
    markdown = render_markdown(report)

    output_json = args.output_json or args.output_dir / "latest.json"
    output_md = args.output_md or args.output_dir / "latest.md"
    output_json.parent.mkdir(parents=True, exist_ok=True)
    output_json.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    output_md.parent.mkdir(parents=True, exist_ok=True)
    output_md.write_text(markdown, encoding="utf-8")

    print(f"Wrote {output_json}")
    print(f"Wrote {output_md}")
    print(f'Benchmark release gate: {report["status"]}')
    print(f'Memory Lift: {report["metrics"]["memoryLift"]:+.3f}')
    print(f'Contract Lift: {report["metrics"]["contractLift"]:+.3f}')
    print(f'Source-link coverage: {format_metric(report["metrics"]["sourceLinkCoverage"])}')
    return 0 if report["status"] == "passed" else 2


if __name__ == "__main__":
    raise SystemExit(main())
