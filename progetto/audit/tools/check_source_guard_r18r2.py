"""Execute the source-only PowerShell preflight and regression mutations; no WPF or network acquisition."""
import argparse,hashlib,json,platform,re,subprocess,tempfile,zipfile
from pathlib import Path

def main():
 p=argparse.ArgumentParser(description=__doc__)
 p.add_argument('--candidate',type=Path,required=True);p.add_argument('--baseline-zip',type=Path,required=True)
 p.add_argument('--pwsh',type=Path,required=True);a=p.parse_args();r=a.candidate.resolve();audit=r/'audit/R10_R18_R2';exe=a.pwsh.resolve()
 sha=lambda b:hashlib.sha256(b).hexdigest()
 assert sha(a.baseline_zip.read_bytes())=='066cff3add196c5b25e3e293b1cbac92c89f309930b9f472c45c8ebc25b249a4'
 with zipfile.ZipFile(a.baseline_zip) as z:old={n.split('/',1)[1]:z.read(n) for n in z.namelist() if not n.endswith('/')}
 rows=[];logs=[]
 def check(name,ok):
  rows.append({'test':name,'status':'PASS' if ok else 'FAIL'})
  print(rows[-1]['status'],name)
 def run(path):
  x=subprocess.run([str(exe),'-NoLogo','-NoProfile','-NonInteractive','-File',str(path)],capture_output=True,text=True,timeout=45)
  return x.returncode,x.stdout+x.stderr
 guard='tests/check-copy-layout-r18r1.ps1';test='tests/ProcessLens.CopyUiTests/Program.cs';original=(r/test).read_text();current=(r/guard).read_text()
 with tempfile.TemporaryDirectory(prefix='source-guard-') as d:
  root=Path(d);(root/'tests/ProcessLens.CopyUiTests').mkdir(parents=True);g=root/guard;t=root/test
  g.write_bytes(old[guard]);t.write_text(original)
  code,log=run(g);logs.append('ORIGINAL R18 R1 GUARD (expected failure)\n'+log)
  check('actual old PowerShell gate reproduces reported false positive',code!=0 and 'must retain native theme without showing windows' in log)
  g.write_text(current)
  fixtures=[('current unchanged WPF fixture',original,True,None),
   ('real Template assignment rejected',original+'\ntext.Template = new ControlTemplate();',False,'must retain native theme'),
   ('real Show call rejected',original+'\nwindow.Show();',False,'must retain native theme'),
   ('initializer assignment rejected',original+'\nnew TextBox { Template = value };',False,'must retain native theme'),
   ('uppercase diagnostics and comments allowed',original+'\n// Template = value; window.Show();\nvar s = "Template= window.Show()"; /* Template=value; */',True,None),
   ('template equality is not assignment',original+'\nif (text.Template == null) return;',True,None),
   ('missing original height assertion rejected',original.replace('text.DesiredSize.Height > 20','text.DesiredSize.Height > 0'),False,'WPF layout test:'),
   ('missing deferred work bound rejected',original.replace('passes < 16','passes < int.MaxValue'),False,'WPF layout test:')]
  for name,source,success,message in fixtures:
   t.write_text(source);code,log=run(g);logs.append(name+'\n'+log)
   check(name,(code==0)==success and (not message or message in log))
 # Execute every source-only gate in the build's declared order; no dotnet stages.
 build=(r/'build-dashboard.ps1').read_text(encoding='utf-8-sig')
 gates=re.findall(r"^& \(Join-Path \$PSScriptRoot '(tests\\check-[^']+\.ps1)'\)",build,re.M)
 check('all thirty source preflight stages discovered',len(gates)==30)
 for relative in gates:
  path=r/relative.replace('\\','/');code,log=run(path);logs.append(relative+'\n'+log)
  check('actual PowerShell preflight '+path.name,code==0)
 ledger=json.loads((audit/'authorized-source-edits.json').read_text())['files']
 check('exact two-file authorized source/test delta',{x['file'] for x in ledger}=={guard,'src/ProcessLens.App/ProcessLens.App.csproj'} and all(sha((r/x['file']).read_bytes())==x['sha256_after'] and sha(old[x['file']])==x['sha256_before'] for x in ledger))
 check('all baseline files retained',all((r/n).is_file() for n in old))
 check('all production C# XAML and C# tests unchanged',all((r/n).read_bytes()==b for n,b in old.items() if (n.startswith('src/') or n.startswith('tests/')) and n.endswith(('.cs','.xaml'))))
 check('standard build pipeline unchanged',(r/'build-dashboard.ps1').read_bytes()==old['build-dashboard.ps1'])
 project='src/ProcessLens.App/ProcessLens.App.csproj'
 check('project changes candidate identity only',(r/project).read_bytes()==old[project].replace(b'R18-R1-wpf-layout-candidate',b'R18-R2-source-guard-candidate'))
 for name in ['runtime-evidence.json','wpf-typecheck-evidence.json','syntax-evidence.json']:
  evidence=json.loads((audit/name).read_text());key='source_hashes' if 'source_hashes' in evidence else 'inputs'
  check('retained unchanged inputs '+name,all(sha((r/n).read_bytes())==h for n,h in evidence[key].items()) and 'Not rerun' in evidence['R18_R2_retention'])
 version=subprocess.run([str(exe),'-NoLogo','-NoProfile','-Command','$PSVersionTable.PSVersion.ToString()'],capture_output=True,text=True,timeout=15)
 (audit/'powershell-execution.log').write_text('\n\n'.join(logs))
 evidence={'platform':platform.system(),'PowerShell':version.stdout.strip(),'Windows_PowerShell_5_1':'NOT EXECUTED','Windows_WPF_runtime':'NOT EXECUTED','results':rows,'guard_sha256':sha((r/guard).read_bytes()),'fixture_sha256':sha((r/test).read_bytes()),'preflight_hashes':{n.replace('\\','/'):sha((r/n.replace('\\','/')).read_bytes()) for n in gates},'pwsh_executable_sha256':sha(exe.read_bytes())}
 (audit/'powershell-evidence.json').write_text(json.dumps(evidence,indent=2)+'\n')
 raise SystemExit(0 if all(x['status']=='PASS' for x in rows) else 1)
if __name__=='__main__':main()
