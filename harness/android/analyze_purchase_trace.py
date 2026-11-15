"""Recompute purchase acknowledgement and durability from raw trace and save evidence."""
import argparse
import json
from collections import Counter
from pathlib import Path


def analyze(directory):
    directory = Path(directory)
    results = json.loads((directory / "results.json").read_text())
    counts = Counter()
    timing_checks = []
    for index, result in enumerate(results, 1):
        prefix = f"{index:02d}"
        events = [json.loads(line) for line in (directory / f"{prefix}-events.jsonl").read_text().splitlines()]
        states = {name: json.loads((directory / f"{prefix}-{name}.json").read_text())
                  for name in ("before", "stopped", "restored")}
        assert states["before"]["cats"][0] == {"id": "Milk", "count": 1}
        assert states["before"]["catHair"] >= 26
        ready = [e for e in events if e["kind"] == "ready"]
        assert len(ready) == 1 and ready[0]["milk"] == 1 and ready[0]["interactable"]
        assert ready[0]["fps"] == result["fps"]
        assert (ready[0]["width"], ready[0]["height"]) == (1600, 900)
        callbacks = [e for e in events if e["kind"] == "after-button-callback"]
        accepted = any(e["milk"] == 2 for e in callbacks)
        assert result["accepted"] == accepted and result["callbacks"] == len(callbacks)
        for name in ("stopped", "restored"):
            assert result[name + "Count"] == states[name]["cats"][0]["count"]
        assert result["pid"] != result["restartedPid"]
        result["pauseObserved"] = any(e["kind"] == "paused" for e in events)
        if result.get("homePolicy") == "confirmed" and result["home"]:
            within_budget = result["homeAfterSeconds"] < 3
            assert result.get("timingBudgetPassed", within_budget) == within_budget
            timing_checks.append(within_budget)
        if accepted:
            outcome = "accepted-and-durable" if all(result[n] == 2 for n in ("stoppedCount", "restoredCount")) else "accepted-but-lost"
        elif callbacks:
            outcome = "callback-without-purchase"
        else:
            assert result["stoppedCount"] == result["restoredCount"] == 1, "Unexplained purchase without acknowledgement"
            outcome = "input-without-button-callback"
        counts[outcome] += 1
        result["classification"] = outcome
    combinations = Counter((r["fps"], r["holdMs"], r["home"]) for r in results)
    assert combinations and len(set(combinations.values())) == 1, "Incomplete matrix"
    assert len(combinations) == 4 * len({r["fps"] for r in results}), "Missing matrix combination"
    return {"runs": len(results), "outcomes": dict(counts), "cases": results,
            "durabilityPassed": not counts["accepted-but-lost"],
            "allPurchasesSucceeded": counts["accepted-and-durable"] == len(results),
            "timingGatePassed": all(timing_checks) if timing_checks else None,
            "originalFailureRootCauseProven": False}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    args = parser.parse_args()
    summary = analyze(args.directory)
    (args.directory.parent / "analysis.json").write_text(json.dumps(summary, indent=2), encoding="utf-8")
    print(json.dumps({k: v for k, v in summary.items() if k != "cases"}, indent=2))
    raise SystemExit(0 if summary["durabilityPassed"] and summary["timingGatePassed"] is not False else 1)
