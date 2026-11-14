"""Check independent statistical conventions and rejection of unavailable counters."""
import csv
import json
import math
import tempfile
import unittest
from pathlib import Path
from performance_stats import distribution, analyse, verify_save_recovery


class PerformanceStatsTests(unittest.TestCase):
    def test_current_full_run_rejects_a_legacy_player_before_reading_samples(self):
        with tempfile.TemporaryDirectory() as temporary:
            folder = Path(temporary)
            (folder/'performance-result.json').write_text(json.dumps(dict(complete=True, errors=0, probe=False)))
            (folder/'performance-environment.json').write_text(json.dumps(dict(development=True,
                mainThreadValid=True, gcValid=True, width=1280, height=720, targetFps=60, vsync=0)))
            with self.assertRaisesRegex(AssertionError, 'Current full run requires'):
                analyse(folder, False, require_save_contract=True)

    def recovery_fixture(self, folder):
        state = dict(version=2, clicks=777, catHair=12345.5, totalEarned=50000,
                     cats=[dict(id='Milk', count=40)], items=[dict(id='toy', level=5)])
        save = folder / 'save-stress/CatGame'
        save.mkdir(parents=True)
        for file in (folder/'save-backup-before-corruption.json', folder/'save-restored-after-corruption.json',
                     save/'save.json', save/'save.json.bak'):
            file.write_text(json.dumps(state))
        (save/'save.bad.json').write_text('{ damaged primary')
        return state

    def test_backup_recovery_does_not_accept_a_fresh_game(self):
        with tempfile.TemporaryDirectory() as temporary:
            folder = Path(temporary)
            state = self.recovery_fixture(folder)
            verify_save_recovery(folder)
            state.update(clicks=0, catHair=0, cats=[dict(id='Milk', count=1)])
            (folder/'save-restored-after-corruption.json').write_text(json.dumps(state))
            with self.assertRaisesRegex(AssertionError, 'Recovered snapshot'):
                verify_save_recovery(folder)

    def test_recovery_requires_fields_beyond_the_old_click_and_cat_checks(self):
        with tempfile.TemporaryDirectory() as temporary:
            folder = Path(temporary)
            state = self.recovery_fixture(folder)
            state['items'][0]['level'] = 4
            (folder/'save-restored-after-corruption.json').write_text(json.dumps(state))
            with self.assertRaisesRegex(AssertionError, 'Recovered snapshot'):
                verify_save_recovery(folder)

    def test_missing_recovery_snapshot_is_not_treated_as_passed(self):
        with tempfile.TemporaryDirectory() as temporary:
            folder = Path(temporary)
            self.recovery_fixture(folder)
            (folder/'save-backup-before-corruption.json').unlink()
            with self.assertRaises(FileNotFoundError): verify_save_recovery(folder)

    def test_valid_json_is_not_evidence_of_quarantining_a_damaged_primary(self):
        with tempfile.TemporaryDirectory() as temporary:
            folder = Path(temporary)
            state = self.recovery_fixture(folder)
            (folder/'save-stress/CatGame/save.bad.json').write_text(json.dumps(state))
            with self.assertRaisesRegex(AssertionError, 'quarantined malformed'):
                verify_save_recovery(folder)

    def test_nearest_rank_percentiles_preserve_spikes(self):
        result = distribution(list(range(1, 101)))
        self.assertEqual((result['p50'], result['p95'], result['p99'], result['maximum']), (50, 95, 99, 100))
        self.assertEqual(distribution([1] * 99 + [10000])['maximum'], 10000)
        with self.assertRaises(ValueError): distribution([])
        with self.assertRaises(ValueError): distribution([1, math.nan])

    def test_missing_profiler_sample_is_not_zero_performance(self):
        with tempfile.TemporaryDirectory() as temporary:
            folder = Path(temporary)
            environment = dict(development=True, mainThreadValid=True, gcValid=True, width=1280, height=720, targetFps=60, vsync=0)
            result = dict(complete=True, probe=True, errors=0, operations=[], phases=[dict(name='probe', frames=2, elapsed=2, boundaryErrors=0, boundaryChecks=0, visible=1, owned=1,workingSetBefore=100,workingSetAfter=100)])
            (folder/'performance-result.json').write_text(json.dumps(result))
            (folder/'performance-environment.json').write_text(json.dumps(environment))
            fields = ['frame','realtime','frame_ms','main_thread_ns','gc_bytes','mono_bytes','unity_allocated_bytes','unity_reserved_bytes']
            with (folder/'frames-probe.csv').open('w',newline='') as stream:
                writer=csv.DictWriter(stream,fieldnames=fields); writer.writeheader()
                for frame in (1,2):
                    writer.writerow(dict(zip(fields,[frame,frame,16.7,0,0,100,200,300])))
            with self.assertRaisesRegex(AssertionError,'Unavailable profiler'):
                analyse(folder, True)
            result['phases'][0]['workingSetBefore']=0
            (folder/'performance-result.json').write_text(json.dumps(result))
            with (folder/'frames-probe.csv').open('w',newline='') as stream:
                writer=csv.DictWriter(stream,fieldnames=fields); writer.writeheader()
                for frame in (1,2):
                    writer.writerow(dict(zip(fields,[frame,frame,16.7,1000000,0,100,200,300])))
            with self.assertRaisesRegex(AssertionError,'Unavailable RSS'):
                analyse(folder, True)


if __name__ == '__main__':
    unittest.main()
