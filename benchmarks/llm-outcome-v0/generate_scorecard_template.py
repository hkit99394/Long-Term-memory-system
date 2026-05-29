#!/usr/bin/env python3
"""Generate a scorecard JSON template for the LLM outcome v0 benchmark."""

from __future__ import annotations

import argparse
import json
from pathlib import Path


CATEGORIES = [
    "taskSuccess",
    "decisionAlignment",
    "preferenceAdherence",
    "correctionHandling",
    "groundedness",
    "clarificationDiscipline",
    "humanEditBurden",
]


SAFETY_COUNTERS = [
    "unsafeLeakCount",
    "redactedContentUsageCount",
    "crossScopeFactUsageCount",
    "unauthorizedSourceMentionCount",
    "inventedProjectArtifactCount",
    "rawQueryStorageRecommendationCount",
    "staleFactUsageCount",
    "unnecessaryClarificationCount",
]


def load_tasks(path: Path) -> dict:
    with path.open("r", encoding="utf-8") as stream:
        return json.load(stream)


def empty_category_scores() -> dict[str, None]:
    return {category: None for category in CATEGORIES}


def empty_safety_counters() -> dict[str, int]:
    return {counter: 0 for counter in SAFETY_COUNTERS}


def render_scorecard(suite: dict) -> dict:
    return {
        "runId": "",
        "date": "",
        "benchmarkCommit": "",
        "suiteId": suite["suiteId"],
        "scenario": suite["scenario"],
        "model": "",
        "temperature": None,
        "scorer": "",
        "notes": "",
        "tasks": [
            {
                "id": task["id"],
                "title": task["title"],
                "category": task["category"],
                "memoryOff": {
                    "categoryScores": empty_category_scores(),
                    "safety": empty_safety_counters(),
                    "notes": ""
                },
                "memoryOn": {
                    "categoryScores": empty_category_scores(),
                    "safety": empty_safety_counters(),
                    "notes": ""
                }
            }
            for task in suite["tasks"]
        ]
    }


def main() -> int:
    here = Path(__file__).resolve().parent
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--tasks", type=Path, default=here / "tasks.json")
    parser.add_argument(
        "--output",
        type=Path,
        default=here.parent / "outputs" / "llm-outcome-v0" / "scorecard-template.json")
    args = parser.parse_args()

    suite = load_tasks(args.tasks)
    scorecard = render_scorecard(suite)

    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(scorecard, indent=2) + "\n", encoding="utf-8")

    print(f"Wrote {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
