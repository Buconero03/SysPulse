"""Package the complete lightweight R28 source candidate and verify exact scope, evidence, patch and hashes."""
import argparse,csv,difflib,hashlib,json,shutil,subprocess,tempfile,zipfile
from pathlib import Path
import xml.etree.ElementTree as ET
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project',type=Path,required=True);p.add_argument('--shell',type=Path,required=True);p.add_argument('--baseline-zip',type=Path,required=True);p.add_argument('--output-directory',type=Path,required=True);a=p.parse_args()
r=a.project.resolve();shell=a.shell.resolve();audit=r/'audit/R10_R28';sha=lambda b:hashlib.sha256(b).hexdigest()
assert sha(a.baseline_zip.read_bytes())=='554bad70574c36e6045bcfcf8fa93b815aff943f6892644ae51815f4d7aedd69'
with zipfile.ZipFile(a.baseline_zip) as z:before={n.split('/',1)[1]:z.read(n) for n in z.namelist() if not n.endswith('/')}
for base in [r,shell]:
 for d in sorted(base.rglob('__pycache__'),reverse=True):shutil.rmtree(d)
current={f.relative_to(r).as_posix():f.read_bytes() for f in r.rglob('*') if f.is_file()}
old={n[len('progetto/'):]:b for n,b in before.items() if n.startswith('progetto/')}
allowed = {'src/ProcessLens.App/AppDiagnostics.cs', 'src/ProcessLens.App/IconPreferencePolicy.cs', 'src/ProcessLens.App/ProcessIconResolver.cs', 'src/ProcessLens.App/ProcessLens.App.csproj', 'tests/ProcessLens.ProcessGroupsTests/Program.cs', 'src/ProcessLens.App/App.xaml.cs', 'tests/ProcessLens.ProcessGroupsTests/ProcessLens.ProcessGroupsTests.csproj', 'src/ProcessLens.App/TrayWindowController.cs', 'src/ProcessLens.App/MainWindow.xaml.cs', 'src/ProcessLens.App/DeferredDiagnosticWriter.cs', 'src/ProcessLens.App/WindowLifetimePolicy.cs', 'tests/ProcessLens.ProcessGroupsUiTests/Program.cs'}
changes={n for n,b in current.items() if n.startswith(('src/','tests/')) and old.get(n)!=b}
assert changes==allowed,(changes,allowed)
extra_tools = {'audit/tools/'+name for name in ['check_powershell_r28.py','check_window_controls_r28.ps1','mutate_window_controls_r28.py','package_window_controls_r28.py','run_process_groups_r28.py','test_launcher_r28.ps1','typecheck_wpf_sources_r28.py']}
forward_guards = {'audit/tools/check_process_groups_r25.ps1','audit/tools/check_scroll_minimize_r26.ps1','audit/tools/check_windows_icons_r27.ps1','build-dashboard.ps1','SHA256SUMS_R10.txt'}
assert not any(n.startswith('progetto/') for n in current), 'Nested duplicate project is not allowed'
assert all(n in old or n in allowed or n in extra_tools or n.startswith('audit/R10_R28/') for n in current), 'Unexpected project file'
assert all(current[n]==b for n,b in old.items() if n not in allowed|forward_guards), 'Historical files outside scope must stay byte identical'

assert current['src/ProcessLens.Core/NewTaskLaunch.cs']==old['src/ProcessLens.Core/NewTaskLaunch.cs'], 'R23 R1 NUL fix must remain byte identical'
assert current['tests/ProcessLens.NewTaskTests/Program.cs']==old['tests/ProcessLens.NewTaskTests/Program.cs']
for name in ['WindowsLiveCpuSampler.cs','WindowsMemorySampler.cs','WindowsProcessCollector.cs']:
 assert current['src/ProcessLens.Windows/'+name]==old['src/ProcessLens.Windows/'+name], 'R24 CPU/RAM sampling must remain byte identical'
assert b'Tests.ProcessLens.NewTaskTests' in current['build-dashboard.ps1'] and b'Tests.ProcessLens.NewTaskUiTests' in current['build-dashboard.ps1']
assert all(n in current for n in old), 'No R23 project file may disappear'
assert all(current[n]==b for n,b in old.items() if n not in allowed and n.startswith(('src/ProcessLens.Windows/','src/ProcessLens.Guardian/','src/ProcessLens.MemoryAgent/','src/ProcessLens.Profiler/','src/ProcessLens.RamMonitor/')))
for n in ['src/ProcessLens.App/AutomaticViewport.cs','src/ProcessLens.App/ReadingPositionPolicy.cs','src/ProcessLens.App/MainWindow.xaml','src/ProcessLens.App/WindowsProcessIconPolicy.cs']:
 assert current[n]==old[n], 'R27 layout, scrolling and Windows classification must remain byte identical'
assert all(current[n]==b for n,b in old.items() if n.startswith(('src/ProcessLens.Core/','src/ProcessLens.Semantics/')))
assert b'Tests.ProcessLens.MainWindowShell' in current['build-dashboard.ps1'] and b'--window-smoke' in current['build-dashboard.ps1']
for n,b in current.items():
 if n.endswith(('.xaml','.csproj')):ET.fromstring(b)
for name,key in [('runtime-evidence.json','source_hashes'),('wpf-typecheck-evidence.json','inputs')]:
 e=json.loads((audit/name).read_text());assert e[key]
 assert all(sha(current[n])==h for n,h in e[key].items()), name
runtime=json.loads((audit/'runtime-evidence.json').read_text());assert [x['assertions'] for x in runtime['outcomes']]==[103,22,48,27,20,17,9,46,52,133,86,70,104]
types=json.loads((audit/'wpf-typecheck-evidence.json').read_text());assert len(types['assemblies'])==9 and all(x['status']=='PASS' for x in types['assemblies'])
e=json.loads((audit/'powershell-evidence.json').read_text());assert e['launcher_fixture_checks']==17 and len(e['stages'])==35
assert sha((shell/'supporto/avvia.ps1').read_bytes())==e['launcher_sha256']
assert all(x['status']=='PASS' and sha(current[x['file']])==x['sha256'] for x in e['stages'])
assert sha(current['audit/tools/test_launcher_r28.ps1'])==e['fixture_sha256']
(audit/'scope-evidence.json').write_text(json.dumps({'baseline_sha256':sha(a.baseline_zip.read_bytes()),'changed_production_and_test_files':sorted(changes),'protected_components':'Guardian, MemoryAgent, profiler, RAM monitor, R24 CPU/RAM samplers and other native acquisitions BYTE IDENTICAL TO R27; all Core and native acquisitions unchanged','new_dependencies':0,'new_binary':'NOT INCLUDED; full Windows build required once','WPF_runtime_DPI_UAC_RAM':'NOT EXECUTED / NOT MEASURED'},indent=2)+'\n')
negative=json.loads((audit/'mutation-evidence.json').read_text());assert len(negative)==4
for item in negative:
 assert item['expected_runtime_failure_observed'] and item['status']=='PASS'
 assert item['production_sha256']==sha(current[item['production_file']])
 assert item['test_sha256']==sha(current['tests/ProcessLens.ProcessGroupsTests/Program.cs'])
guards=json.loads((audit/'live-resource-guards.json').read_text());assert len(guards)==14 and all(x['status']=='PASS' for x in guards)
with (audit/'string-ledger.csv').open('w',newline='') as f:
 w=csv.writer(f);w.writerow(['file','line','before_or_after','changed_text'])
 for n in sorted(changes):
  for line in difflib.unified_diff(old.get(n,b'').decode('utf-8-sig').splitlines(),current[n].decode('utf-8-sig').splitlines(),n,n,n=0):
   if line.startswith(('+','-')) and not line.startswith(('+++','---')):w.writerow([n,'see patch','after' if line[0]=='+' else 'before',line[1:]])
current={f.relative_to(r).as_posix():f.read_bytes() for f in r.rglob('*') if f.is_file()}
after={**{f.relative_to(shell).as_posix():f.read_bytes() for f in shell.rglob('*') if f.is_file() and f.name!='SHA256SUMS.txt'},**{'progetto/'+n:b for n,b in current.items()}}
include=lambda n:not n.startswith('progetto/audit/R10_R28/') and n not in {'SHA256SUMS.txt','progetto/SHA256SUMS_R10.txt'}
delta=sorted(n for n in set(before)|set(after) if include(n) and before.get(n)!=after.get(n))
with (audit/'files-changed.csv').open('w',newline='') as f:
 w=csv.writer(f);w.writerow(['file','kind','sha256_before','sha256_after'])
 for n in delta:w.writerow([n,'removed' if n not in after else 'added' if n not in before else 'modified',sha(before[n]) if n in before else '',sha(after[n]) if n in after else ''])
with tempfile.TemporaryDirectory(prefix='patch-r23r1-',dir=r.parent.parent/'test') as d:
 t=Path(d);bdir=t/'before';adir=t/'after';bdir.mkdir();adir.mkdir()
 for base,values in [(bdir,before),(adir,after)]:
  for n,b in values.items():
   if include(n):f=base/n;f.parent.mkdir(parents=True,exist_ok=True);f.write_bytes(b)
 x=subprocess.run(['git','diff','--no-index','--no-renames','--binary','--src-prefix=a/','--dst-prefix=b/','before','after'],cwd=t,capture_output=True);assert x.returncode==1
 lines=[]
 for line in x.stdout.splitlines(keepends=True):
  if line.startswith((b'diff --git ',b'--- ',b'+++ ')):
   for prefix in [b'a/before/',b'a/after/']:line=line.replace(prefix,b'a/')
   for prefix in [b'b/before/',b'b/after/']:line=line.replace(prefix,b'b/')
  lines.append(line)
 patch=b''.join(lines);path=audit/'R27_to_R28.patch';path.write_bytes(patch)
 x=subprocess.run(['git','apply','--check',str(path)],cwd=bdir,capture_output=True);assert x.returncode==0,x.stderr
 subprocess.run(['git','apply',str(path)],cwd=bdir,capture_output=True,check=True)
 assert {f.relative_to(bdir).as_posix():f.read_bytes() for f in bdir.rglob('*') if f.is_file()}=={n:b for n,b in after.items() if include(n)}
(audit/'patch-evidence.json').write_text(json.dumps({'git_apply_check':'PASS','actual_apply_and_byte_comparison':'PASS','delta_files':len(delta),'patch_sha256':sha(patch),'qualification':'Current audit folder and recursive manifests excluded from diff only, included in final full ZIP'},indent=2)+'\n')
current={f.relative_to(r).as_posix():f.read_bytes() for f in r.rglob('*') if f.is_file() and f.name!='SHA256SUMS_R10.txt'}
(r/'SHA256SUMS_R10.txt').write_text(''.join(sha(b)+'  '+n+'\n' for n,b in sorted(current.items())))
current['SHA256SUMS_R10.txt']=(r/'SHA256SUMS_R10.txt').read_bytes()
files={**{f.relative_to(shell).as_posix():f.read_bytes() for f in shell.rglob('*') if f.is_file() and f.name!='SHA256SUMS.txt'},**{'progetto/'+n:b for n,b in current.items()}}
assert not any(n.endswith(('.nupkg','.deb','.dll','.exe','.pdb')) or '/setup/payload/' in n or n.endswith('build-r28.ok') for n in files)
assert not any(n.startswith(('sdk/','runtime/','app/')) for n in files)
assert all(len(b)<10*1024*1024 for b in files.values())
manifest=''.join(sha(b)+'  '+n+'\n' for n,b in sorted(files.items()));files['SHA256SUMS.txt']=manifest.encode()
out=a.output_directory.resolve();out.mkdir(exist_ok=True,parents=True);target=out/'ProcessLens-R28-icone-originali-riduzione.zip'
with zipfile.ZipFile(target,'w',zipfile.ZIP_DEFLATED,compresslevel=9) as z:
 for n,b in sorted(files.items()):z.writestr('ProcessLens-Leggero/'+n,b)
with zipfile.ZipFile(target) as z:
 assert z.testzip() is None
 for line in manifest.splitlines():
  h,n=line.split('  ',1);assert sha(z.read('ProcessLens-Leggero/'+n))==h
 assert {n.split('/')[1] for n in z.namelist()}=={'AVVIA_PROCESSLENS.cmd','LEGGIMI.txt','supporto','progetto','SHA256SUMS.txt'}
result={'zip':str(target),'bytes':target.stat().st_size,'sha256':sha(target.read_bytes()),'manifest_entries':len(files)-1,'portable_assertions':737,'PowerShell_fixtures':17,'source_preflight_stages':35,'WPF_CSharp_typechecked_assemblies':9,'WPF_runtime_DPI_RAM':'NOT EXECUTED / NOT MEASURED'}
(out/'verification-r28.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result,indent=2))
