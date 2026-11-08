"""Check independent statistical conventions and rejection of unavailable counters."""
import csv
import json
import math
import tempfile
import unittest
from pathlib import Path
from performance_stats import distribution, analyse


class PerformanceStatsTests(unittest.TestCase):
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
