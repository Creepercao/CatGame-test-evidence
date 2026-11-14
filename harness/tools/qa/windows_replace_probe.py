"""Diagnose Win32 replacement with explicit, bounded retries, outside Unity.

This probe changes only newly created files in the requested output directory.
It never edits game saves. A recovered error remains visible in the results.
"""
import argparse
import ctypes
from ctypes import wintypes
import json
from pathlib import Path
import time


def run(output, cycles, repeats, retries, delay_ms):
    replace = ctypes.WinDLL('kernel32', use_last_error=True).ReplaceFileW
    replace.argtypes = [wintypes.LPCWSTR, wintypes.LPCWSTR, wintypes.LPCWSTR,
                        wintypes.DWORD, ctypes.c_void_p, ctypes.c_void_p]
    replace.restype = wintypes.BOOL
    output.mkdir(parents=True, exist_ok=False)
    results = []
    for repeat in range(1, repeats + 1):
        for backup in (True, False):
            folder = output / f'{repeat}-backup-{backup}'
            folder.mkdir()
            primary, temporary, previous = [folder / name for name in ('save.json', 'save.json.tmp', 'save.json.bak')]
            committed = None
            record = {'repeat': repeat, 'backupEnabled': backup, 'requested': cycles,
                      'completed': 0, 'retryLimit': retries, 'delayMs': delay_ms, 'errors': []}
            started = time.monotonic()
            for cycle in range(cycles):
                data = json.dumps({'cycle': cycle, 'payload': 'x' * 3000}).encode()
                temporary.write_bytes(data)
                success = False
                if primary.exists():
                    for attempt in range(retries + 1):
                        ctypes.set_last_error(0)
                        success = bool(replace(str(primary), str(temporary), str(previous) if backup else None,
                                               0, None, None))
                        if success:
                            break
                        error = ctypes.get_last_error()
                        stable = primary.exists() and primary.read_bytes() == committed
                        pending = temporary.exists() and temporary.read_bytes() == data
                        record['errors'].append({'cycle': cycle, 'attempt': attempt, 'win32Error': error,
                                                 'primaryPreserved': stable, 'temporaryPreserved': pending})
                        if error != 1175 or not stable or not pending or attempt == retries:
                            break
                        time.sleep(delay_ms / 1000)
                    if not success:
                        record['failedCycle'] = cycle
                        break
                    if backup:
                        assert previous.read_bytes() == committed
                else:
                    temporary.rename(primary)
                assert primary.read_bytes() == data and not temporary.exists()
                committed = data
                record['completed'] += 1
            record['elapsedSeconds'] = time.monotonic() - started
            results.append(record)
            (output / 'results.json').write_text(json.dumps(results, indent=2), encoding='utf-8')
            print(json.dumps({k: v for k, v in record.items() if k != 'errors'}),
                  'observedErrors=' + str(len(record['errors'])), flush=True)
    return results


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--cycles', type=int, default=1000, choices=range(1, 10001))
    parser.add_argument('--repeats', type=int, default=3, choices=range(1, 6))
    parser.add_argument('--retries', type=int, default=0, choices=range(0, 4))
    parser.add_argument('--delay-ms', type=int, default=10, choices=range(1, 101))
    args = parser.parse_args()
    rows = run(args.output, args.cycles, args.repeats, args.retries, args.delay_ms)
    raise SystemExit(0 if all(row['completed'] == args.cycles for row in rows) else 1)
