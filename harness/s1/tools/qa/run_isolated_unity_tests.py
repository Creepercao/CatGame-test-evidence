"""Run Unity CLI tests with an explicit project-local Editor save root."""
import argparse,json,os,shutil,subprocess
from pathlib import Path
p=argparse.ArgumentParser()
p.add_argument('--output',required=True)
p.add_argument('--filter',required=True)
args=p.parse_args()
root=Path(__file__).resolve().parents[2]
out=(root/args.output).resolve();out.relative_to(root)
out.mkdir(exist_ok=False)
env=dict(os.environ,CATGAME_EDITOR_QA_ROOT=str(out/'isolated-save'))
cli=Path(os.environ['LOCALAPPDATA'])/'Unity/bin/unity.exe'
command=[str(cli),'test',str(root),'--mode','PlayMode','--filter',args.filter,'--output',str(out/'results.xml'),'--timeout','600','--non-interactive']
with (out/'cli.log').open('wb') as log:
    run=subprocess.run(command,env=env,stdout=log,stderr=subprocess.STDOUT,timeout=660)
shutil.copyfile(Path(os.environ['LOCALAPPDATA'])/'Unity/Editor/Editor.log',out/'Editor.log')
(out/'exit-code.txt').write_text(str(run.returncode)+'\n',encoding='utf-8')
source=subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip()
(out/'source.txt').write_text(source+'\n',encoding='utf-8')
(out/'isolation.json').write_text(json.dumps(dict(editorQaRoot=str(out/'isolated-save'),source=source,exitCode=run.returncode),indent=2),encoding='utf-8')
raise SystemExit(run.returncode)
