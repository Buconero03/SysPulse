"""Create a lightweight R32 R2 candidate; assert the precise R32 R1 delta, evidence hashes and manifests."""
import argparse,csv,difflib,hashlib,json,shutil,subprocess,tempfile,zipfile
from pathlib import Path
import xml.etree.ElementTree as ET
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--shell',type=Path,required=True);p.add_argument('--baseline-zip',type=Path,required=True);p.add_argument('--output-directory',type=Path,required=True);a=p.parse_args()
shell=a.shell.resolve();r=shell/'progetto';audit=r/'audit/R10_R32_R2';sha=lambda b:hashlib.sha256(b).hexdigest()
assert sha(a.baseline_zip.read_bytes())=='ff5e50322cd46997ad056b118b0abd76f27d88854bbdf8bbeac32db963053909'
with zipfile.ZipFile(a.baseline_zip) as z:before={n.split('/',1)[1]:z.read(n) for n in z.namelist() if not n.endswith('/')}
for d in sorted(shell.rglob('__pycache__'),reverse=True):shutil.rmtree(d)
after={f.relative_to(shell).as_posix():f.read_bytes() for f in shell.rglob('*') if f.is_file()}
modified={'LEGGIMI.txt','supporto/avvia.ps1','progetto/build-dashboard.ps1','progetto/audit/tools/check_wpf_path_r32r1.ps1','progetto/src/ProcessLens.App/MainWindow.xaml','progetto/src/ProcessLens.App/MainWindow.xaml.cs','progetto/src/ProcessLens.App/TrayWindowController.cs','progetto/src/ProcessLens.App/ProcessLens.App.csproj','progetto/tests/ProcessLens.ProcessGroupsUiTests/Program.cs'}
added={'progetto/src/ProcessLens.App/IconFile.cs','progetto/src/ProcessLens.App/ProcessLensIcon.cs','progetto/tests/ProcessLens.IconResourceTests/Program.cs','progetto/tests/ProcessLens.IconResourceTests/ProcessLens.IconResourceTests.csproj'}|{f'progetto/audit/tools/{n}' for n in ['typecheck_wpf_sources_r32r2.py','check_powershell_r32r2.py','check_embedded_icon_r32r2.ps1','test_launcher_r32r2.ps1','package_embedded_icon_r32r2.py']}
manifests={'SHA256SUMS.txt','progetto/SHA256SUMS_R10.txt'}
assert all(n in after for n in before),'No baseline file may disappear'
assert {n for n,b in before.items() if n not in manifests and after[n]!=b}==modified,'Unexpected modification or missing intended change'
assert {n for n in after if n not in before and not n.startswith('progetto/audit/R10_R32_R2/')}==added,'Unexpected new file'
assert not any(n.startswith('progetto/progetto/') for n in after),'Nested duplicate project'
for n,b in after.items():
 if n.endswith(('.xaml','.csproj')):ET.fromstring(b)
assert all(after[n]==b for n,b in before.items() if n not in modified and n.startswith(('progetto/src/ProcessLens.Core/','progetto/src/ProcessLens.Semantics/','progetto/src/ProcessLens.Windows/','progetto/src/ProcessLens.Guardian/','progetto/src/ProcessLens.MemoryAgent/','progetto/src/ProcessLens.Profiler/','progetto/src/ProcessLens.RamMonitor/')))
for n in ['AutomaticViewport.cs','ReadingPositionPolicy.cs','WindowLifetimePolicy.cs','IconPreferencePolicy.cs','DeferredDiagnosticWriter.cs','ProcessIconResolver.cs','NativeIconPreference.cs','AppDiagnostics.cs']:
 assert after['progetto/src/ProcessLens.App/'+n]==before['progetto/src/ProcessLens.App/'+n],n
d=json.loads((audit/'wpf-typecheck-evidence.json').read_text())
assert len(d['assemblies'])==9 and all(x['status']=='PASS' for x in d['assemblies'])
assert 'System.IO' not in d['evaluated_wpf_implicit_usings']
for n,h in d['inputs'].items():assert sha((r/n).read_bytes())==h,(n,'stale typecheck input')
ps=json.loads((audit/'powershell-evidence.json').read_text());assert len(ps['stages'])==42 and ps['launcher_fixture_checks']==17
for row in ps['stages']:assert sha((r/row['file']).read_bytes())==row['sha256']
assert sha((shell/'supporto/avvia.ps1').read_bytes())==ps['launcher_sha256']
runtime=json.loads((audit/'runtime-evidence.json').read_text());assert runtime['checks']==36
for n,h in runtime['source_hashes'].items():assert sha((r/n).read_bytes())==h,(n,'stale runtime input')
assert 'Build succeeded.' in (audit/'icon-msbuild.log').read_text()
assert sum(l.startswith('PASS') for l in (audit/'icon-single-file-run.log').read_text().splitlines())==19
items=json.loads((audit/'msbuild-wpf-usings.json').read_text())['Items']
assert not any('ProcessLens.ico' in x['Identity'] for x in items['Content'])
assert any(x.get('LogicalName')=='ProcessLens.AppIcon' for x in items['EmbeddedResource'])
assert ' Icon=' not in (r/'src/ProcessLens.App/MainWindow.xaml').read_text().split('>')[0]
(audit/'scope-evidence.json').write_text(json.dumps({'baseline_sha256':sha(a.baseline_zip.read_bytes()),'modified_files':sorted(modified),'new_files':sorted(added),'protected':'Core, Semantics, native process/network acquisition, Guardian, metrics, window policies and icon asset byte identical. Only optional application icon pipeline and declared test/build/version files changed','new_dependencies':0,'Windows_runtime_DPI_RAM':'NOT EXECUTED / NOT MEASURED'},indent=2)+'\n')
with (audit/'string-ledger.csv').open('w',newline='') as f:
 w=csv.writer(f);w.writerow(['file','side','changed_text'])
 for n in sorted(modified|added):
  for line in difflib.unified_diff(before.get(n,b'').decode('utf-8-sig').splitlines(),after[n].decode('utf-8-sig').splitlines(),n,n,n=0):
   if line.startswith(('+','-')) and not line.startswith(('+++','---')):w.writerow([n,'after' if line[0]=='+' else 'before',line[1:]])
with (audit/'files-changed.csv').open('w',newline='') as f:
 w=csv.writer(f);w.writerow(['file','kind','sha256_before','sha256_after'])
 for n in sorted(modified|added):w.writerow([n,'added' if n in added else 'modified',sha(before[n]) if n in before else '',sha(after[n])])
include=lambda n:n not in manifests and not n.startswith('progetto/audit/R10_R32_R2/')
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
 patch=b''.join(lines);path=audit/'R32_R1_to_R32_R2.patch';path.write_bytes(patch)
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
a.output_directory.mkdir(parents=True,exist_ok=True);target=a.output_directory/'ProcessLens-R32-R2-correzione-icona-avvio.zip'
with zipfile.ZipFile(target,'w',zipfile.ZIP_DEFLATED,compresslevel=9) as z:
 for n,b in sorted(files.items()):z.writestr('ProcessLens-Leggero/'+n,b)
with zipfile.ZipFile(target) as z:
 assert z.testzip() is None
 for line in manifest.splitlines():h,n=line.split('  ',1);assert sha(z.read('ProcessLens-Leggero/'+n))==h
 for line in (r/'SHA256SUMS_R10.txt').read_text().splitlines():h,n=line.split('  ',1);assert sha(z.read('ProcessLens-Leggero/progetto/'+n))==h
 assert {n.split('/')[1] for n in z.namelist()}=={'AVVIA_PROCESSLENS.cmd','LEGGIMI.txt','supporto','progetto','SHA256SUMS.txt'}
result={'zip':str(target.resolve()),'bytes':target.stat().st_size,'sha256':sha(target.read_bytes()),'manifest_entries':len(files)-1,'R32_historical_assertions_not_rerun':834,'PowerShell_source_stages':42,'launcher_fixture_checks':17,'new_icon_assertions':36,'real_single_file_fixture':'PASS linux-x64; WPF Windows NOT EXECUTED','CSharp_WindowsDesktop_typechecked_assemblies':9,'Windows_WPF_runtime_DPI_RAM':'NOT EXECUTED / NOT MEASURED'}
(a.output_directory/'verification-r32r2.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result,indent=2))
