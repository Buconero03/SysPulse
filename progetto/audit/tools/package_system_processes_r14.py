"""Package an already source-checked R14. This does not compile or qualify Windows runtime."""
import argparse, csv, difflib, hashlib, json, shutil, subprocess, tempfile, zipfile
from pathlib import Path

def sha(data): return hashlib.sha256(data).hexdigest()
def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--candidate',type=Path,required=True);p.add_argument('--baseline-zip',type=Path,required=True)
    p.add_argument('--output-directory',type=Path,required=True);a=p.parse_args()
    root=a.candidate.resolve();audit=root/'audit/R10_R14';output=a.output_directory.resolve()
    assert sha(a.baseline_zip.read_bytes())=='c11ff4eb1b4809776d58171f4df62af5eebc21f8f07e918b3e5e780545f9b824'
    for d in sorted(root.rglob('__pycache__'),reverse=True):shutil.rmtree(d)
    with zipfile.ZipFile(a.baseline_zip) as z:
        assert z.testzip() is None
        old={n.split('/',1)[1]:z.read(n) for n in z.namelist() if not n.endswith('/')}
    assert all((root/n).is_file() for n in old)
    for row in json.loads((audit/'authorized-source-edits.json').read_text())['files']:
        assert sha((root/row['file']).read_bytes())==row['sha256_after'],row['file']
    results=[]
    for n,count in [('source-results.json',33),('resource-source-mutations.json',14),('monitor-navigation-source-mutations.json',13)]:
        rows=json.loads((audit/n).read_text());assert len(rows)==count and all(row['status']=='PASS' for row in rows)
        results.extend(rows)
    # Preserve all added/changed source contexts in the current string ledger.
    with (audit/'string-ledger.csv').open('w',newline='') as f:
        writer=csv.writer(f);writer.writerow(['file','line','added_or_modified_source_text','context'])
        for row in json.loads((audit/'authorized-source-edits.json').read_text())['files']:
            n=row['file'];before=old.get(n,b'').decode('utf-8-sig').splitlines();after=(root/n).read_text(encoding='utf-8-sig').splitlines()
            for tag,i,j,k,l in difflib.SequenceMatcher(None,before,after).get_opcodes():
                if tag not in ('insert','replace'):continue
                for number,line in enumerate(after[k:l],k+1):
                    if '"' in line:writer.writerow([n,number,line.strip(),'UI/report or technical source context'])
    def included(n):return not n.startswith('audit/R10_R14/') and n!='SHA256SUMS_R10.txt'
    current={f.relative_to(root).as_posix():f.read_bytes() for f in root.rglob('*') if f.is_file()}
    delta=[n for n,data in sorted(current.items()) if included(n) and old.get(n)!=data]
    with (audit/'files-changed.csv').open('w',newline='') as f:
        writer=csv.writer(f);writer.writerow(['file','kind','sha256_before','sha256_after'])
        for n in delta:writer.writerow([n,'modified' if n in old else 'added',sha(old[n]) if n in old else '',sha(current[n])])
    staging=root.parent.parent/'test';staging.mkdir(exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='patch-replay-r14-',dir=staging) as temporary:
        temp=Path(temporary);before=temp/'before';after=temp/'after';before.mkdir();after.mkdir()
        for base,values in [(before,old),(after,current)]:
            for n,data in values.items():
                if not included(n):continue
                path=base/n;path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(data)
        diff=subprocess.run(['git','diff','--no-index','--binary','--src-prefix=a/','--dst-prefix=b/','before','after'],cwd=temp,capture_output=True)
        assert diff.returncode==1,diff.stderr.decode()
        lines=[]
        for line in diff.stdout.splitlines(keepends=True):
            if line.startswith((b'diff --git ', b'--- ', b'+++ ')):
                for prefix in (b'a/before/',b'a/after/'):line=line.replace(prefix,b'a/')
                for prefix in (b'b/before/',b'b/after/'):line=line.replace(prefix,b'b/')
            lines.append(line)
        patch=b''.join(lines)
        path=audit/'R13_to_R14.patch';path.write_bytes(patch)
        subprocess.run(['git','apply','--check',str(path)],cwd=before,check=True,capture_output=True)
        subprocess.run(['git','apply',str(path)],cwd=before,check=True,capture_output=True)
        mismatch=[n for n,data in current.items() if included(n) and (before/n).read_bytes()!=data]
        assert not mismatch,mismatch
    (audit/'patch-replay-results.json').write_text(json.dumps({'git_apply_check':'PASS','actual_apply_and_byte_comparison':'PASS','delta_files':len(delta),'patch_sha256':sha(patch),'excluded':'Current audit metadata and current manifest only; excluded for self-reference.'},indent=2)+'\n')
    (audit/'retention.json').write_text(json.dumps({'source_trees':1,'patch_replay_temporary_tree_removed':True,'baseline':'R13 retained for byte comparisons','no_bin_obj_pycache_in_release':True,'source_check_count':len(results),'Windows_runtime_and_RAM_measurement':'NOT EXECUTED'},indent=2)+'\n')
    matrix=audit/'test-matrix.csv'
    with matrix.open(newline='') as f:
        matrix_rows=[row for row in csv.reader(f) if row and row[0] not in
            {'Current R14 + resource + navigation guards','R13 to R14 patch replay'}]
    with matrix.open('w',newline='') as f:
        writer=csv.writer(f);writer.writerows(matrix_rows)
        writer.writerow(['Current R14 + resource + navigation guards','Python source/XML/mutations','PASS',f'{len(results)} checks; no C# execution'])
        writer.writerow(['R13 to R14 patch replay','git apply and byte comparison','PASS',f'{len(delta)} changed or added files; audit metadata and manifest excluded'])
    for d in root.rglob('*'):
        assert not (d.is_dir() and d.name in ('__pycache__','bin','obj')),d
    files=sorted(f for f in root.rglob('*') if f.is_file() and f.name!='SHA256SUMS_R10.txt')
    (root/'SHA256SUMS_R10.txt').write_text(''.join(sha(f.read_bytes())+'  '+f.relative_to(root).as_posix()+'\n' for f in files))
    output.mkdir(parents=True,exist_ok=True)
    target=output/'ProcessLens-v0.19.16-R10-R14-system-processes-candidate.zip'
    with zipfile.ZipFile(target,'w',zipfile.ZIP_DEFLATED,compresslevel=9) as z:
        for f in sorted(root.rglob('*')):
            if f.is_file():z.write(f,root.name+'/'+f.relative_to(root).as_posix())
    with zipfile.ZipFile(target) as z:
        assert z.testzip() is None
        for line in z.read(root.name+'/SHA256SUMS_R10.txt').decode().splitlines():
            expected,n=line.split('  ',1);assert sha(z.read(root.name+'/'+n))==expected,n
        assert len({n.split('/')[0] for n in z.namelist()})==1
    result={'zip':str(target),'bytes':target.stat().st_size,'sha256':sha(target.read_bytes()),'manifest_entries':len(files),'delta_files':len(delta),'source_checks':len(results),'Windows_runtime':'NOT EXECUTED'}
    (output/'verification-r14.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result,indent=2))
if __name__=='__main__':main()
