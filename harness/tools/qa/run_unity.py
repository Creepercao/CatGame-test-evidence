"""Run a bounded Unity editor command, retaining command and real process exit evidence."""
import argparse
import json
import subprocess
from datetime import datetime, timezone
from pathlib import Path

p = argparse.ArgumentParser()
p.add_argument('--output', required=True)
p.add_argument('--method', required=True)
p.add_argument('--unity', default=r'D:\Unity\2022.3.21f1\Editor\Unity.exe')
a = p.parse_args()
root = Path(__file__).resolve().parents[2]
out = (root / a.output).resolve()
out.mkdir(parents=True, exist_ok=False)
if (root / 'Temp/UnityLockfile').exists():
    raise SystemExit('Editor project lock exists; close Editor before batch commands.')
cmd = [a.unity, '-batchmode', '-quit', '-projectPath', str(root), '-executeMethod',
       a.method, '-logFile', str(out / 'unity.log')]
record = dict(command=cmd, started=datetime.now(timezone.utc).isoformat())
(out / 'command.json').write_text(json.dumps(record, indent=2), encoding='utf-8')
try:
    result = subprocess.run(cmd, cwd=root, timeout=1800)
    record['exit_code'] = result.returncode
except subprocess.TimeoutExpired:
    record['error'] = 'Unity timeout after 1800 seconds'
    record['exit_code'] = 124
record['finished'] = datetime.now(timezone.utc).isoformat()
(out / 'result.json').write_text(json.dumps(record, indent=2), encoding='utf-8')
print(json.dumps(record))
raise SystemExit(record['exit_code'])
