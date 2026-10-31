"""R14 source/XML and source-mutation checks. Never compiles or executes C#/WPF/Windows."""
import argparse, hashlib, json, re, zipfile
from pathlib import Path
import xml.etree.ElementTree as ET
from test_clipboard_calls_r10r9 import invalid_calls
from button_layout_rules import violations as button_violations
from check_packet_capture_r13 import violations as packet_violations
from network_device_rules import violations as network_violations
from ux_writing_rules import approved_feature

def violations(files):
    c=files['src/ProcessLens.Windows/WindowsSystemProcessSource.cs']
    p=files['src/ProcessLens.Core/SystemProcessCatalog.cs']
    u=files['src/ProcessLens.App/SystemProcessesView.xaml.cs']
    m=files['src/ProcessLens.App/MainWindow.xaml.cs']; bad=[]
    def need(ok,label):
        if not ok: bad.append(label)
    need(all(x in c for x in ['MaximumBuffer = 16 * 1024 * 1024', 'MaximumRows = 16384', 'attempt < 8',
         'next > MaximumBuffer', 'finally { Marshal.FreeHGlobal(buffer); }']), 'bounded native allocation retries and release')
    need(all(x in c for x in ['offset > length - header','next > length - offset - header','next % IntPtr.Size',
         'nameOffset > end - n.Length','n.Length > 2048','(n.Length & 1)', '!pids.Add((int)pid)']), 'native buffer offset Unicode and duplicate guards')
    need('token.ThrowIfCancellationRequested()' in c and 'cancellationToken.ThrowIfCancellationRequested()' in c,
         'cancellation between native calls rows and identity queries')
    need('NTSTATUS 0x{status:X8}' in c and 'ReadToolhelp(cancellationToken)' in c and 'error != 18' in c,
         'native failure preserved and Toolhelp abnormal ending not accepted')
    need('OpenProcess(0x1000, false' in c and 'finally { CloseHandle(handle); }' in c and
         not any(x in c+u for x in ['.Kill(', 'Process.Start(', 'AdjustTokenPrivileges(', 'PROCESS_VM_READ', 'DispatcherTimer']),
         'limited read-only handles no process actions privilege changes or timer')
    need('if (start > began)' in c and 'GetFileName(value).Equals(row.Name' in c and
         'entry.StartUtc.Value == process.StartTime.Value.ToUniversalTime()' in p and 'entry.Name.Equals(process.Name' in p and
         '!SystemProcessPresentation.SameInstance(target, current.Process)' in m and 'navigation != _workspaceNavigation' in m,
         'metadata and detail navigation guarded against instance mismatch')
    need('PrivatePageCount' in c and 'Reserved1[' not in c and 'Reserved7[' not in c and
         'pages > (ulong)long.MaxValue / pageSize' in c and 'pageSize == 0' in c,
         'reserved members not interpreted and page conversion bounded')
    need('Working Set totale:' in p and 'non sommarli' in p and 'bytes is >= 0' in p and
         'if (workingSet == 0 &&' in c and 'resident = null' in c, 'physical total residency separated from kernel commit and placeholder zero')
    need('Names.Contains(p.Name)' in p and 'TrimEnd' in p and 'x is "." or ".."' in p and
         'prefi' in p and 'non verificata' in p, 'name hints not ownership and Windows directory boundary')
    need('if (!_active || _busy) return;' in u and 'generation != _generation' in u and
         '_operation?.Cancel()' in u and '_catalog = null' in u and 'SystemProcessesTable.ItemsSource = null;' in u,
         'coalesced generation guarded reads and release when hidden')
    exact_pid = 'p.Pid.ToString(CultureInfo.InvariantCulture) == search' in u or (
        'p.Entry.Pid.ToString(CultureInfo.InvariantCulture) == search' in files.get('src/ProcessLens.Core/WindowsComponentCatalog.cs','') and
        'WindowsComponentCatalog.Groups(c.Processes' in u)
    need('ClipboardText.Copy(this,' in u and 'GridCopySupport.CaptureTable' in u and exact_pid,
         'copy actual rendered report and exact PID search')
    need('system && SystemProcesses is null' in m and 'SystemProcesses?.SetActive(active && _systemMode)' in m and
         '!_networkMode && !_systemMode' in m, 'lazy workspace lifecycle and no periodic system scan')
    return bad

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--candidate',type=Path,required=True);parser.add_argument('--baseline-zip',type=Path,required=True)
    parser.add_argument('--output',type=Path,required=True)
    a=parser.parse_args();root=a.candidate.resolve()
    assert hashlib.sha256(a.baseline_zip.read_bytes()).hexdigest()=='c11ff4eb1b4809776d58171f4df62af5eebc21f8f07e918b3e5e780545f9b824'
    with zipfile.ZipFile(a.baseline_zip) as z:old={n.split('/',1)[1]:z.read(n) for n in z.namelist() if not n.endswith('/')}
    files={f.relative_to(root).as_posix():f.read_text(encoding='utf-8-sig') for f in root.rglob('*') if f.suffix in {'.cs','.xaml','.ps1'}}
    results=[]
    def check(label,ok):
        results.append({'test':label,'status':'PASS' if ok else 'FAIL','kind':'source/XML or source mutation only'})
        print(results[-1]['status'],label)
    check('R14 satisfies native source and lifecycle guards',not violations(files))
    changed={rel for rel in old if rel.startswith('src/') and (root/rel).read_bytes()!=old[rel]}
    allowed={'src/ProcessLens.App/MainWindow.xaml','src/ProcessLens.App/MainWindow.xaml.cs',
             'src/ProcessLens.App/ProcessLens.App.csproj','src/ProcessLens.App/CopyTestVisibility.cs'}
    check('only R14 UI changes and exact recorded R14 R1/R2/R3 fixes and exact R15 presentation since R13',changed - {p for p in changed if any(approved_feature(root,p,rev) for rev in ('R10_R14_R1','R10_R14_R2','R10_R14_R3','R10_R15','R10_R16','R10_R17','R10_R18','R10_R18_R1','R10_R18_R2','R10_R19'))} == allowed - {p for p in allowed if any(approved_feature(root,p,rev) for rev in ('R10_R14_R1','R10_R14_R2','R10_R14_R3','R10_R15','R10_R16','R10_R17','R10_R18','R10_R18_R1','R10_R18_R2','R10_R19'))})
    check('existing sources exact to R13 outside R14 UI and exact R14 R1/R2/R3 fixes and exact R15 presentation',
          all((root/p).read_bytes()==data or any(approved_feature(root,p,rev) for rev in ('R10_R14_R1','R10_R14_R2','R10_R14_R3','R10_R15','R10_R16','R10_R17','R10_R18','R10_R18_R1','R10_R18_R2','R10_R19')) for p,data in old.items() if p.startswith('src/') and p not in allowed))
    check('no baseline file deleted',all((root/p).is_file() for p in old))
    approved=json.loads((root/'audit/R10_R14/authorized-source-edits.json').read_text())['files']
    check('R14 production hashes retained or replaced by exact R14 R1/R2/R3 or R15 hashes',all(hashlib.sha256((root/r['file']).read_bytes()).hexdigest()==r['sha256_after'] or any(approved_feature(root,r['file'],rev) for rev in ('R10_R14_R1','R10_R14_R2','R10_R14_R3','R10_R15','R10_R16','R10_R17','R10_R18','R10_R18_R1','R10_R18_R2','R10_R19')) for r in approved))
    for f in [*root.rglob('*.xaml'),*root.rglob('*.csproj')]: ET.parse(f)
    check('all project XML and XAML parse',True)
    view=ET.parse(root/'src/ProcessLens.App/SystemProcessesView.xaml').getroot();x='{http://schemas.microsoft.com/winfx/2006/xaml}';w='{http://schemas.microsoft.com/winfx/2006/xaml/presentation}';local='{clr-namespace:ProcessLens.App}'
    names=[e.get(x+'Name') for e in view.iter() if e.get(x+'Name')]
    check('view names unique and declared handlers connected',len(names)==len(set(names)) and all(re.search(r'\b'+re.escape(v)+r'\s*\(',files['src/ProcessLens.App/SystemProcessesView.xaml.cs']) for e in view.iter() for k,v in e.attrib.items() if k in {'Click','SelectionChanged','TextChanged'}))
    check('system report text selectable and wrapped',all(e.get('TextWrapping')=='Wrap' for e in view.iter(local+'SelectableText')))
    check('system table virtualized copied read-only',all(e.get('IsReadOnly')=='True' and e.get('VirtualizingPanel.VirtualizationMode')=='Recycling' and e.get(local+'GridCopySupport.Enabled')=='True' for e in view.iter(w+'DataGrid')))
    check('system buttons wrap without fixed height',all(e.get('Style')=='{StaticResource SystemAction}' and 'Height' not in e.attrib for e in view.iter(w+'Button')) and any(e.get('TextWrapping')=='Wrap' for e in view.iter(w+'TextBlock')))
    main=ET.parse(root/'src/ProcessLens.App/MainWindow.xaml').getroot()
    check('main navigation wired with lazy host and original button rules',not button_violations(main) and any(e.get(x+'Name')=='NavSystemButton' for e in main.iter()) and not list(main.iter(local+'SystemProcessesView')))
    check('all clipboard calls keep required owner and text arguments',not any(invalid_calls(f.read_text()) for f in (root/'src/ProcessLens.App').rglob('*.cs')))
    check('R13 Wireshark source guards still pass',not packet_violations(files))
    check('R12 network source guards still pass',not network_violations(files))
    test=files['tests/ProcessLens.SystemProcessTests/Program.cs'];ui=files['tests/ProcessLens.SystemProcessUiTests/Program.cs']
    check('native tests include independent binary ABI fixtures boundaries cultures and Windows self comparison',all(s in test for s in ['wide ? 256 : 184','Marshal.WriteIntPtr','Memory Compression','reject name outside record','it-IT','en-US','Process.GetCurrentProcess()','self.StartTime.ToUniversalTime()']))
    check('WPF fixture exercises actual bindings copy scope navigation and late completion',all(s in ui for s in ['new SystemProcessesView(source','GridCopySupport.CaptureTable','scope.SelectedIndex = 1','source.Release.Set()','view.AnalysisRequested','grid.Items.Count == 0']))
    build=files['build-dashboard.ps1']
    check('both C# suites and source gate in standard Windows build',all(s in build for s in ['check-system-processes-r14.ps1','Tests.ProcessLens.SystemProcessTests','Tests.ProcessLens.SystemProcessUiTests']))
    paths={'component':'src/ProcessLens.Core/WindowsComponentCatalog.cs','native':'src/ProcessLens.Windows/WindowsSystemProcessSource.cs','model':'src/ProcessLens.Core/SystemProcessCatalog.cs','view':'src/ProcessLens.App/SystemProcessesView.xaml.cs','main':'src/ProcessLens.App/MainWindow.xaml.cs'}
    mutations=[('native buffer bound','native','next > MaximumBuffer','false'),('native buffer release','native','Marshal.FreeHGlobal(buffer);','KeepBuffer();'),
      ('next-record bounds','native','next > length - offset - header','false'),('Unicode pointer bounds','native','nameOffset > end - n.Length','false'),
      ('duplicate PID','native','!pids.Add((int)pid)','false'),('handle scope','native','OpenProcess(0x1000, false','OpenProcess(0x1fffff, false'),
      ('reused PID age check','native','if (start > began)','if (false)'),('native error retained','native','NTSTATUS 0x{status:X8}','empty inventory'),
      ('fallback termination error','native','error != 18','false'),('page overflow','native','pages > (ulong)long.MaxValue / pageSize','false'),
      ('unknown creation','model','entry.StartUtc.Value == process.StartTime.Value.ToUniversalTime()','true'),
      ('name ownership','model','Names.Contains(p.Name)','p.Name.Contains("system")'),
      ('late result','view','generation != _generation','false'),('hidden cancellation','view','_operation?.Cancel()','KeepQuery()'),
      ('PID substring search','component' if 'src/ProcessLens.Core/WindowsComponentCatalog.cs' in files else 'view','p.Entry.Pid.ToString(CultureInfo.InvariantCulture) == search' if 'src/ProcessLens.Core/WindowsComponentCatalog.cs' in files else 'p.Pid.ToString(CultureInfo.InvariantCulture) == search','p.Entry.Pid.ToString().Contains(search)'),
      ('navigation identity','main','!SystemProcessPresentation.SameInstance(target, current.Process)','false')]
    for label,kind,before,after in mutations:
        path=paths[kind];assert before in files[path],label
        altered=dict(files);altered[path]=altered[path].replace(before,after)
        check('reject '+label,bool(violations(altered)))
    a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(results,indent=2)+'\n')
    raise SystemExit(0 if all(r['status']=='PASS' for r in results) else 1)
if __name__=='__main__': main()
