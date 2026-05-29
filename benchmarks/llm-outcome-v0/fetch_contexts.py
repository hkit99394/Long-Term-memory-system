#!/usr/bin/env python3
"""Fetch memory-on context packets for the LLM outcome v0 benchmark."""

from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
from urllib.error import HTTPError, URLError
from urllib.parse import urlencode
from urllib.request import Request, urlopen


DEFAULT_API_BASE_URL = "http://127.0.0.1:5099"
DEFAULT_API_KEY = "private-alpha-local-key"


def load_tasks(path: Path) -> dict:
    with path.open("r", encoding="utf-8") as stream:
        return json.load(stream)


def build_url(base_url: str, context_request: dict) -> str:
    query = {
        "q": context_request["query"],
        "limit": context_request.get("limit", 6),
    }

    if context_request.get("scopeType"):
        query["scopeType"] = context_request["scopeType"]
    if context_request.get("scopeId"):
        query["scopeId"] = context_request["scopeId"]
    if context_request.get("roleId"):
        query["roleId"] = context_request["roleId"]

    return f'{base_url.rstrip("/")}{context_request["path"]}?{urlencode(query)}'


def fetch_json(url: str, api_key: str, timeout_seconds: float) -> dict:
    request = Request(url, headers={"X-Api-Key": api_key})

    with urlopen(request, timeout=timeout_seconds) as response:
        body = response.read().decode("utf-8")
        return json.loads(body)


def write_json(path: Path, value: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def main() -> int:
    here = Path(__file__).resolve().parent
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--tasks", type=Path, default=here / "tasks.json")
    parser.add_argument(
        "--output-dir",
        type=Path,
        default=here.parent / "outputs" / "llm-outcome-v0" / "contexts")
    parser.add_argument(
        "--api-base-url",
        default=os.environ.get("MEMORYSYSTEM_API_BASE_URL", DEFAULT_API_BASE_URL))
    parser.add_argument(
        "--api-key",
        default=os.environ.get("MEMORYSYSTEM_BENCHMARK_API_KEY", DEFAULT_API_KEY))
    parser.add_argument("--timeout-seconds", type=float, default=10)
    args = parser.parse_args()

    suite = load_tasks(args.tasks)
    manifest = {
        "suiteId": suite["suiteId"],
        "apiBaseUrl": args.api_base_url,
        "contexts": []
    }

    for task in suite["tasks"]:
        url = build_url(args.api_base_url, task["contextRequest"])
        output_path = args.output_dir / f'{task["id"]}.json'

        try:
            context_packet = fetch_json(url, args.api_key, args.timeout_seconds)
        except HTTPError as error:
            raise SystemExit(f'{task["id"]}: HTTP {error.code} from {url}') from error
        except URLError as error:
            raise SystemExit(f'{task["id"]}: failed to reach {url}: {error.reason}') from error

        write_json(output_path, context_packet)
        manifest["contexts"].append({
            "taskId": task["id"],
            "title": task["title"],
            "url": url,
            "path": str(output_path)
        })
        print(f'Wrote {output_path}')

    write_json(args.output_dir / "manifest.json", manifest)
    print(f'Wrote {args.output_dir / "manifest.json"}')
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
