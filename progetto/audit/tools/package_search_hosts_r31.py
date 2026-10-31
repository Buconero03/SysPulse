"""Create a lightweight R31 candidate; assert the precise R30 delta, evidence hashes and manifests."""
import argparse,csv,difflib,hashlib,json,shutil,subprocess,tempfile,zipfile
from pathlib import Path
import xml.etree.ElementTree as ET
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--shell',type=Path,required=True);p.add_argument('--baseline-zip',type=Path,required=True);p.add_argument('--output-directory',type=Path,required=True);a=p.parse_args()
shell=a.shell.resolve();r=shell/'progetto';audit=r/'audit/R10_R31';sha=lambda b:hashlib.sha256(b).hexdigest()
assert sha(a.baseline_zip.read_bytes())=='6331971ce39d46cf3653abfec31d08b2d74fc0b50cec3d8f8e1cd6113564f5b5'
with zipfile.ZipFile(a.baseline_zip) as z:before={n.split('/',1)[1]:z.read(n) for n in z.namelist() if not n.endswith('/')}
for d in sorted(shell.rglob('__pycache__'),reverse=True):shutil.rmtree(d)
after={f.relative_to(shell).as_posix():f.read_bytes() for f in shell.rglob('*') if f.is_file()}
modified={'LEGGIMI.txt','supporto/avvia.ps1','progetto/build-dashboard.ps1',*[f'progetto/src/ProcessLens.App/{n}' for n in ['TrayWindowController.cs','WindowActionDiagnostics.cs','ProcessLens.App.csproj','MainWindow.xaml','MainWindow.xaml.cs','ProcessRow.cs','WindowsComponentContext.cs']], 'progetto/src/ProcessLens.Core/WindowsComponentCatalog.cs','progetto/src/ProcessLens.Semantics/RoleClassifier.cs',*[f'progetto/tests/{n}' for n in ['ProcessLens.ProcessGroupsTests/ProcessLens.ProcessGroupsTests.csproj','ProcessLens.ProcessGroupsTests/Program.cs','ProcessLens.ProcessGroupsUiTests/Program.cs','ProcessLens.WindowsComponentTests/Program.cs']]}
added={'progetto/src/ProcessLens.Core/WindowsHostReference.cs',*[f'progetto/audit/tools/{n}' for n in ['run_process_groups_r31.py','typecheck_wpf_sources_r31.py','check_powershell_r31.py','check_search_hosts_r31.ps1','test_launcher_r31.ps1','mutate_search_hosts_r31.py','package_search_hosts_r31.py']]}
manifests={'SHA256SUMS.txt','progetto/SHA256SUMS_R10.txt'}
assert all(n in after for n in before),'No baseline file may disappear'
assert {n for n,b in before.items() if n not in manifests and after[n]!=b}==modified,'Unexpected modification or missing intended change'
assert {n for n in after if n not in before and not n.startswith('progetto/audit/R10_R31/')}==added,'Unexpected new file'
assert not any(n.startswith('progetto/progetto/') for n in after),'Nested duplicate project'
for n,b in after.items():
 if n.endswith(('.xaml','.csproj')):ET.fromstring(b)
assert all(after[n]==b for n,b in before.items() if n not in modified and n.startswith(('progetto/src/ProcessLens.Core/','progetto/src/ProcessLens.Semantics/','progetto/src/ProcessLens.Windows/','progetto/src/ProcessLens.Guardian/','progetto/src/ProcessLens.MemoryAgent/','progetto/src/ProcessLens.Profiler/','progetto/src/ProcessLens.RamMonitor/')))
for n in ['AutomaticViewport.cs','ReadingPositionPolicy.cs','WindowLifetimePolicy.cs','WindowsProcessIconPolicy.cs','IconPreferencePolicy.cs','DeferredDiagnosticWriter.cs','ProcessIconResolver.cs','NativeIconPreference.cs','AppDiagnostics.cs']:
 assert after['progetto/src/ProcessLens.App/'+n]==before['progetto/src/ProcessLens.App/'+n],n
for filename,key in [('runtime-evidence.json','source_hashes'),('wpf-typecheck-evidence.json','inputs')]:
 data=json.loads((audit/filename).read_text());assert data[key]
 for n,h in data[key].items():assert sha((r/n).read_bytes())==h,(filename,n)
runtime=json.loads((audit/'runtime-evidence.json').read_text());assert len(runtime['outcomes'])==13 and all(x['status']=='PASS' for x in runtime['outcomes']);assert sum(x['assertions'] for x in runtime['outcomes'])==822
wpf=json.loads((audit/'wpf-typecheck-evidence.json').read_text());assert len(wpf['assemblies'])==9 and all(x['status']=='PASS' for x in wpf['assemblies'])
ps=json.loads((audit/'powershell-evidence.json').read_text());assert len(ps['stages'])==38 and ps['launcher_fixture_checks']==17
for x in ps['stages']:assert x['status']=='PASS' and sha((r/x['file']).read_bytes())==x['sha256']
assert sha((shell/'supporto/avvia.ps1').read_bytes())==ps['launcher_sha256']
assert sha((r/'audit/tools/test_launcher_r31.ps1').read_bytes())==ps['fixture_sha256']
negative=json.loads((audit/'mutation-evidence.json').read_text());assert len(negative)==11
for x in negative:
 assert x['status']=='PASS' and x['expected_runtime_failure_observed']
 assert sha((r/x['production_file']).read_bytes())==x['production_sha256']
 assert sha((r/'tests/ProcessLens.ProcessGroupsTests/Program.cs').read_bytes())==x['test_sha256']
resource=json.loads((audit/'live-resource-guards.json').read_text());assert len(resource)==14 and all(x['status']=='PASS' for x in resource)
(audit/'scope-evidence.json').write_text(json.dumps({'baseline_sha256':sha(a.baseline_zip.read_bytes()),'modified_files':sorted(modified),'new_files':sorted(added),'protected':'Native acquisition, Guardian, MemoryAgent, profiler, RAM monitor, icon retrieval, scrolling and lifetime policies byte identical to R30; Core/Semantics changes restricted to the declared reference catalog and two role cases','new_dependencies':0,'Windows_runtime_DPI_RAM':'NOT EXECUTED / NOT MEASURED'},indent=2)+'\n')
with (audit/'string-ledger.csv').open('w',newline='') as f:
 w=csv.writer(f);w.writerow(['file','side','changed_text'])
 for n in sorted(modified|added):
  for line in difflib.unified_diff(before.get(n,b'').decode('utf-8-sig').splitlines(),after[n].decode('utf-8-sig').splitlines(),n,n,n=0):
   if line.startswith(('+','-')) and not line.startswith(('+++','---')):w.writerow([n,'after' if line[0]=='+' else 'before',line[1:]])
with (audit/'files-changed.csv').open('w',newline='') as f:
 w=csv.writer(f);w.writerow(['file','kind','sha256_before','sha256_after'])
 for n in sorted(modified|added):w.writerow([n,'added' if n in added else 'modified',sha(before[n]) if n in before else '',sha(after[n])])
include=lambda n:n not in manifests and not n.startswith('progetto/audit/R10_R31/')
with tempfile.TemporaryDirectory(prefix='processlens-r31-') as tmp:
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
 patch=b''.join(lines);path=audit/'R30_to_R31.patch';path.write_bytes(patch)
 x=subprocess.run(['git','apply','--check',str(path)],cwd=bdir,capture_output=True);assert x.returncode==0,x.stderr
 subprocess.run(['git','apply',str(path)],cwd=bdir,check=True)
 assert {f.relative_to(bdir).as_posix():f.read_bytes() for f in bdir.rglob('*') if f.is_file()}=={n:b for n,b in after.items() if include(n)}
(audit/'patch-evidence.json').write_text(json.dumps({'git_apply_check':'PASS','actual_apply_and_byte_comparison':'PASS','patch_sha256':sha(patch),'excluded_from_diff_only':'current R31 audit and recursive manifests; all included in final ZIP'},indent=2)+'\n')
current={f.relative_to(r).as_posix():f.read_bytes() for f in r.rglob('*') if f.is_file() and f.name!='SHA256SUMS_R10.txt'}
(r/'SHA256SUMS_R10.txt').write_text(''.join(sha(b)+'  '+n+'\n' for n,b in sorted(current.items())))
files={f.relative_to(shell).as_posix():f.read_bytes() for f in shell.rglob('*') if f.is_file() and f.name!='SHA256SUMS.txt'}
assert not any(n.endswith(('.dll','.exe','.pdb','.deb','.nupkg','.pyc','.ok')) for n in files),'Build/SDK artifacts forbidden'
assert all(len(b)<10*1024*1024 for b in files.values())
manifest=''.join(sha(b)+'  '+n+'\n' for n,b in sorted(files.items()));files['SHA256SUMS.txt']=manifest.encode();(shell/'SHA256SUMS.txt').write_text(manifest)
a.output_directory.mkdir(parents=True,exist_ok=True);target=a.output_directory/'ProcessLens-R31-ricerca-host-finestre.zip'
with zipfile.ZipFile(target,'w',zipfile.ZIP_DEFLATED,compresslevel=9) as z:
 for n,b in sorted(files.items()):z.writestr('ProcessLens-Leggero/'+n,b)
with zipfile.ZipFile(target) as z:
 assert z.testzip() is None
 for line in manifest.splitlines():h,n=line.split('  ',1);assert sha(z.read('ProcessLens-Leggero/'+n))==h
 for line in (r/'SHA256SUMS_R10.txt').read_text().splitlines():h,n=line.split('  ',1);assert sha(z.read('ProcessLens-Leggero/progetto/'+n))==h
 assert {n.split('/')[1] for n in z.namelist()}=={'AVVIA_PROCESSLENS.cmd','LEGGIMI.txt','supporto','progetto','SHA256SUMS.txt'}
result={'zip':str(target.resolve()),'bytes':target.stat().st_size,'sha256':sha(target.read_bytes()),'manifest_entries':len(files)-1,'portable_assertions':822,'PowerShell_source_stages':38,'launcher_fixture_checks':17,'runtime_negative_controls':11,'resource_guard_checks':14,'CSharp_WindowsDesktop_typechecked_assemblies':9,'Windows_WPF_runtime_DPI_RAM':'NOT EXECUTED / NOT MEASURED'}
(a.output_directory/'verification-r31.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result,indent=2))
