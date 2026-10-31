"""Package R32 with an exact R31 R1 delta, replayable patch and current verification evidence."""
import argparse,csv,difflib,hashlib,json,shutil,subprocess,tempfile,zipfile
from pathlib import Path
import xml.etree.ElementTree as ET
p=argparse.ArgumentParser();p.add_argument('--shell',type=Path,required=True);p.add_argument('--baseline-zip',type=Path,required=True);p.add_argument('--output-directory',type=Path,required=True);a=p.parse_args()
shell=a.shell.resolve();r=shell/'progetto';audit=r/'audit/R10_R32';sha=lambda b:hashlib.sha256(b).hexdigest()
assert sha(a.baseline_zip.read_bytes())=='79a6b2db701b48a80a168bf26fdbee83c86c82c32494cf25dd4701c6a8adc642'
with zipfile.ZipFile(a.baseline_zip) as z:before={n.split('/',1)[1]:z.read(n) for n in z.namelist() if not n.endswith('/')}
for d in sorted(shell.rglob('__pycache__'),reverse=True):shutil.rmtree(d)
after={p.relative_to(shell).as_posix():p.read_bytes() for p in shell.rglob('*') if p.is_file()}
modified=set('''LEGGIMI.txt
supporto/avvia.ps1
progetto/audit/tools/check_scroll_minimize_r26.ps1
progetto/audit/tools/check_search_hosts_r31.ps1
progetto/audit/tools/check_window_diagnostics_r29.ps1
progetto/audit/tools/check_windows_icons_r27.ps1
progetto/build-dashboard.ps1
progetto/docs/terminology/GLOSSARIO_TERMINOLOGICO_PROCESSLENS.csv
progetto/src/ProcessLens.App/App.xaml.cs
progetto/src/ProcessLens.App/MainWindow.xaml
progetto/src/ProcessLens.App/NativeIconPreference.cs
progetto/src/ProcessLens.App/ProcessIconResolver.cs
progetto/src/ProcessLens.App/ProcessLens.App.csproj
progetto/src/ProcessLens.App/TrayWindowController.cs
progetto/src/ProcessLens.App/WindowActionDiagnostics.cs
progetto/src/ProcessLens.App/WindowLifetimePolicy.cs
progetto/src/ProcessLens.App/WindowsProcessIconPolicy.cs
progetto/src/ProcessLens.Core/ProcessDetailPresentation.cs
progetto/tests/ProcessLens.LiveMetricsUiTests/Program.cs
progetto/tests/ProcessLens.ProcessGroupsTests/ProcessLens.ProcessGroupsTests.csproj
progetto/tests/ProcessLens.ProcessGroupsTests/Program.cs
progetto/tests/ProcessLens.ProcessGroupsUiTests/Program.cs
progetto/tests/ProcessLens.TerminologyTests/Program.cs
progetto/tests/check-classification-language-v01911.ps1
progetto/tests/check-terminology-ui.ps1'''.splitlines())
added=set('''progetto/src/ProcessLens.App/ProcessIconPlaceholder.cs
progetto/src/ProcessLens.App/Assets/ProcessLens.ico
progetto/audit/tools/test_launcher_r32.ps1
progetto/audit/tools/check_notification_area_r32.ps1
progetto/audit/tools/check_powershell_r32.py
progetto/audit/tools/typecheck_wpf_sources_r32.py
progetto/audit/tools/package_notification_area_r32.py'''.splitlines())
manifests={'SHA256SUMS.txt','progetto/SHA256SUMS_R10.txt'}
assert set(before)<=set(after),'Deleted baseline file'
assert {n for n,b in before.items() if n not in manifests and after[n]!=b}==modified,'Unexpected source modifications'
assert {n for n in after if n not in before and not n.startswith('progetto/audit/R10_R32/')}==added,'Unexpected additions'
for n,b in after.items():
 if n.endswith(('.csproj','.xaml')):ET.fromstring(b)
for f,k in [('runtime-evidence.json','source_hashes'),('wpf-typecheck-evidence.json','inputs')]:
 d=json.loads((audit/f).read_text())
 for n,h in d[k].items():assert sha((r/n).read_bytes())==h,(f,n,'stale evidence')
runtime=json.loads((audit/'runtime-evidence.json').read_text());assert runtime['assertions']==834 and len(runtime['outcomes'])==13
ps=json.loads((audit/'powershell-evidence.json').read_text());assert len(ps['stages'])==40 and ps['launcher_fixture_checks']==17
for x in ps['stages']:assert sha((r/x['file']).read_bytes())==x['sha256']
assert sha((shell/'supporto/avvia.ps1').read_bytes())==ps['launcher_sha256']
wpf=json.loads((audit/'wpf-typecheck-evidence.json').read_text());assert len(wpf['assemblies'])==9 and all(x['status']=='PASS' for x in wpf['assemblies'])
for area in ['ProcessLens.Windows','ProcessLens.Semantics','ProcessLens.Guardian','ProcessLens.MemoryAgent','ProcessLens.RamMonitor']:
 assert all(after[n]==b for n,b in before.items() if n.startswith('progetto/src/'+area+'/'))
for name in ['AutomaticViewport.cs','ReadingPositionPolicy.cs','ProcessNetworkPresentation.cs','TrayCallbackRouter.cs']:
 n='progetto/src/ProcessLens.App/'+name;assert after[n]==before[n]
(audit/'scope-evidence.json').write_text(json.dumps({'baseline_sha256':sha(a.baseline_zip.read_bytes()),'modified':sorted(modified),'added':sorted(added),'protected':'native acquisition, Semantics, Guardian, MemoryAgent, RAM monitor, network, scroll policies byte identical','new_external_dependencies':0,'Windows_WPF_runtime':'NOT EXECUTED','RAM':'NOT MEASURED'},indent=2)+'\n')
with (audit/'files-changed.csv').open('w',newline='') as f:
 w=csv.writer(f);w.writerow(['file','kind','sha256_before','sha256_after'])
 for n in sorted(modified|added):w.writerow([n,'modified' if n in before else 'added',sha(before[n]) if n in before else '',sha(after[n])])
with (audit/'string-ledger.csv').open('w',newline='') as f:
 w=csv.writer(f);w.writerow(['file','side','changed_text'])
 for n in sorted(modified|added):
  if n.endswith('.ico'):continue
  for line in difflib.unified_diff(before.get(n,b'').decode('utf-8-sig').splitlines(),after[n].decode('utf-8-sig').splitlines(),n,n,n=0):
   if line.startswith(('+','-')) and not line.startswith(('+++','---')):w.writerow([n,'after' if line[0]=='+' else 'before',line[1:]])
include=lambda n:n not in manifests and not n.startswith('progetto/audit/R10_R32/')
with tempfile.TemporaryDirectory(prefix='processlens-r32-') as tmp:
 t=Path(tmp);bdir=t/'before';adir=t/'after';bdir.mkdir();adir.mkdir()
 for base,data in [(bdir,before),(adir,after)]:
  for n,b in data.items():
   if include(n):p=base/n;p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(b)
 x=subprocess.run(['git','diff','--no-index','--no-renames','--binary','--src-prefix=a/','--dst-prefix=b/','before','after'],cwd=t,capture_output=True);assert x.returncode==1
 lines=[]
 for line in x.stdout.splitlines(keepends=True):
  if line.startswith((b'diff --git ',b'--- ',b'+++ ')):
   for prefix in [b'a/before/',b'a/after/']:line=line.replace(prefix,b'a/')
   for prefix in [b'b/before/',b'b/after/']:line=line.replace(prefix,b'b/')
  lines.append(line)
 patch=b''.join(lines);path=audit/'R31_R1_to_R32.patch';path.write_bytes(patch)
 x=subprocess.run(['git','apply','--check',str(path)],cwd=bdir,capture_output=True);assert x.returncode==0,x.stderr
 subprocess.run(['git','apply',str(path)],cwd=bdir,check=True)
 assert {p.relative_to(bdir).as_posix():p.read_bytes() for p in bdir.rglob('*') if p.is_file()}=={n:b for n,b in after.items() if include(n)}
(audit/'patch-evidence.json').write_text(json.dumps({'git_apply_check':'PASS','actual_apply_and_byte_comparison':'PASS','patch_sha256':sha(patch)},indent=2)+'\n')
current={p.relative_to(r).as_posix():p.read_bytes() for p in r.rglob('*') if p.is_file() and p.name!='SHA256SUMS_R10.txt'}
(r/'SHA256SUMS_R10.txt').write_text(''.join(sha(b)+'  '+n+'\n' for n,b in sorted(current.items())))
files={p.relative_to(shell).as_posix():p.read_bytes() for p in shell.rglob('*') if p.is_file() and p.name!='SHA256SUMS.txt'}
assert not any(n.endswith(('.dll','.exe','.pdb','.nupkg','.deb','.pyc','.ok')) for n in files)
assert all(len(b)<10*1024*1024 for b in files.values())
manifest=''.join(sha(b)+'  '+n+'\n' for n,b in sorted(files.items()));files['SHA256SUMS.txt']=manifest.encode();(shell/'SHA256SUMS.txt').write_text(manifest)
a.output_directory.mkdir(parents=True,exist_ok=True);target=a.output_directory/'ProcessLens-R32-area-notifica-icone-testi.zip'
with zipfile.ZipFile(target,'w',zipfile.ZIP_DEFLATED,compresslevel=9) as z:
 for n,b in sorted(files.items()):z.writestr('ProcessLens-Leggero/'+n,b)
with zipfile.ZipFile(target) as z:
 assert z.testzip() is None
 for line in manifest.splitlines():h,n=line.split('  ',1);assert sha(z.read('ProcessLens-Leggero/'+n))==h
 for line in (r/'SHA256SUMS_R10.txt').read_text().splitlines():h,n=line.split('  ',1);assert sha(z.read('ProcessLens-Leggero/progetto/'+n))==h
 assert {n.split('/')[1] for n in z.namelist()}=={'AVVIA_PROCESSLENS.cmd','LEGGIMI.txt','supporto','progetto','SHA256SUMS.txt'}
result={'zip':str(target),'bytes':target.stat().st_size,'sha256':sha(target.read_bytes()),'manifest_entries':len(files)-1,'actual_MSBuild_projects':13,'assertions':834,'PS_stages':40,'launcher_checks':17,'CSharp_WindowsDesktop_stubs_only':9,'Windows_WPF_runtime':'NOT EXECUTED','RAM':'NOT MEASURED'}
(a.output_directory/'verification-r32.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result,indent=2))
