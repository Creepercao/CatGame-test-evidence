"""Read-only verification of curated historical evidence (not a game test run)."""
from pathlib import Path
import json, hashlib, csv, math
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent
def read(path):
    return json.loads((ROOT/path).read_text(encoding='utf-8'))

def main():
    manifest=read('provenance.json')
    for row in manifest['files']:
        p=ROOT/row['path']
        assert hashlib.sha256(p.read_bytes()).hexdigest()==row['publishedSha256'], row['path']
        if p.suffix=='.xml': ET.parse(p)
        elif p.name=='save.bad.json':
            try: json.loads(p.read_text(encoding='utf-8'))
            except json.JSONDecodeError: pass
            else: raise AssertionError('Expected deliberately corrupted fixture')
        elif p.suffix=='.json': json.loads(p.read_text(encoding='utf-8'))
        elif p.suffix=='.jsonl':
            for line in p.read_text(encoding='utf-8').splitlines():
                if line.strip(): json.loads(line)
    print('PASS public hashes and structured formats:',len(manifest['files']))
    for kind,count in [('PlayMode',86),('EditMode',51)]:
        x=ET.parse(ROOT/f'evidence/ci-5/full/unity-main/test-results-{kind}.xml').getroot()
        assert int(x.get('passed'))==count and int(x.get('failed'))==int(x.get('skipped'))==0
    print('PASS NUnit: PlayMode 86, EditMode 51')
    for color in ['red','green']:
        path=next((ROOT/f'evidence/roaming-target-{color}-20261106-01').rglob('test-results-PlayMode.xml'))
        failures=int(ET.parse(path).getroot().get('failed'))
        assert (failures>0) if color=='red' else (failures==0)
    print('PASS roaming defect red/green results')
    for n,allowed in [(2,False),(3,False),(4,True),(5,True)]:
        p=read(f'evidence/ci-{n}/pipeline.json')
        assert p['releaseAllowed'] is allowed
        if not allowed: assert p['stages'][-1]['exitCode']!=0
    print('PASS failed CI gates and successful delivery records')
    stepsfile=next((ROOT/'evidence/ci-5/business').rglob('steps.jsonl'))
    steps=[json.loads(s) for s in stepsfile.read_text(encoding='utf-8').splitlines() if s.strip()]
    launches=[s['result']['pid'] for s in steps if s['action']=='launch']
    exits=[s['result'] for s in steps if s['action']=='normal-exit']
    assert len(set(launches))==2 and [e['pid'] for e in exits]==launches and all(e['exitCode']==0 for e in exits)
    assert any(s['action']=='restart-verified' for s in steps)
    print('PASS real process restart evidence')
    for n in [1,2]:
        folder=f'evidence/performance-baseline-20261106-02/performance/round-{n}'
        with (ROOT/folder/'frames-hundred.csv').open(encoding='utf-8') as f:
            vals=sorted(float(row['frame_ms']) for row in csv.DictReader(f))
        assert vals and all(math.isfinite(x) and x>0 for x in vals)
        p95=vals[math.ceil(.95*len(vals))-1]
        expected=read(folder+'/analysis.json')['phases']['hundred']['frames']['p95']
        assert abs(p95-expected)<1e-6
        assert p95<=20 and sum(x>33.3 for x in vals)/len(vals)<=.01
        print(f'PASS round {n} hundred-cat P95={p95:.6f}ms, frames={len(vals)}')
    print('All evidence checks passed; no new Unity/Robot execution performed.')

if __name__=='__main__': main()
