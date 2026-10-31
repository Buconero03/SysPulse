"""Package original R19 sources plus offline Windows SDK/NuGet payload and setup. No Windows install claimed."""
import argparse,csv,hashlib,json,shutil,subprocess,tempfile,zipfile
from pathlib import Path
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--candidate',type=Path,required=True);p.add_argument('--payload',type=Path,required=True);p.add_argument('--baseline-zip',type=Path,required=True);p.add_argument('--output-directory',type=Path,required=True);a=p.parse_args()
r=a.candidate.resolve();audit=r/'audit/R10_R20';payload=a.payload.resolve();out=a.output_directory.resolve();sha=lambda b:hashlib.sha256(b).hexdigest()
assert sha(a.baseline_zip.read_bytes())=='f01f5434192e34e7ce30d5496d3b777863ffa53ba49310c71cb6e26bbc318598'
with zipfile.ZipFile(a.baseline_zip) as z:old={n.split('/',1)[1]:z.read(n) for n in z.namelist() if not n.endswith('/')}
assert all((r/n).is_file() for n in old)
assert all((r/n).read_bytes()==b for n,b in old.items() if n.startswith(('src/','tests/')))
assert (r/'build-dashboard.ps1').read_bytes()==old['build-dashboard.ps1']
proof=json.loads((audit/'powershell-evidence.json').read_text());assert len(proof['stages'])==30 and all(x['status']=='PASS' and sha((r/x['file']).read_bytes())==x['sha256'] for x in proof['stages'])
assert proof['fixture_checks']==19 and proof['script_syntax_count']==74
assert (audit/'installer-fixture.log').read_text().count('PASS bundle:')==19
assert (audit/'native-diagnostic-typecheck.log').read_text()==''
downloads=json.loads((payload/'downloads.json').read_text());assert len(downloads['files'])==65
for row in downloads['files']:assert sha((payload/row['file']).read_bytes())==row['sha256']
sdk=next(x for x in downloads['files'] if x['role'].startswith('Windows'))
assert hashlib.sha512((payload/sdk['file']).read_bytes()).hexdigest()==sdk['sha512']
assert json.loads((audit/'dependency-closure.json').read_text())['status']=='PASS'
for d in sorted(r.rglob('__pycache__'),reverse=True):shutil.rmtree(d)
source_names=['global.json','INSTALLA_PROCESSLENS.cmd','AVVIA_PROCESSLENS.cmd','DIAGNOSTICA_RETE.cmd',*sorted(p.relative_to(r).as_posix() for p in (r/'setup').glob('*.ps1'))]
(audit/'authorized-source-edits.json').write_text(json.dumps({'baseline_sha256':sha(a.baseline_zip.read_bytes()),'scope':'Offline setup wrapper only. Every existing production and test source unchanged.','files':[{'file':n,'sha256_before':None,'sha256_after':sha((r/n).read_bytes())} for n in source_names]},indent=2)+'\n')
with (audit/'string-ledger.csv').open('w',newline='') as f:
 writer=csv.writer(f);writer.writerow(['file','line','source_text','context'])
 for n in source_names:
  for line,s in enumerate((r/n).read_text().splitlines(),1):
   if '"' in s or "'" in s:writer.writerow([n,line,s.strip(),'Offline installer / local diagnostic; no application UI changes'])
current={f.relative_to(r).as_posix():f for f in r.rglob('*') if f.is_file() and f.name!='SHA256SUMS_R10.txt'}
include=lambda n:not n.startswith('audit/R10_R20/') and n!='setup/bundle-manifest.json'
delta=[n for n,f in sorted(current.items()) if include(n) and old.get(n)!=f.read_bytes()]
with (audit/'files-changed.csv').open('w',newline='') as f:
 writer=csv.writer(f);writer.writerow(['file','kind','sha256_before','sha256_after'])
 for n in delta:writer.writerow([n,'modified' if n in old else 'added',sha(old[n]) if n in old else '',sha(current[n].read_bytes())])
with tempfile.TemporaryDirectory(prefix='patch-replay-r20-',dir=r.parent.parent/'test') as d:
 t=Path(d);before=t/'before';after=t/'after';before.mkdir();after.mkdir()
 for n,b in old.items():
  if include(n) and n!='SHA256SUMS_R10.txt':p=before/n;p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(b)
 for n,f in current.items():
  if include(n):p=after/n;p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(f.read_bytes())
 diff=subprocess.run(['git','diff','--no-index','--binary','--src-prefix=a/','--dst-prefix=b/','before','after'],cwd=t,capture_output=True);assert diff.returncode==1
 lines=[]
 for line in diff.stdout.splitlines(keepends=True):
  if line.startswith((b'diff --git ',b'--- ',b'+++ ')):
   for prefix in [b'a/before/',b'a/after/']:line=line.replace(prefix,b'a/')
   for prefix in [b'b/before/',b'b/after/']:line=line.replace(prefix,b'b/')
  lines.append(line)
 patch=b''.join(lines);path=audit/'R19_to_R20_setup.patch';path.write_bytes(patch)
 subprocess.run(['git','apply','--check',str(path)],cwd=before,check=True,capture_output=True);subprocess.run(['git','apply',str(path)],cwd=before,check=True,capture_output=True)
 assert all((before/n).read_bytes()==f.read_bytes() for n,f in current.items() if include(n))
(audit/'patch-replay-results.json').write_text(json.dumps({'git_apply_check':'PASS','actual_apply_and_byte_comparison':'PASS','source_delta_files':len(delta),'patch_sha256':sha(patch),'exclusions':'Current audit metadata, manifest and original third-party payload. Payload exact bytes and download URLs are separately recorded in downloads.json.'},indent=2)+'\n')
(audit/'test-matrix.csv').write_text('check,environment,status,scope\nPowerShell source pipeline,PowerShell 7.4.21 Linux,PASS,30 stages and syntax of 74 scripts\nInstaller integrity fixtures,PowerShell 7.4.21 Linux,PASS,19 checks; no install\nNative diagnostic CSharp,Roslyn SDK 8.0.426 Linux,PASS,Type check only; no native call\nDependency closure,NuGet.Frameworks and package manifests,PASS,64 packages for net8.0 and Windows target\nSDK payload,SHA512 official Microsoft metadata,PASS,Original archive unchanged\nFull Windows restore install build and WPF,Windows,NOT EXECUTED,Performed by installer on destination\nLive WLAN Security permissions and capture,Windows,NOT EXECUTED,Read-only diagnostic supplied\n')
(audit/'retention.json').write_text(json.dumps({'source_trees':1,'current_release_zips':1,'previous_R19_baseline_retained':True,'SDK_archive_in_payload_only':True,'no_extracted_Windows_SDK_in_source':True,'App_Guardian_MemoryAgent_profiler_native_collectors':'BYTE IDENTICAL R19','Windows_installation':'NOT EXECUTED'},indent=2)+'\n')
current={f.relative_to(r).as_posix():f for f in r.rglob('*') if f.is_file() and f.name!='SHA256SUMS_R10.txt' and f.relative_to(r).as_posix()!='setup/bundle-manifest.json'}
payload_files=[row['file'] for row in downloads['files']]+['downloads.json']
assert len(payload_files)==66 and len(set(payload_files))==66
for n in payload_files:
 current['setup/payload/'+n]=payload/n
assert not any(n.endswith('.part') for n in current)
manifest={'schema':1,'bundle':'ProcessLens-R20-win-x64-offline','sdk_sha512':sdk['sha512'],'files':[{'file':n,'sha256':sha(f.read_bytes()),'bytes':f.stat().st_size} for n,f in sorted(current.items())]}
(r/'setup/bundle-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n');current['setup/bundle-manifest.json']=r/'setup/bundle-manifest.json'
(r/'SHA256SUMS_R10.txt').write_text(''.join(sha(f.read_bytes())+'  '+n+'\n' for n,f in sorted(current.items())))
current['SHA256SUMS_R10.txt']=r/'SHA256SUMS_R10.txt'
out.mkdir(exist_ok=True,parents=True);target=out/'ProcessLens-v0.19.16-R10-R20-offline-install-win-x64.zip'
with zipfile.ZipFile(target,'w',allowZip64=True) as z:
 for n,f in sorted(current.items()):
  assert not any(part in {'bin','obj','__pycache__'} for part in Path(n).parts)
  z.write(f,r.name+'/'+n,compress_type=zipfile.ZIP_STORED if n.startswith('setup/payload/') else zipfile.ZIP_DEFLATED,compresslevel=None if n.startswith('setup/payload/') else 9)
with zipfile.ZipFile(target) as z:
 assert z.testzip() is None
 for n,f in current.items():assert sha(z.read(r.name+'/'+n))==sha(f.read_bytes()),n
 assert len({n.split('/')[0] for n in z.namelist()})==1
result={'file':str(target),'bytes':target.stat().st_size,'sha256':sha(target.read_bytes()),'manifest_entries':len(current)-1,'third_party_archives':65,'NuGet_packages':64,'source_delta_files':len(delta),'Windows_installation_and_WPF':'NOT EXECUTED','application_source_version':'R19','bundle_version':'R20'}
(out/'verification-r20.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result,indent=2))
