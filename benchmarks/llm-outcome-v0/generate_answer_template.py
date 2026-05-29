#!/usr/bin/env python3
"""Generate an answer-capture template for the LLM outcome v0 benchmark."""

from __future__ import annotations

import argparse
import json
from pathlib import Path


def load_tasks(path: Path) -> dict:
    with path.open("r", encoding="utf-8") as stream:
        return json.load(stream)


def bullet_list(items: list[str]) -> str:
    if not items:
        return "- none"

    return "\n".join(f"- {item}" for item in items)


def render_task(task: dict) -> str:
    return f"""## {task["id"]}: {task["title"]}

Category: `{task["category"]}`

### Prompt

```text
{task["prompt"]}
```

### Expected Signals

{bullet_list(task.get("expectedSignals", []))}

### Forbidden Signals

{bullet_list(task.get("forbiddenSignals", []))}

### Safety Gates

{bullet_list(task.get("safetyGates", []))}

### Memory Off Answer

```text
Paste the model answer here.
```

### Memory On Context

```text
Paste or reference the context packet used for this run.
```

### Memory On Answer

```text
Paste the model answer here.
```

### Scoring Notes

- Memory off notes:
- Memory on notes:
- Safety observations:
"""


def render_template(suite: dict) -> str:
    tasks = "\n".join(render_task(task) for task in suite["tasks"])

    return f"""# {suite["suiteId"]} Answer Capture Template

Scenario: `{suite["scenario"]}`

Use this file to capture model outputs before scoring. Keep model settings,
temperature, and task prompts identical between `memory_off` and `memory_on`.

## Run Metadata

```text
run_id:
date:
model:
temperature:
scorer:
benchmark_commit:
context_manifest:
```

## Tasks

{tasks}
"""


def main() -> int:
    here = Path(__file__).resolve().parent
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--tasks", type=Path, default=here / "tasks.json")
    parser.add_argument(
        "--output",
        type=Path,
        default=here.parent / "outputs" / "llm-outcome-v0" / "answers-template.md")
    args = parser.parse_args()

    suite = load_tasks(args.tasks)
    output = render_template(suite)

    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(output, encoding="utf-8")

    print(f"Wrote {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
