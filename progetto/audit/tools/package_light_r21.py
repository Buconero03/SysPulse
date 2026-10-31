"""R21 lightweight sources plus fast launcher for the user's completed R20 installation; no new app binary."""
import argparse,csv,hashlib,json,shutil,subprocess,tempfile,zipfile
from pathlib import Path
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project',type=Path,required=True);p.add_argument('--shell',type=Path,required=True);p.add_argument('--baseline-zip',type=Path,required=True);p.add_argument('--output-directory',type=Path,required=True);a=p.parse_args()
r=a.project.resolve();shell=a.shell.resolve();audit=r/'audit/R10_R21';sha=lambda b:hashlib.sha256(b).hexdigest()
assert sha(a.baseline_zip.read_bytes())=='3f51f87d17a3c8763e27e34072663b747473e10f2fc38cc92c638fc303248f7e'
with zipfile.ZipFile(a.baseline_zip) as z:
 old={n.split('/',1)[1]:z.read(n) for n in z.namelist() if not n.endswith('/') and not n.split('/',1)[1].startswith('setup/payload/')}
 payload=[{'file':n.split('/',1)[1],'bytes':z.getinfo(n).file_size,'compressed_bytes':z.getinfo(n).compress_size} for n in z.namelist() if '/setup/payload/' in n]
assert all((r/n).read_bytes()==b for n,b in old.items() if n.startswith(('src/','tests/')))
assert (r/'build-dashboard.ps1').read_bytes()==old['build-dashboard.ps1']
retired=json.loads((audit/'retired-r20-files.json').read_text());assert len(retired)==7
assert all(not (r/x['previous_path']).exists() and (r/x['archive_path']).read_bytes()==old[x['previous_path']] for x in retired)
e=json.loads((audit/'powershell-evidence.json').read_text());assert e['launcher_fixture_checks']==16 and len(e['stages'])==30 and all(x['status']=='PASS' and sha((r/x['file']).read_bytes())==x['sha256'] for x in e['stages'])
assert sha((shell/'supporto/avvia.ps1').read_bytes())==e['launcher_sha256']
assert '75 script verificati' in (audit/'preflight.log').read_text()
for base in [r,shell]:
 for d in sorted(base.rglob('__pycache__'),reverse=True):shutil.rmtree(d)
(audit/'scope-evidence.json').write_text(json.dumps({'baseline_sha256':sha(a.baseline_zip.read_bytes()),'existing_src_and_tests':'BYTE IDENTICAL','build_pipeline':'BYTE IDENTICAL','retired_R20_files_archived':7,'new_binary':'NOT INCLUDED','Windows_launch_UAC':'NOT EXECUTED'},indent=2)+'\n')
with (audit/'excluded-payload.csv').open('w',newline='') as f:
 w=csv.DictWriter(f,fieldnames=['file','bytes','compressed_bytes']);w.writeheader();w.writerows(payload)
with (audit/'string-ledger.csv').open('w',newline='') as f:
 w=csv.writer(f);w.writerow(['file','line','source_text','scope'])
 for path in sorted(shell.rglob('*')):
  if path.is_file():
   for i,s in enumerate(path.read_text().splitlines(),1):w.writerow([path.relative_to(shell).as_posix(),i,s,'Fast launcher or instructions; no application text changes'])
current={f.relative_to(r).as_posix():f.read_bytes() for f in r.rglob('*') if f.is_file() and f.name!='SHA256SUMS_R10.txt'}
light={f.relative_to(shell).as_posix():f.read_bytes() for f in shell.rglob('*') if f.is_file() and f.name!='SHA256SUMS.txt'}
# Compare R20 source layout mapped beneath progetto with the new source+launcher layout.
include=lambda n:not n.startswith('progetto/audit/R10_R21/') and n not in {'SHA256SUMS.txt','progetto/SHA256SUMS_R10.txt'}
before_values={'progetto/'+n:b for n,b in old.items() if n!='SHA256SUMS_R10.txt'}
after_values={**light,**{'progetto/'+n:b for n,b in current.items()}}
delta=sorted(n for n in set(before_values)|set(after_values) if include(n) and before_values.get(n)!=after_values.get(n))
with (audit/'files-changed.csv').open('w',newline='') as f:
 w=csv.writer(f);w.writerow(['file','kind','sha256_before','sha256_after'])
 for n in delta:w.writerow([n,'removed' if n not in after_values else 'added' if n not in before_values else 'modified',sha(before_values[n]) if n in before_values else '',sha(after_values[n]) if n in after_values else ''])
with tempfile.TemporaryDirectory(prefix='patch-r21-',dir=r.parent.parent/'test') as d:
 t=Path(d);before=t/'before';after=t/'after';before.mkdir();after.mkdir()
 for base,values in [(before,before_values),(after,after_values)]:
  for n,b in values.items():
   if include(n):p=base/n;p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(b)
 x=subprocess.run(['git','diff','--no-index','--no-renames','--binary','--src-prefix=a/','--dst-prefix=b/','before','after'],cwd=t,capture_output=True);assert x.returncode==1
 lines=[]
 for line in x.stdout.splitlines(keepends=True):
  if line.startswith((b'diff --git ',b'--- ',b'+++ ')):
   for prefix in [b'a/before/',b'a/after/']:line=line.replace(prefix,b'a/')
   for prefix in [b'b/before/',b'b/after/']:line=line.replace(prefix,b'b/')
  lines.append(line)
 patch=b''.join(lines);path=audit/'R20_sources_to_R21_light.patch';path.write_bytes(patch)
 check=subprocess.run(['git','apply','--check',str(path)],cwd=before,capture_output=True)
 assert check.returncode==0,check.stderr.decode()
 subprocess.run(['git','apply',str(path)],cwd=before,capture_output=True,check=True)
 assert {f.relative_to(before).as_posix():f.read_bytes() for f in before.rglob('*') if f.is_file()}=={n:b for n,b in after_values.items() if include(n)}
(audit/'patch-evidence.json').write_text(json.dumps({'git_apply_check':'PASS','actual_apply_and_byte_comparison':'PASS','delta_files':len(delta),'patch_sha256':sha(patch),'R20_source_mapping':'progetto/ prefix; SDK/NuGet payload recorded separately; current audit and manifests excluded to avoid self-reference'},indent=2)+'\n')
current={f.relative_to(r).as_posix():f.read_bytes() for f in r.rglob('*') if f.is_file() and f.name!='SHA256SUMS_R10.txt'}
(r/'SHA256SUMS_R10.txt').write_text(''.join(sha(b)+'  '+n+'\n' for n,b in sorted(current.items())))
current['SHA256SUMS_R10.txt']=(r/'SHA256SUMS_R10.txt').read_bytes()
files={**light,**{'progetto/'+n:b for n,b in current.items()}}
assert not any(n.endswith(('.nupkg','.deb')) or '/setup/payload/' in n for n in files)
assert not any(n.startswith(('sdk/','runtime/','app/')) for n in files)
assert all(len(b)<10*1024*1024 for b in files.values())
manifest=''.join(sha(b)+'  '+n+'\n' for n,b in sorted(files.items()));files['SHA256SUMS.txt']=manifest.encode()
out=a.output_directory.resolve();out.mkdir(exist_ok=True,parents=True);target=out/'ProcessLens-R21-leggero.zip'
with zipfile.ZipFile(target,'w',zipfile.ZIP_DEFLATED,compresslevel=9) as z:
 for n,b in sorted(files.items()):z.writestr('ProcessLens-Leggero/'+n,b)
with zipfile.ZipFile(target) as z:
 assert z.testzip() is None
 for line in manifest.splitlines():
  h,n=line.split('  ',1);assert sha(z.read('ProcessLens-Leggero/'+n))==h
 assert {n.split('/')[1] for n in z.namelist() if '/' in n}=={'AVVIA_PROCESSLENS.cmd','LEGGIMI.txt','supporto','progetto','SHA256SUMS.txt'}
result={'zip':str(target),'bytes':target.stat().st_size,'sha256':sha(target.read_bytes()),'previous_zip_bytes':a.baseline_zip.stat().st_size,'reduction_percent':round(100*(1-target.stat().st_size/a.baseline_zip.stat().st_size),2),'manifest_entries':len(files)-1,'application_source':'unchanged R19 installed by R20','new_exe':'NOT INCLUDED; existing R20 installation or local publication required','PowerShell_fixtures':16,'source_preflight_stages':30,'Windows_launch_and_RAM':'NOT EXECUTED / NOT MEASURED'}
(out/'verification-r21.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result,indent=2))
