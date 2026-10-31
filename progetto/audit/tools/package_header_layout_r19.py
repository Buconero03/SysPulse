"""Full R19 candidate with exact baseline, replayable patch and SHA256 manifest. No Windows qualification implied."""
import argparse,csv,difflib,hashlib,json,shutil,subprocess,tempfile,zipfile
from pathlib import Path
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--candidate',type=Path,required=True);p.add_argument('--baseline-zip',type=Path,required=True);p.add_argument('--output-directory',type=Path,required=True);a=p.parse_args()
r=a.candidate.resolve();audit=r/'audit/R10_R19';output=a.output_directory.resolve();sha=lambda b:hashlib.sha256(b).hexdigest()
assert sha(a.baseline_zip.read_bytes())=='1e86eb08d6e4b87649a65ee69cbb1786c82933dc8ea95c7c6d9385b0d1cedee2'
for d in sorted(r.rglob('__pycache__'),reverse=True):shutil.rmtree(d)
with zipfile.ZipFile(a.baseline_zip) as z:
 assert z.testzip() is None
 old={n.split('/',1)[1]:z.read(n) for n in z.namelist() if not n.endswith('/')}
assert all((r/n).is_file() for n in old)
ledger=json.loads((audit/'authorized-source-edits.json').read_text())['files'];allowed={row['file'] for row in ledger}
assert len(allowed)==7 and all(sha((r/x['file']).read_bytes())==x['sha256_after'] and sha(old[x['file']])==x['sha256_before'] for x in ledger)
current={f.relative_to(r).as_posix():f.read_bytes() for f in r.rglob('*') if f.is_file()}
assert {n for n,b in current.items() if n.startswith(('src/','tests/')) and old.get(n)!=b}==allowed
results=[]
for name,count in [('source-results.json',51),('layout-retention-results.json',25),('recovery-retention-results.json',47),('capture-retention-results.json',39),('native-retention-results.json',31),('component-retention-results.json',24),('context-retention-results.json',26),('tab-retention-results.json',20),('catalog-retention-results.json',15),('system-retention-results.json',33)]:
 rows=json.loads((audit/name).read_text());assert len(rows)==count and all(x['status']=='PASS' for x in rows),name;results.extend(rows)
e=json.loads((audit/'presentation-evidence.json').read_text());assert e['status']=='PASS' and e['assertions']==86 and e['R19_assertions']==18 and all(sha((r/n).read_bytes())==h for n,h in e['source_hashes'].items())
e=json.loads((audit/'wpf-typecheck-evidence.json').read_text());assert len(e['assemblies'])==6 and all(x['status']=='PASS' and x['warning_count']==0 for x in e['assemblies']) and all(sha((r/n).read_bytes())==h for n,h in e['inputs'].items())
e=json.loads((audit/'network-runtime-retention.json').read_text());assert sum(x['assertions'] for x in e['outcomes'])==332 and all(sha((r/n).read_bytes())==h for n,h in e['source_hashes'].items())
e=json.loads((audit/'preflight-evidence.json').read_text());assert len(e['hashes'])==30 and all(sha((r/n).read_bytes())==h for n,h in e['hashes'].items())
assert 'PASS Sintassi PowerShell: 69 script verificati.' in (audit/'powershell-preflight.log').read_text()
with (audit/'string-ledger.csv').open('w',newline='') as f:
 writer=csv.writer(f);writer.writerow(['file','line','added_or_modified_text','context'])
 for n in sorted(allowed):
  before=old[n].decode('utf-8-sig').splitlines();after=(r/n).read_text(encoding='utf-8-sig').splitlines()
  for tag,i,j,k,l in difflib.SequenceMatcher(None,before,after).get_opcodes():
   if tag not in ('insert','replace'):continue
   for number,line in enumerate(after[k:l],k+1):
    if '"' in line:writer.writerow([n,number,line.strip(),'RAM header, layout, candidate identity or corresponding regression'])
def included(n):return n!='SHA256SUMS_R10.txt' and not n.startswith('audit/R10_R19/')
current={f.relative_to(r).as_posix():f.read_bytes() for f in r.rglob('*') if f.is_file()}
delta=[n for n,b in sorted(current.items()) if included(n) and old.get(n)!=b]
with (audit/'files-changed.csv').open('w',newline='') as f:
 writer=csv.writer(f);writer.writerow(['file','kind','sha256_before','sha256_after'])
 for n in delta:writer.writerow([n,'modified' if n in old else 'added',sha(old[n]) if n in old else '',sha(current[n])])
with tempfile.TemporaryDirectory(prefix='patch-replay-r19-',dir=r.parent.parent/'test') as d:
 temp=Path(d);before=temp/'before';after=temp/'after';before.mkdir();after.mkdir()
 for root,values in [(before,old),(after,current)]:
  for n,b in values.items():
   if not included(n):continue
   path=root/n;path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(b)
 diff=subprocess.run(['git','diff','--no-index','--binary','--src-prefix=a/','--dst-prefix=b/','before','after'],cwd=temp,capture_output=True);assert diff.returncode==1,diff.stderr
 lines=[]
 for line in diff.stdout.splitlines(keepends=True):
  if line.startswith((b'diff --git ',b'--- ',b'+++ ')):
   for prefix in [b'a/before/',b'a/after/']:line=line.replace(prefix,b'a/')
   for prefix in [b'b/before/',b'b/after/']:line=line.replace(prefix,b'b/')
  lines.append(line)
 patch=b''.join(lines);path=audit/'R18_R2_to_R19.patch';path.write_bytes(patch)
 subprocess.run(['git','apply','--check',str(path)],cwd=before,check=True,capture_output=True)
 subprocess.run(['git','apply',str(path)],cwd=before,check=True,capture_output=True)
 assert all((before/n).read_bytes()==b for n,b in current.items() if included(n))
(audit/'patch-replay-results.json').write_text(json.dumps({'git_apply_check':'PASS','actual_apply_and_byte_comparison':'PASS','delta_files':len(delta),'patch_sha256':sha(patch),'excluded':'Current audit metadata and manifest only, to avoid self-reference'},indent=2)+'\n')
(audit/'retention.json').write_text(json.dumps({'source_trees':1,'current_release_zips':1,'patch_temporary_tree_removed':True,'baseline':'R18 R2 retained for exact byte comparisons','source_and_preflight_checks':len(results),'fresh_presentation_assertions':86,'fresh_typechecked_assemblies':6,'retained_network_assertions':332,'network_rerun':False,'Windows_build_WPF_DPI_live_RAM':'NOT EXECUTED'},indent=2)+'\n')
for f in r.rglob('*'):assert not(f.is_dir() and f.name in {'__pycache__','bin','obj'}),f
files=sorted(f for f in r.rglob('*') if f.is_file() and f.name!='SHA256SUMS_R10.txt')
(r/'SHA256SUMS_R10.txt').write_text(''.join(sha(f.read_bytes())+'  '+f.relative_to(r).as_posix()+'\n' for f in files))
output.mkdir(parents=True,exist_ok=True);target=output/'ProcessLens-v0.19.16-R10-R19-system-layout-ram-candidate.zip'
with zipfile.ZipFile(target,'w',zipfile.ZIP_DEFLATED,compresslevel=9) as z:
 for f in sorted(r.rglob('*')):
  if f.is_file():
   assert f.stat().st_size<10*1024*1024,f
   z.write(f,r.name+'/'+f.relative_to(r).as_posix())
with zipfile.ZipFile(target) as z:
 assert z.testzip() is None
 for line in z.read(r.name+'/SHA256SUMS_R10.txt').decode().splitlines():
  h,n=line.split('  ',1);assert sha(z.read(r.name+'/'+n))==h,n
 assert len({n.split('/')[0] for n in z.namelist()})==1
result={'zip':str(target),'bytes':target.stat().st_size,'sha256':sha(target.read_bytes()),'manifest_entries':len(files),'delta_files':len(delta),'source_and_preflight_checks':len(results),'fresh_presentation_assertions':86,'fresh_typechecked_assemblies':6,'retained_network_assertions':332,'network_rerun':False,'Windows_WPF_DPI_and_live_RAM':'NOT EXECUTED'}
(output/'verification-r19.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result,indent=2))
