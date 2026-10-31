"""R14 R1 source checks + correlation with retained real .NET test logs. Not a Windows build."""
import argparse,hashlib,json,zipfile
from pathlib import Path
import xml.etree.ElementTree as ET
from network_device_rules import violations as network_violations
from ux_writing_rules import approved_feature

def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--candidate',type=Path,required=True);p.add_argument('--baseline-zip',type=Path,required=True)
    p.add_argument('--output',type=Path,required=True);a=p.parse_args();root=a.candidate.resolve()
    sha=lambda data:hashlib.sha256(data).hexdigest()
    assert sha(a.baseline_zip.read_bytes())=='4ea232b848acc675ab3c39a907460e0ab147b90fa42537c6c448a1bc12525955'
    with zipfile.ZipFile(a.baseline_zip) as z:old={n.split('/',1)[1]:z.read(n) for n in z.namelist() if not n.endswith('/')}
    files={f.relative_to(root).as_posix():f.read_text(encoding='utf-8-sig') for f in root.rglob('*') if f.suffix in {'.cs','.xaml','.ps1'}}
    results=[]
    def check(label,ok):
        results.append({'test':label,'status':'PASS' if ok else 'FAIL','kind':'source/retained runtime evidence'})
        print(results[-1]['status'],label)
    core='src/ProcessLens.Core/NetworkDevices.cs';app='src/ProcessLens.App/ProcessLens.App.csproj';test='tests/ProcessLens.NetworkDeviceTests/Program.cs'
    before='ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException'
    after='ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException'
    check('production catalog delta is exactly one explicit exception alternative',
        old[core].count(before.encode())==1 and (root/core).read_bytes()==old[core].replace(before.encode(),after.encode(),1))
    check('App project delta changes only candidate identity',(root/app).read_bytes()==old[app].replace(b'R14-system-processes-candidate',b'R14-R1-device-catalog-fix-candidate') or any(approved_feature(root,app,rev) for rev in ('R10_R14_R2','R10_R14_R3','R10_R15','R10_R16','R10_R17','R10_R18','R10_R18_R1','R10_R18_R2','R10_R19')))
    changed={n for n,data in old.items() if n.startswith('src/') and (root/n).read_bytes()!=data}
    check('catalog fix retained with only exact authorized R14 R2/R3 and R15 UI delta',changed - {n for n in changed if any(approved_feature(root,n,rev) for rev in ('R10_R14_R2','R10_R14_R3','R10_R15','R10_R16','R10_R17','R10_R18','R10_R18_R1','R10_R18_R2','R10_R19'))} == {core,app} - {n for n in (core,app) if any(approved_feature(root,n,rev) for rev in ('R10_R14_R2','R10_R14_R3','R10_R15','R10_R16','R10_R17','R10_R18','R10_R18_R1','R10_R18_R2','R10_R19'))})
    check('R14 sources and prior C# tests retained outside exact authorized R14 R2/R3 and R15 delta',all((root/n).read_bytes()==data or any(approved_feature(root,n,rev) for rev in ('R10_R14_R2','R10_R14_R3','R10_R15','R10_R16','R10_R17','R10_R18','R10_R18_R1','R10_R18_R2','R10_R19')) for n,data in old.items() if (n.startswith('src/') and n not in {core,app}) or (n.startswith('tests/') and n.endswith('.cs') and n!=test)))
    check('current network guards pass',not network_violations(files))
    t=files[test]
    check('real file tests retain oversized schema null duplicate boundary and overwrite checks',all(s in t for s in ['MaximumBytes + 1','null device list','null device entry','duplicate identities','too many devices','empty JSON','exact byte limit','File.ReadAllBytes(path).SequenceEqual(bytes)','store.Save(known) == loaded.Error','it-IT','en-US']))
    ledger=json.loads((root/'audit/R10_R14_R1/authorized-source-edits.json').read_text())['files']
    check('catalog fix hashes exact or exact authorized R14 R2/R3 or R15 version update',len(ledger)==2 and all(sha((root/r['file']).read_bytes())==r['sha256_after'] or any(approved_feature(root,r['file'],rev) for rev in ('R10_R14_R2','R10_R14_R3','R10_R15','R10_R16','R10_R17','R10_R18','R10_R18_R1','R10_R18_R2','R10_R19')) for r in ledger))
    evidence=json.loads((root/'audit/R10_R14_R1/runtime-evidence.json').read_text())
    check('retained compiled source hashes match current source',all(sha((root/n).read_bytes())==digest for n,digest in evidence['source_hashes'].items()))
    d=root/'audit/R10_R14_R1'
    network=(d/'network-tests-run.log').read_text();system=(d/'system-tests-run.log').read_text();baseline=(d/'baseline-repro.log').read_text()
    check('81 actual network assertions retained',sum(s.startswith('PASS ') for s in network.splitlines())==81 and 'Network device regressions complete' in network)
    check('22 portable system assertions retained with Windows branch skipped',sum(s.startswith('PASS ') for s in system.splitlines())==22 and 'SKIP live Windows read' in system)
    check('same suite reproduces original unhandled exception on R14 Core','InvalidDataException' in baseline and 'File troppo grande.' in baseline and 'Network device regressions complete' not in baseline)
    for f in [*root.rglob('*.xaml'),*root.rglob('*.csproj')]:ET.parse(f)
    check('XAML and project XML parse',True)
    for label,before,after in [('missing semantic error handling',after,'ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException'),
        ('corrupt file overwrite','if (!existing.Available) return existing.Error;','if (false) return existing.Error;'),
        ('byte limit removed','MaximumBytes = 524288','MaximumBytes = int.MaxValue')]:
        changed=dict(files);assert before in changed[core];changed[core]=changed[core].replace(before,after)
        check('source guard rejects '+label,bool(network_violations(changed)))
    a.output.parent.mkdir(exist_ok=True,parents=True);a.output.write_text(json.dumps(results,indent=2)+'\n')
    raise SystemExit(0 if all(r['status']=='PASS' for r in results) else 1)
if __name__=='__main__':main()
