"""Verify a real failed sampling Player preserves partial evidence and exits."""
import json
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
output = Path(sys.argv[1]).resolve()
assert output.is_relative_to((ROOT / 'Tests/robot/results').resolve())
output.mkdir(parents=True, exist_ok=False)
with (output / 'host.log').open('w', encoding='utf-8') as log:
    run = subprocess.run([sys.executable, 'tools/qa/run_performance.py', str(output / 'case'), '--probe', '--fault-probe'],
                         cwd=ROOT, stdout=log, stderr=subprocess.STDOUT, timeout=120)
assert run.returncode == 1, 'Controlled sampling failure must fail the real host gate'
host = json.loads((output / 'case/result.json').read_text(encoding='utf-8'))
assert len(host['rounds']) == 1 and host['rounds'][0]['exitCode'] == 1 and not host['rounds'][0].get('forcedCleanup')
folder = output / 'case/round-1'
result = json.loads((folder / 'performance-result.json').read_text(encoding='utf-8-sig'))
partial = json.loads((folder / 'performance-partial.json').read_text(encoding='utf-8-sig'))
assert not result['complete'] and result['probe']
assert partial['phase'] == 'probe' and len(partial['frames']) >= 5
assert all(f['mainThreadNs'] > 0 for f in partial['frames'])
assert 'Controlled sampling failure fixture' in (folder / 'performance-errors.txt').read_text(encoding='utf-8')
proof = dict(passed=True, scope='expected sampling failure contract, not game performance',
             ownedPid=host['rounds'][0]['pid'], realExitCode=1, partialFrames=len(partial['frames']), complete=False)
(output / 'verification.json').write_text(json.dumps(proof, indent=2), encoding='utf-8')
print(json.dumps(proof))
