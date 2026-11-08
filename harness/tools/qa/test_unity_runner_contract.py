"""A passing XML must not hide a crashing/nonzero Unity process."""
import importlib.util
import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('runner', ROOT / 'Tests/robot/libraries/UnityTestRunner.py')
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class UnityExitContractTests(unittest.TestCase):
    def test_nonzero_exit_rejected_even_with_passing_xml(self):
        with tempfile.TemporaryDirectory() as folder:
            runner = module.UnityTestRunner('unused.exe', folder, folder)
            def fake_run(*args, **kwargs):
                (Path(folder) / 'test-results-EditMode.xml').write_text(
                    '<test-run total="1" passed="1" failed="0" skipped="0" result="Passed"/>')
                return SimpleNamespace(returncode=199)
            with patch.object(module.subprocess, 'run', side_effect=fake_run):
                with self.assertRaisesRegex(AssertionError, 'code 199'):
                    runner.run_tests('EditMode')


if __name__ == '__main__':
    unittest.main()
