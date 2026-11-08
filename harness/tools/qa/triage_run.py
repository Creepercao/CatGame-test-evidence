"""Append an evidence-based triage decision without rewriting raw test results."""
import argparse
import json
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('run', type=Path)
    parser.add_argument('--classification', required=True, choices=['product', 'harness', 'environment', 'expected-failure'])
    parser.add_argument('--reason', required=True)
    parser.add_argument('--evidence', action='append', required=True, help='Path relative to run directory')
    args = parser.parse_args()
    run = args.run.resolve()
    if not run.is_relative_to((ROOT / 'Tests/robot/results').resolve()):
        parser.error('Run must be under Tests/robot/results')
    result = json.loads((run / 'result.json').read_text(encoding='utf-8'))
    if result.get('releaseAllowed'):
        parser.error('Triage requires an unsuccessful run')
    paths = [(run / p).resolve() for p in args.evidence]
    if not all(p.is_relative_to(run) and p.is_file() for p in paths):
        parser.error('All evidence must be existing files under the run')
    record = dict(utc=datetime.now(timezone.utc).isoformat(), classification=args.classification,
                  reason=args.reason, evidence=[str(p.relative_to(run)) for p in paths],
                  releaseAllowed=result['releaseAllowed'])
    # Append-only history; raw result.json remains the immutable execution observation.
    with (run / 'triage.jsonl').open('a', encoding='utf-8') as stream:
        stream.write(json.dumps(record, ensure_ascii=False) + '\n')
    print(json.dumps(record, ensure_ascii=False))


if __name__ == '__main__':
    main()
