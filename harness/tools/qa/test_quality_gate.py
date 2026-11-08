"""Real Robot integration: failure, skip, malformed evidence and repair gate behaviour."""
import json
import os
import subprocess
import sys
import unittest
from pathlib import Path
from tempfile import TemporaryDirectory

from run_suite import ROOT, verdict


class QualityGateTests(unittest.TestCase):
    def test_exit_code_and_report_must_agree(self):
        with TemporaryDirectory(dir=ROOT / 'Tests/robot/results') as folder:
            report = Path(folder) / 'output.xml'
            self.assertFalse(verdict(0, report)['releaseAllowed'])
            report.write_text('<broken>', encoding='utf-8')
            self.assertFalse(verdict(0, report)['releaseAllowed'])
            report.write_text('<robot><statistics><total><stat pass="2" fail="0" skip="0"/></total></statistics></robot>')
            self.assertFalse(verdict(9, report)['releaseAllowed'])
            self.assertTrue(verdict(0, report)['releaseAllowed'])

    def test_actual_robot_failure_then_repair_and_skip(self):
        import uuid
        evidence = ROOT / 'Tests/robot/results' / ('gate-contract-' + uuid.uuid4().hex[:8])
        evidence.mkdir(parents=True)
        cases = [('red', 'Fail    deliberate gate contract failure', False),
                 ('green', 'Should Be Equal    repaired    repaired', True),
                 ('skip', 'Skip    deliberate missing coverage', False)]
        for label, keyword, allowed in cases:
            suite = evidence / (label + '.robot')
            suite.write_text('*** Test Cases ***\nContract Step\n    ' + keyword + '\n', encoding='utf-8')
            output = evidence / label
            run = subprocess.run([sys.executable, str(ROOT / 'tools/qa/run_suite.py'),
                                  '--suite', str(suite), '--output', str(output)], cwd=ROOT,
                                 capture_output=True, text=True, encoding='utf-8', timeout=60)
            result = json.loads((output / 'result.json').read_text(encoding='utf-8'))
            self.assertEqual(result['releaseAllowed'], allowed, run.stdout + run.stderr)
            self.assertEqual(run.returncode, 0 if allowed else 1)
            self.assertTrue((output / 'log.html').is_file())
            self.assertTrue((output / 'artifacts.json').is_file())
            self.assertTrue((output / 'console.log').is_file())
        print('Gate contract evidence: ' + str(evidence))


if __name__ == '__main__':
    unittest.main()
