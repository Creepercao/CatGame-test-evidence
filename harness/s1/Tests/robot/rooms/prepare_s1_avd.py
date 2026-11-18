"""Initialize only the explicitly owned, read-only AVD's separate QA user."""
import argparse
import json
import re
import subprocess
from pathlib import Path

p=argparse.ArgumentParser();p.add_argument('--adb',required=True);p.add_argument('--output',type=Path,required=True)
args=p.parse_args();args.output.mkdir(parents=True,exist_ok=False)
prefix=[args.adb,'-P','15037','-s','emulator-5564','shell']
steps=[]
def shell(*command):
    r=subprocess.run(prefix+list(command),capture_output=True,text=True,encoding='utf-8',timeout=45)
    steps.append(dict(command=list(command),exit=r.returncode,output=r.stdout.strip(),error=r.stderr.strip()))
    (args.output/'steps.json').write_text(json.dumps(steps,indent=2),encoding='utf-8')
    assert r.returncode==0,r.stderr
    return r.stdout.strip()
assert shell('getprop','ro.product.model')=='sdk_gphone64_x86_64'
assert shell('getprop','sys.boot_completed')=='1'
users=shell('pm','list','users')
if 'CatGame-Appium-QA' not in users:
    created=shell('pm','create-user','CatGame-Appium-QA')
    uid=re.search(r'\bid (\d+)\b',created).group(1)
else:
    uid=re.search(r'UserInfo\{(\d+):CatGame-Appium-QA:',users).group(1)
assert uid=='10','Do not silently choose another test profile'
shell('settings','--user',uid,'put','secure','user_setup_complete','1')
shell('settings','--user',uid,'put','system','screen_off_timeout','2147483647')
shell('am','start-user','-w',uid)
shell('am','switch-user',uid)
shell('wm','size','900x1600')
assert shell('am','get-current-user')==uid
(args.output/'result.json').write_text(json.dumps(dict(passed=True,user=int(uid),serial='emulator-5564',
    readOnlyLaunch='android-env-02',phoneOperated=False,mumuOperated=False),indent=2),encoding='utf-8')
print('PASS owned AVD QA user',uid)
