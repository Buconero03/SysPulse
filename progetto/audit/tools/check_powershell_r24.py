"""Run PowerShell source preflight and isolated launcher fixtures, never a build or installation."""
import argparse,json,hashlib,subprocess
from pathlib import Path
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project',type=Path,required=True);p.add_argument('--shell',type=Path,required=True);p.add_argument('--pwsh',type=Path,required=True);a=p.parse_args()
r=a.project.resolve();shell=a.shell.resolve();audit=r/'audit/R10_R24';sha=lambda b:hashlib.sha256(b).hexdigest()
# Same 30 authorized baseline source stages, plus the R24 wiring gate.
stages=[x['file'] for x in json.loads((r/'audit/R10_R23_R1/powershell-evidence.json').read_text())['stages']]+['audit/tools/check_live_metrics_r24.ps1'];results=[];logs=[]
for n in stages:
 x=subprocess.run([str(a.pwsh.resolve()),'-NoLogo','-NoProfile','-File',str(r/n)],cwd=r,capture_output=True,text=True,timeout=60);log=x.stdout+x.stderr;logs.append(n+'\n'+log)
 if x.returncode:(audit/'preflight.log').write_text('\n'.join(logs));raise SystemExit(log)
 results.append({'file':n,'sha256':sha((r/n).read_bytes()),'status':'PASS'})
(audit/'preflight.log').write_text('\n'.join(logs))
fixture=r/'audit/tools/test_launcher_r24.ps1';launcher=shell/'supporto/avvia.ps1'
x=subprocess.run([str(a.pwsh.resolve()),'-NoLogo','-NoProfile','-File',str(fixture),'-Launcher',str(launcher)],capture_output=True,text=True,timeout=60);log=x.stdout+x.stderr;(audit/'launcher-fixture.log').write_text(log)
if x.returncode:raise SystemExit(log)
count=sum(line.startswith('PASS R24 launcher:') for line in log.splitlines());assert count==17,count
(audit/'powershell-evidence.json').write_text(json.dumps({'PowerShell':'7.4.21 Linux; no Windows runtime execution','stages':results,'launcher_fixture_checks':count,'launcher_sha256':sha(launcher.read_bytes()),'fixture_sha256':sha(fixture.read_bytes())},indent=2)+'\n');print('PASS',len(results),'source stages;',count,'launcher checks')
