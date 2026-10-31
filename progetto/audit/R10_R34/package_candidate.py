"""Verify the narrow R34 delta and package through the checked atomic ZIP writer.

python package_candidate.py SHELL_DIRECTORY R33_R1_ZIP OUTPUT_ZIP
The output must be outside the input shell directory.
"""
from pathlib import Path
import csv
import difflib
import hashlib
import importlib.util
import json
import subprocess
import sys
import tempfile
import zipfile
import xml.etree.ElementTree as ET

sha=lambda b:hashlib.sha256(b).hexdigest()
shell,baseline,output=map(lambda s:Path(s).resolve(),sys.argv[1:])
assert not output.is_relative_to(shell)
r=shell/'progetto';audit=r/'audit/R10_R34'
assert sha(baseline.read_bytes())=='eefa97864d7b0dc22d22a3aec08707a4bf7a968004fb4cc25b9ef1f11843a090'
with zipfile.ZipFile(baseline) as z:
    assert z.testzip() is None
    before={n.split('/',1)[1]:z.read(n) for n in z.namelist()}
for directory in shell.rglob('__pycache__'):
    import shutil
    shutil.rmtree(directory)
after={p.relative_to(shell).as_posix():p.read_bytes() for p in shell.rglob('*') if p.is_file()}
modified={'LEGGIMI.txt','supporto/avvia.ps1','progetto/build-dashboard.ps1','progetto/docs/terminology/GLOSSARIO_TERMINOLOGICO_PROCESSLENS.csv',
 'progetto/src/ProcessLens.App/ProcessLens.App.csproj','progetto/src/ProcessLens.Core/NativeNetworkEvidence.cs','progetto/src/ProcessLens.Core/NetworkEvidenceStatus.cs',
 'progetto/src/ProcessLens.Windows/WindowsNativeNetworkEvidence.cs','progetto/tests/ProcessLens.NativeNetworkTests/Program.cs','progetto/tests/ProcessLens.NetworkUiTests/Program.cs'}
modified|={f'progetto/src/ProcessLens.App/{n}{suffix}' for n in ['NetworkDevicesView','NativeNetworkEvidenceView','NativeCaptureView'] for suffix in ['.xaml','.xaml.cs']}
added={'progetto/src/ProcessLens.Core/NetworkReadPresentation.cs','progetto/tests/ProcessLens.NativeNetworkTests/R34NetworkReadChecks.cs'}
added|={f'progetto/audit/tools/{n}' for n in ['check_network_clarity_r34.ps1','test_launcher_r34.ps1','typecheck_wpf_sources_r34.py','check_powershell_r34.py']}
manifests={'SHA256SUMS.txt','progetto/SHA256SUMS_R10.txt'}
assert set(before)<=set(after),'Baseline file removed'
assert {n for n in before if n not in manifests and before[n]!=after[n]}==modified,'Unexpected source delta'
assert {n for n in after if n not in before and not n.startswith('progetto/audit/R10_R34/')}==added,'Unexpected addition'
for name,data in after.items():
    assert not any(part in {'bin','obj','.git','__pycache__'} for part in Path(name).parts),name
    assert not name.endswith(('.dll','.exe','.pdb','.nupkg','.pyc','.ok')),name
    if name.endswith(('.xaml','.csproj')):ET.fromstring(data)
for record in ['runtime-evidence.json','wpf-typecheck-evidence.json']:
    evidence=json.loads((audit/record).read_text())
    for name,h in evidence['inputs'].items():assert sha((r/name).read_bytes())==h,('stale evidence',record,name)
runtime=json.loads((audit/'runtime-evidence.json').read_text())
assert runtime['assertions']==341 and len(runtime['projects'])==5
for project in runtime['projects']:
    assert project['exit_code']==0
    assert sum(l.startswith('PASS') for l in (audit/(project['project']+'-run.log')).read_text().splitlines())==project['assertions']
typing=json.loads((audit/'wpf-typecheck-evidence.json').read_text())
assert len(typing['assemblies'])==9 and all(row['status']=='PASS' for row in typing['assemblies'])
assert 'System.IO' not in typing['evaluated_wpf_implicit_usings']
ps=json.loads((audit/'powershell-evidence.json').read_text())
assert len(ps['stages'])==44 and ps['launcher_fixture_checks']==17
for row in ps['stages']:assert sha((r/row['file']).read_bytes())==row['sha256']
assert sha((shell/'supporto/avvia.ps1').read_bytes())==ps['launcher_sha256']
items=json.loads((audit/'msbuild-wpf-usings.json').read_text())['Items']
assert not items['Content']
assert {row['LogicalName'] for row in items['EmbeddedResource']}=={'ProcessLens.AppIcon','ProcessLens.ProcessSymbols'}
assert all(before[name]==after[name] for name in before if name.startswith('progetto/src/') and name not in modified)
(audit/'scope-evidence.json').write_text(json.dumps({'baseline_sha256':sha(baseline.read_bytes()),'modified':sorted(modified),'added_outside_current_audit':sorted(added),
 'protected':'All other application sources byte-identical: main window/tray, icons, scrolling, process grouping, acquisition, CPU/RAM, Guardian and external dependencies.',
 'new_dependencies':0,'Windows_WPF_DPI_RAM_live_acquisition':'NOT EXECUTED / NOT MEASURED'},indent=2)+'\n')
with (audit/'files-changed.csv').open('w',newline='') as f:
    w=csv.writer(f);w.writerow(['file','kind','sha256_before','sha256_after'])
    for name in sorted(modified|added):w.writerow([name,'modified' if name in before else 'added',sha(before[name]) if name in before else '',sha(after[name])])
with (audit/'string-ledger.csv').open('w',newline='') as f:
    w=csv.writer(f);w.writerow(['file','side','changed_line'])
    for name in sorted(modified|added):
        for line in difflib.unified_diff(before.get(name,b'').decode('utf-8-sig').splitlines(),after[name].decode('utf-8-sig').splitlines(),n=0):
            if line.startswith(('+','-')) and not line.startswith(('+++','---')):w.writerow([name,'after' if line[0]=='+' else 'before',line[1:]])
include=lambda n:n not in manifests and not n.startswith('progetto/audit/R10_R34/')
with tempfile.TemporaryDirectory(prefix='processlens-r34-diff-') as temporary:
    t=Path(temporary);b=t/'before';a=t/'after';b.mkdir();a.mkdir()
    for directory,data in [(b,before),(a,after)]:
        for name,value in data.items():
            if include(name):p=directory/name;p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(value)
    diff=subprocess.run(['git','diff','--no-index','--no-renames','--binary','--src-prefix=a/','--dst-prefix=b/','before','after'],cwd=t,capture_output=True)
    assert diff.returncode==1
    lines=[]
    for line in diff.stdout.splitlines(keepends=True):
        if line.startswith((b'diff --git ',b'--- ',b'+++ ')):
            for p in [b'a/before/',b'a/after/']:line=line.replace(p,b'a/')
            for p in [b'b/before/',b'b/after/']:line=line.replace(p,b'b/')
        lines.append(line)
    patch=audit/'R33_R1_to_R34.patch';patch.write_bytes(b''.join(lines))
    subprocess.run(['git','apply','--check',str(patch)],cwd=b,check=True)
    subprocess.run(['git','apply',str(patch)],cwd=b,check=True)
    assert {f.relative_to(b).as_posix():f.read_bytes() for f in b.rglob('*') if f.is_file()}=={n:v for n,v in after.items() if include(n)}
    (audit/'patch-evidence.json').write_text(json.dumps({'git_apply_check':'PASS','actual_apply_and_byte_comparison':'PASS','patch_sha256':sha(patch.read_bytes())},indent=2)+'\n')
sys.dont_write_bytecode=True
writer=r/'audit/R10_R33_R1/package_checked_zip.py'
spec=importlib.util.spec_from_file_location('checked_zip',writer);module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
sys.argv=[str(writer),str(shell),str(output)];module.main()
with tempfile.TemporaryDirectory(prefix='processlens-r34-unzip-') as temporary:
    test=subprocess.run(['unzip','-t',str(output)],capture_output=True,text=True);assert test.returncode==0,test.stdout+test.stderr
    subprocess.run(['unzip','-q',str(output),'-d',temporary],check=True)
    actual={f.relative_to(Path(temporary)/shell.name).as_posix():f.read_bytes() for f in (Path(temporary)/shell.name).rglob('*') if f.is_file()}
    expected={f.relative_to(shell).as_posix():f.read_bytes() for f in shell.rglob('*') if f.is_file() and '__pycache__' not in f.parts}
    assert actual==expected
result={'zip':str(output),'bytes':output.stat().st_size,'sha256':sha(output.read_bytes()),'files':len(actual),'independent_unzip_and_byte_comparison':'PASS',
 'current_assertions':341,'PowerShell_stages':44,'launcher_checks':17,'WindowsDesktop_typechecked_assemblies':9,'Windows_WPF_runtime':'NOT EXECUTED'}
(output.parent/'verification-r34.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result,indent=2))
