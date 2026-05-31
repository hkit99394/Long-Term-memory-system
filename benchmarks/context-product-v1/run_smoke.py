#!/usr/bin/env python3
"""Run the context-product benchmark smoke checks for CP-08."""

from __future__ import annotations

import argparse
import json
import os
import sys
import urllib.error
import urllib.parse
import urllib.request
import uuid
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


PROJECT_A_DECISION_FACT_ID = "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"
PROJECT_B_DECISION_FACT_ID = "eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee"
SUPERSEDED_OVERLAY_FACT_ID = "f1f1f1f1-f1f1-4f1f-8f1f-f1f1f1f1f1f1"
REDACTED_OVERLAY_FACT_ID = "f2f2f2f2-f2f2-4f2f-8f2f-f2f2f2f2f2f2"
PROJECT_A_DECISION_EVENT_ID = "77777777-7777-4777-8777-777777777777"
CONTEXT_ITEM_GROUPS = [
    "userPreferences",
    "projectMemory",
    "roleMemory",
    "relevantDecisions",
]
REQUIRED_RANK_COMPONENTS = [
    "relevance",
    "confidence",
    "recency",
    "authority",
    "scopeMatch",
    "feedbackAdjustment",
]
REQUIRED_REVIEW_ACTIONS = {
    "useful",
    "stale",
    "wrong",
    "sensitive",
    "over_broad",
}


class SmokeFailure(Exception):
    """Raised when an expected context-product signal is missing."""


def load_tasks(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8") as stream:
        return json.load(stream)


def apply_templates(value: Any, replacements: dict[str, str]) -> Any:
    if isinstance(value, str):
        rendered = value
        for key, replacement in replacements.items():
            rendered = rendered.replace("{{" + key + "}}", replacement)
        return rendered

    if isinstance(value, list):
        return [apply_templates(item, replacements) for item in value]

    if isinstance(value, dict):
        return {
            key: apply_templates(item, replacements)
            for key, item in value.items()
        }

    return value


def execute_http_call(base_url: str, api_key: str, http: dict[str, Any]) -> dict[str, Any]:
    method = http["method"].upper()
    path = http["path"]
    headers = {
        "Accept": "application/json",
        "X-Api-Key": api_key,
    }

    if method == "GET":
        query = http.get("query") or {}
        encoded_query = urllib.parse.urlencode(query)
        url = f"{base_url}{path}"
        if encoded_query:
            url = f"{url}?{encoded_query}"
        data = None
    elif method == "POST":
        url = f"{base_url}{path}"
        data = json.dumps(http.get("body", {})).encode("utf-8")
        headers["Content-Type"] = "application/json"
    else:
        raise SmokeFailure(f"Unsupported HTTP method: {method}")

    request = urllib.request.Request(url, data=data, headers=headers, method=method)

    try:
        with urllib.request.urlopen(request, timeout=20) as response:
            body = response.read().decode("utf-8")
            status = response.status
    except urllib.error.HTTPError as error:
        body = error.read().decode("utf-8", errors="replace")
        raise SmokeFailure(f"{method} {path} returned HTTP {error.code}: {body}") from error
    except urllib.error.URLError as error:
        raise SmokeFailure(f"{method} {path} failed: {error.reason}") from error

    if status < 200 or status >= 300:
        raise SmokeFailure(f"{method} {path} returned HTTP {status}: {body}")

    try:
        return json.loads(body)
    except json.JSONDecodeError as error:
        raise SmokeFailure(f"{method} {path} returned non-JSON body: {body}") from error


def json_text(value: Any) -> str:
    return json.dumps(value, sort_keys=True)


def lower_json_text(value: Any) -> str:
    return json_text(value).lower()


def require(condition: bool, message: str) -> None:
    if not condition:
        raise SmokeFailure(message)


def context_items(payload: dict[str, Any]) -> list[dict[str, Any]]:
    items: list[dict[str, Any]] = []
    for group_name in CONTEXT_ITEM_GROUPS:
        items.extend(payload.get(group_name) or [])
    return items


def facts(payload: dict[str, Any]) -> list[dict[str, Any]]:
    return list(payload.get("facts") or [])


def contradictions(payload: dict[str, Any]) -> list[dict[str, Any]]:
    return list(payload.get("contradictions") or [])


def excluded(payload: dict[str, Any]) -> list[dict[str, Any]]:
    return list(payload.get("excluded") or [])


def require_number(value: Any, message: str) -> float:
    require(not isinstance(value, bool) and isinstance(value, (int, float)), message)
    return float(value)


def item_by_source_id(payload: dict[str, Any], source_id: str) -> dict[str, Any]:
    for item in context_items(payload):
        if item.get("sourceId") == source_id:
            return item
    raise SmokeFailure(f"Context packet did not include source id {source_id}.")


def rank_components(item: dict[str, Any]) -> dict[str, Any]:
    explanation = item.get("explanation") or {}
    components = explanation.get("components") or {}
    for component in REQUIRED_RANK_COMPONENTS:
        require_number(
            components.get(component),
            f"Missing numeric rank component {component} for source {item.get('sourceId')}.")
    return components


def feedback_adjustment(item: dict[str, Any]) -> float:
    return require_number(
        rank_components(item).get("feedbackAdjustment"),
        f"Missing feedbackAdjustment for source {item.get('sourceId')}.")


def rank(item: dict[str, Any]) -> float:
    return require_number(item.get("rank"), f"Missing numeric rank for source {item.get('sourceId')}.")


def validate_context_packet_product(payload: dict[str, Any]) -> dict[str, Any]:
    items = context_items(payload)
    require(items, "Context packet returned no items.")
    item_by_source_id(payload, PROJECT_A_DECISION_FACT_ID)

    source_linked_count = 0
    explained_count = 0

    for item in items:
        require(item.get("itemId"), "Context item is missing itemId.")
        require(item.get("sourceId"), "Context item is missing sourceId.")
        require(item.get("sourceType") in {"memory_fact", "role_memory_lens"}, "Unexpected sourceType.")
        require(item.get("sourceEventId"), "Context item is missing sourceEventId.")
        require(str(item.get("sourceLink", "")).startswith("/api/events/"), "Context item is missing sourceLink.")
        source_linked_count += 1

        explanation = item.get("explanation") or {}
        require(explanation.get("primaryReason"), "Context item is missing explanation.primaryReason.")
        require(explanation.get("matchedSignals"), "Context item is missing explanation.matchedSignals.")
        rank_components(item)
        review_actions = set(explanation.get("reviewSuggestedActions") or [])
        require(
            REQUIRED_REVIEW_ACTIONS.issubset(review_actions),
            "Context item is missing one or more product review actions.")
        explained_count += 1

    exclusions = excluded(payload)
    require(
        any(
            exclusion.get("reason") == "not_authorized"
            and exclusion.get("countDisclosure") == "withheld"
            and exclusion.get("count") is None
            for exclusion in exclusions),
        "Missing withheld not_authorized exclusion summary.")
    require(
        any(
            exclusion.get("reason") == "sensitive"
            and exclusion.get("countDisclosure") == "withheld"
            and exclusion.get("count") is None
            for exclusion in exclusions),
        "Missing withheld sensitive exclusion summary.")

    response_text = lower_json_text(payload)
    require(PROJECT_B_DECISION_FACT_ID not in response_text, "Project B source id leaked in context packet.")
    require("confidential runway model" not in response_text, "Project B confidential content leaked in context packet.")

    coverage = round(source_linked_count / len(items), 3)
    explanation_coverage = round(explained_count / len(items), 3)

    safe_exclusion_counts: dict[str, int] = {}
    for exclusion in exclusions:
        reason = exclusion.get("reason")
        if not isinstance(reason, str):
            continue

        safe_exclusion_counts[reason] = safe_exclusion_counts.get(reason, 0) + int(exclusion.get("count") or 0)

    return {
        "includedItemCount": len(items),
        "sourceLinkedItemCount": source_linked_count,
        "sourceLinkCoverage": coverage,
        "explanationCoverage": explanation_coverage,
        "safeExclusionCountByReason": safe_exclusion_counts,
        "safeExclusionReasons": sorted(
            {
                exclusion.get("reason")
                for exclusion in exclusions
                if exclusion.get("countDisclosure") == "withheld"
            }),
    }


def validate_stale_avoidance(payload: dict[str, Any]) -> dict[str, Any]:
    matching_active = [
        fact for fact in facts(payload)
        if fact.get("id") == PROJECT_A_DECISION_FACT_ID
        and "sql-first migrations plus raw npgsql" in lower_json_text(fact)
        and (fact.get("policy") or {}).get("lifecycleStatus") == "active"
    ]
    require(bool(matching_active), "Active Project A SQL-first Npgsql fact was not returned.")

    active_fact_ids = {fact.get("id") for fact in facts(payload)}
    require(SUPERSEDED_OVERLAY_FACT_ID not in active_fact_ids, "Superseded overlay fact was returned as active.")
    require(REDACTED_OVERLAY_FACT_ID not in active_fact_ids, "Redacted overlay fact was returned as active.")

    contradiction_ids = {
        contradiction.get("relatedFactId")
        for contradiction in contradictions(payload)
    }
    inactive_exclusions = [
        exclusion for exclusion in excluded(payload)
        if exclusion.get("reason") in {"inactive", "redacted_or_deleted"}
    ]

    require(
        SUPERSEDED_OVERLAY_FACT_ID in contradiction_ids or inactive_exclusions,
        "Superseded overlay was not represented as contradiction or inactive metadata.")
    require(
        "redacted benchmark migration path should stay hidden" not in lower_json_text(payload),
        "Redacted overlay content leaked in benchmark response.")
    require(PROJECT_A_DECISION_EVENT_ID in lower_json_text(payload), "Project A source event id is missing.")

    return {
        "staleMemoryUsageCount": 0,
        "activeFactCount": len(facts(payload)),
        "inactiveMetadataCount": len(inactive_exclusions),
    }


def validate_feedback_ranking(
    responses: list[dict[str, Any]],
    replacements: dict[str, str]) -> dict[str, Any]:
    before, feedback, after = responses
    before_item = item_by_source_id(before, PROJECT_A_DECISION_FACT_ID)
    after_item = item_by_source_id(after, PROJECT_A_DECISION_FACT_ID)
    before_adjustment = feedback_adjustment(before_item)
    after_adjustment = feedback_adjustment(after_item)
    before_rank = rank(before_item)
    after_rank = rank(after_item)

    feedback_text = lower_json_text(feedback)
    raw_query = replacements["feedbackQuery"].lower()
    run_id = replacements["runId"].lower()

    require(feedback.get("feedbackType") == "missing", "Feedback response did not preserve missing type.")
    require(str(feedback.get("queryHash", "")).startswith("sha256:"), "Feedback response is missing queryHash.")
    require(raw_query not in feedback_text, "Feedback response echoed raw query text.")
    require(run_id not in feedback_text, "Feedback response leaked the run-unique query token.")
    require(after_adjustment <= before_adjustment - 0.014, "Missing feedback did not lower feedbackAdjustment.")
    require(after_rank < before_rank, "Missing feedback did not lower the Project A decision rank.")

    return {
        "beforeFeedbackAdjustment": round(before_adjustment, 6),
        "afterFeedbackAdjustment": round(after_adjustment, 6),
        "feedbackAdjustmentDelta": round(after_adjustment - before_adjustment, 6),
        "beforeRank": round(before_rank, 6),
        "afterRank": round(after_rank, 6),
        "rankDelta": round(after_rank - before_rank, 6),
    }


def validate_task(
    task_id: str,
    responses: list[dict[str, Any]],
    replacements: dict[str, str]) -> dict[str, Any]:
    if task_id == "context-product-cp08-001":
        return validate_context_packet_product(responses[0])

    if task_id == "context-product-cp08-002":
        return validate_stale_avoidance(responses[0])

    if task_id == "context-product-cp08-003":
        return validate_feedback_ranking(responses, replacements)

    raise SmokeFailure(f"No smoke validator exists for {task_id}.")


def build_replacements() -> dict[str, str]:
    run_id = uuid.uuid4().hex
    return {
        "runId": run_id,
        "feedbackQuery": (
            "Project A CTO guidance migration risk delivery sequencing "
            f"security boundaries cp08 {run_id}"
        ),
    }


def run_smoke(
    suite: dict[str, Any],
    base_url: str,
    api_key: str) -> dict[str, Any]:
    replacements = build_replacements()
    results: list[dict[str, Any]] = []
    aggregate_metrics: dict[str, Any] = {
        "sourceLinkCoverage": None,
        "explanationCoverage": None,
        "safeExclusionCountByReason": {},
        "staleMemoryUsageCount": 0,
        "feedbackAdjustmentDelta": None,
        "rankDelta": None,
    }

    for task in suite["tasks"]:
        responses = [
            execute_http_call(
                base_url,
                api_key,
                apply_templates(call["http"], replacements))
            for call in task.get("toolCalls", [])
        ]
        metrics = validate_task(task["id"], responses, replacements)
        print(f"PASS {task['id']} {task['title']} ({len(responses)} tool call(s))")

        if "sourceLinkCoverage" in metrics:
            aggregate_metrics["sourceLinkCoverage"] = metrics["sourceLinkCoverage"]
        if "explanationCoverage" in metrics:
            aggregate_metrics["explanationCoverage"] = metrics["explanationCoverage"]
        if "safeExclusionCountByReason" in metrics:
            for reason, count in metrics["safeExclusionCountByReason"].items():
                aggregate_metrics["safeExclusionCountByReason"][reason] = (
                    aggregate_metrics["safeExclusionCountByReason"].get(reason, 0) + count)
        if "staleMemoryUsageCount" in metrics:
            aggregate_metrics["staleMemoryUsageCount"] += metrics["staleMemoryUsageCount"]
        if "feedbackAdjustmentDelta" in metrics:
            aggregate_metrics["feedbackAdjustmentDelta"] = metrics["feedbackAdjustmentDelta"]
        if "rankDelta" in metrics:
            aggregate_metrics["rankDelta"] = metrics["rankDelta"]

        results.append({
            "id": task["id"],
            "title": task["title"],
            "category": task["category"],
            "toolCallCount": len(responses),
            "status": "passed",
            "metrics": metrics,
        })

    return {
        "suiteId": suite["suiteId"],
        "scenario": suite["scenario"],
        "contractVersion": suite["contractVersion"],
        "baselineReport": suite.get("baselineReport", ""),
        "apiBaseUrl": base_url,
        "generatedAt": datetime.now(timezone.utc).isoformat(timespec="seconds"),
        "results": results,
        "metrics": aggregate_metrics,
    }


def main() -> int:
    here = Path(__file__).resolve().parent

    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--tasks", type=Path, default=here / "tasks.json")
    parser.add_argument(
        "--api-base-url",
        default=os.environ.get("MEMORYSYSTEM_API_BASE_URL", "http://127.0.0.1:5099"))
    parser.add_argument(
        "--api-key",
        default=os.environ.get(
            "MEMORYSYSTEM_BENCHMARK_API_KEY",
            os.environ.get("MEMORYSYSTEM_API_KEY", "private-alpha-local-key")))
    parser.add_argument("--output", type=Path, default=here.parent / "outputs" / "context-product-v1" / "latest.json")
    args = parser.parse_args()

    suite = load_tasks(args.tasks)
    base_url = args.api_base_url.rstrip("/")

    try:
        report = run_smoke(suite, base_url, args.api_key)
    except SmokeFailure as error:
        print(f"FAIL {error}", file=sys.stderr)
        return 1

    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
        print(f"Wrote {args.output}")

    print(f"Executed {len(report['results'])}/{len(suite['tasks'])} context-product benchmark tasks.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
