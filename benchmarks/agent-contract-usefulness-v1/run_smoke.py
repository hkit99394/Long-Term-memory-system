#!/usr/bin/env python3
"""Run the LMSS v1 agent-contract benchmark tool-response smoke."""

from __future__ import annotations

import argparse
import json
import os
import sys
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path
from typing import Any


PROJECT_A_DECISION_FACT_ID = "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"
PROJECT_A_DECISION_EVENT_ID = "77777777-7777-4777-8777-777777777777"
USER_PREFERENCE_EVENT_ID = "66666666-6666-4666-8666-666666666666"
SUPERSEDED_OVERLAY_FACT_ID = "f1f1f1f1-f1f1-4f1f-8f1f-f1f1f1f1f1f1"
PROJECT_B_ID = "44444444-4444-4444-8444-444444444444"


class SmokeFailure(Exception):
    """Raised when an expected tool-response signal is missing."""


def load_tasks(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8") as stream:
        return json.load(stream)


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


def facts(payload: dict[str, Any]) -> list[dict[str, Any]]:
    return list(payload.get("facts") or [])


def contradictions(payload: dict[str, Any]) -> list[dict[str, Any]]:
    return list(payload.get("contradictions") or [])


def excluded(payload: dict[str, Any]) -> list[dict[str, Any]]:
    return list(payload.get("excluded") or [])


def require(condition: bool, message: str) -> None:
    if not condition:
        raise SmokeFailure(message)


def require_text(payload: Any, expected: str) -> None:
    require(expected.lower() in lower_json_text(payload), f"Missing expected text: {expected}")


def require_decision_fact(payload: dict[str, Any]) -> None:
    matching_facts = [
        fact for fact in facts(payload)
        if fact.get("id") == PROJECT_A_DECISION_FACT_ID
        and "sql-first migrations plus raw npgsql" in (fact.get("claim") or "").lower()
    ]
    require(bool(matching_facts), "Project A SQL-first Npgsql decision fact was not returned.")


def require_source_event(payload: dict[str, Any], source_event_id: str) -> None:
    source_event_ids = [
        event_id
        for fact in facts(payload)
        for event_id in fact.get("sourceEventIds", [])
    ]
    require(source_event_id in source_event_ids, f"Missing source event id {source_event_id}.")


def require_authorized_active_fact(payload: dict[str, Any]) -> None:
    for fact in facts(payload):
        policy = fact.get("policy") or {}
        if policy.get("authorized") is True and policy.get("lifecycleStatus") == "active":
            return
    raise SmokeFailure("No authorized active fact policy was returned.")


def require_not_authorized_withheld(payload: dict[str, Any]) -> None:
    for exclusion in excluded(payload):
        if exclusion.get("reason") == "not_authorized" and exclusion.get("countDisclosure") == "withheld":
            return
    raise SmokeFailure("Missing withheld not_authorized exclusion summary.")


def require_exclusion_count_at_least(payload: dict[str, Any], reason: str, count: int) -> None:
    for exclusion in excluded(payload):
        if exclusion.get("reason") == reason and (exclusion.get("count") or 0) >= count:
            return
    raise SmokeFailure(f"Missing exclusion '{reason}' with count >= {count}.")


def validate_task(task_id: str, responses: list[dict[str, Any]]) -> None:
    first = responses[0]

    if task_id == "lmss-v1-acu-001":
        require_decision_fact(first)
        require_authorized_active_fact(first)
        require_source_event(first, PROJECT_A_DECISION_EVENT_ID)
        require_not_authorized_withheld(first)
        return

    if task_id == "lmss-v1-acu-002":
        require_decision_fact(first)
        evidence = responses[1]
        require_text(evidence, "explicit schema control")
        require_text(evidence, "migration repeatability")
        require_text(evidence, "clear authorization predicates")
        require_text(evidence, "manual mapping")
        return

    if task_id == "lmss-v1-acu-003":
        require_decision_fact(first)
        matching_contradictions = [
            contradiction for contradiction in contradictions(first)
            if contradiction.get("currentFactId") == PROJECT_A_DECISION_FACT_ID
            and contradiction.get("relatedFactId") == SUPERSEDED_OVERLAY_FACT_ID
            and contradiction.get("relatedStatus") == "superseded"
        ]
        require(bool(matching_contradictions), "ACU-003 contradiction overlay was not returned.")
        require_exclusion_count_at_least(first, "inactive", 1)
        require_exclusion_count_at_least(first, "redacted_or_deleted", 1)
        require(
            "redacted benchmark migration path should stay hidden" not in lower_json_text(first),
            "Redacted overlay content leaked through query-facts.")
        return

    if task_id == "lmss-v1-acu-004":
        facts_and_links = {
            "facts": first.get("facts", []),
            "contradictions": first.get("contradictions", []),
        }
        require(
            "confidential runway model" not in lower_json_text(facts_and_links),
            "Project B confidential content leaked outside the caller-supplied query echo.")
        require(PROJECT_B_ID not in json_text(facts_and_links), "Project B id leaked in returned facts.")
        require_not_authorized_withheld(first)
        return

    if task_id == "lmss-v1-acu-005":
        context = first
        require_text(context, "SQL-first Npgsql decision")
        require_text(context, "architecture risk")
        require_text(context, "operational reversibility")
        require_text(context, "security boundaries")
        require_decision_fact(responses[1])
        return

    if task_id == "lmss-v1-acu-006":
        deployment_region_facts = [
            fact for fact in facts(first)
            if "deployment region" in lower_json_text(fact)
        ]
        require(not deployment_region_facts, "Unexpected deployment-region fact was returned.")
        return

    if task_id == "lmss-v1-acu-007":
        require_text(first, "concise decision logs")
        require_source_event(first, USER_PREFERENCE_EVENT_ID)
        return

    if task_id == "lmss-v1-acu-008":
        require_text(first, PROJECT_A_DECISION_FACT_ID)
        require_text(first, "/api/events/")
        return

    raise SmokeFailure(f"No smoke validator exists for {task_id}.")


def run_smoke(suite: dict[str, Any], base_url: str, api_key: str) -> list[dict[str, Any]]:
    results: list[dict[str, Any]] = []
    tasks = suite["tasks"]

    for task in tasks:
        responses = [
            execute_http_call(base_url, api_key, call["http"])
            for call in task.get("toolCalls", [])
        ]
        validate_task(task["id"], responses)
        print(f"PASS {task['id']} {task['title']} ({len(responses)} tool call(s))")
        results.append({
            "id": task["id"],
            "title": task["title"],
            "toolCallCount": len(responses),
            "status": "passed",
        })

    print(f"Executed {len(results)}/{len(tasks)} benchmark tasks with ACU-003 enabled.")
    return results


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
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()

    suite = load_tasks(args.tasks)
    base_url = args.api_base_url.rstrip("/")

    try:
        results = run_smoke(suite, base_url, args.api_key)
    except SmokeFailure as error:
        print(f"FAIL {error}", file=sys.stderr)
        return 1

    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps({
            "suiteId": suite["suiteId"],
            "contractVersion": suite["contractVersion"],
            "apiBaseUrl": base_url,
            "results": results,
        }, indent=2) + "\n", encoding="utf-8")
        print(f"Wrote {args.output}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
