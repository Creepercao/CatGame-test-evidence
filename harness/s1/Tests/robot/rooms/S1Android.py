"""S1 ordinary APK observations on the explicitly owned read-only AVD only."""
import hashlib
import json
import re
import subprocess
import time
from pathlib import Path
from F1ArtAndroid import F1ArtAndroid


class S1Android(F1ArtAndroid):
    def upgrade_s1_preserving_natural_save(self, apk):
        self._guard()
        self._home_and_wait()
        self.force_stop_cat_game()
        assert 'versionCode=6 ' in self._shell('dumpsys', 'package', self.PACKAGE)
        path=f'/data/media/{self.user}/Android/data/{self.PACKAGE}/files/CatGame/save.json'
        read=[*self.adb[:-1], 'exec-out', 'su', '0', 'cat', path]
        before=subprocess.check_output(read, timeout=30)
        (self.output_dir/'before-install-save.json').write_bytes(before)
        assert sum(c['count'] for c in json.loads(before)['cats'])==2
        p=subprocess.run([*self.adb[:-1], 'install', '--user', self.user, '-r', str(apk)], capture_output=True, timeout=180)
        (self.output_dir/'install.stdout.txt').write_bytes(p.stdout)
        (self.output_dir/'install.stderr.txt').write_bytes(p.stderr)
        assert p.returncode==0 and b'Success' in p.stdout
        after=subprocess.check_output(read, timeout=30)
        (self.output_dir/'after-install-before-launch-save.json').write_bytes(after)
        package=self._shell('dumpsys', 'package', self.PACKAGE)
        assert 'versionCode=7 ' in package and 'versionName=1.2.0' in package
        result=dict(fromCode=6,toCode=7,bytesIdentical=before==after,
                    beforeSha256=hashlib.sha256(before).hexdigest(),afterSha256=hashlib.sha256(after).hexdigest(),
                    apkSha256=hashlib.sha256(Path(apk).read_bytes()).hexdigest())
        self._record('ordinary-upgrade', result)
        (self.output_dir/'upgrade.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
        assert before==after

    def prepare_explicit_near_threshold_fixture(self):
        self._guard()
        self._home_and_wait()
        self.force_stop_cat_game()
        before=self._saved_state('natural-before-fixture.json')
        assert sum(c['count'] for c in before['cats'])==2
        fixture=json.loads(json.dumps(before))
        fixture.update(totalEarned=99990,catHair=200,lastSeenUnix=0,pendingStoryEvents=[])
        # Isolate theme timing from CGs; the real CG ordering has its own Unity integration test.
        source=Path(__file__).resolve().parents[3]/'Assets/Scripts/Idle/StoryCatalog.cs'
        ids=re.findall(r'new StoryDefinition\("([^"]+)"',source.read_text(encoding='utf-8-sig'))
        assert len(ids)==20
        fixture['seenStoryEvents']=ids
        raw=json.dumps(fixture,ensure_ascii=False).encode('utf-8')
        (self.output_dir/'explicit-near-threshold-fixture.json').write_bytes(raw)
        path=f'/data/media/{self.user}/Android/data/{self.PACKAGE}/files/CatGame/save.json'
        # Existing game-owned file only: tee preserves its UID, unlike creating root-owned directories.
        p=subprocess.run([*self.adb, 'su','0','tee',path],input=raw,capture_output=True,timeout=30)
        assert p.returncode==0 and p.stdout.strip()==raw
        self._record('explicit-qa-fixture',dict(totalEarned=99990,catHair=200,seenStories=ids,reason='isolate scene transition, not natural progression'))

    def wait_for_room_pixels(self, theme, name, width=1600):
        self._guard()
        source=Path(__file__).resolve().parents[3]/f'Assets/Resources/Art/Room/Pixel/Growth/{theme}/architecture.png'
        from PIL import Image
        expected=Image.open(source).convert('RGB').getpixel((322,32))
        end=time.monotonic()+30
        while True:
            frame=self._screenshot().convert('RGB')
            if frame.size==(int(width),900):
                scale=max(frame.width/512,frame.height/288)
                point=(round((322-256)*scale+frame.width/2),round((32-144)*scale+frame.height/2))
                actual=frame.getpixel(point)
                if max(abs(actual[i]-expected[i]) for i in range(3))<=2:
                    frame.save(self.output_dir/name)
                    self._record('room-source-pixel',dict(theme=theme,point=point,actual=actual,expected=expected,size=frame.size))
                    return
            if time.monotonic()>end:
                frame.save(self.output_dir/(name+'-failed.png'))
                raise AssertionError(f'{theme} room sample not visible, expected {expected}')
            time.sleep(.25)

    def assert_saved_theme_progress(self, name):
        self._home_and_wait()
        state=self._saved_state(name)
        assert state['totalEarned']>=100000
        assert sum(c['count'] for c in state['cats'])==2
        assert state['introCompleted']
        self._record('saved-theme-progress',dict(totalEarned=state['totalEarned'],owned=2,clicks=state['clicks']))
