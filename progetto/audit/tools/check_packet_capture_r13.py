"""R13 source/XML and mutation checks only. Does not execute C#, drivers or captures."""
import argparse, hashlib, json, re, zipfile
from pathlib import Path
import xml.etree.ElementTree as ET
from test_clipboard_calls_r10r9 import invalid_calls
from ux_writing_rules import approved_feature

def violations(files):
    c=files['src/ProcessLens.Windows/WiresharkTools.cs']; p=files['src/ProcessLens.Core/PacketCaptureEvidence.cs']
    u=files['src/ProcessLens.App/NetworkCaptureView.xaml.cs']; parent=files['src/ProcessLens.App/NetworkDevicesView.xaml.cs']
    bad=[]
    def need(ok,label):
        if not ok:bad.append(label)
    need('UseShellExecute = false' in c and 'child.StartInfo.ArgumentList.Add(argument)' in c and
         not any(x in c+u for x in ['cmd.exe','powershell.exe','GetProcessesByName(', 'GetProcessById(']), 'no shell or foreign-process lookup')
    need('budget.CancelAfter' in c and 'PacketCapturePolicy.MaximumTextChars - destination.Length' in c and
         'budget.Cancel(); break;' in c and 'Task.WhenAll(stdout, stderr)' in c, 'bounded asynchronous output and time')
    need('if (started)' in c and 'if (!child.HasExited) child.Kill();' in c and
         'started = true;' in c and 'child.WaitForExitAsync(stop.Token)' in c, 'cleanup applies only to the exact launched child')
    need('FileMode.CreateNew' in c and 'FileShare.Read,' in c and 'SHA256.HashDataAsync' in c and
         'file.Length > PacketCapturePolicy.MaximumFileBytes' in c, 'new capture files immutable bounded analysis and source hash')
    need('LocalInterface(adapter.Name)' in p and 'Guid.TryParseExact(name[12..], "B"' in p and
         'path.StartsWith(@"\\\\"' in p, 'stable local identifiers and local file destinations')
    need('"duration:20"' in p and '"filesize:10000"' in p and '"-c", "10000"' in p and
         '"-s", "256"' in p and '"-B", "2"' in p, 'capture duration packet snaplen size and buffer budgets')
    need('if (monitor) args.Add("-I"); else args.Add("-p");' in p and
         '_verifiedMonitor.Contains(Stamp(tools, adapter))' in c and 'result.Success && PacketCapturePolicy.HasMonitorLinkType' in c,
         'monitor opt-in and exact-interface capability declaration')
    need('new[] { "-i", adapter.Name, "-I", "-L" }' in c and 'File.Exists(npcap)' in c and
         'Path.GetDirectoryName(tools.Dumpcap)' in c, 'explicit probe and installed sibling tools')
    need('"-n", "-r", path' in p and '"-c", "2048"' in p and '"escape=y"' in p and
         'if (!result.Success) throw new IOException' in c, 'offline decoder cannot fabricate an empty result after error')
    need('rows.Count < 512' in p and 'f.Length != 9' in p and 'char.IsControl(c)' in p and
         'type == 0 && subtype is 5 or 8' in p, 'bounded defensive parser and advertised SSID only in beacon or response')
    need('generation == _generation' in u and '_operation?.Cancel()' in u and
         'CaptureObservationsTable.ItemsSource = null;' in u and 'if (!_active || _busy)' in u,
         'inactive UI cancels owned work and rejects late publication')
    need('CaptureInterfacesBox.SelectedIndex = -1' in u and
         'CaptureMonitorBox.IsChecked != true || _monitorVerified' in u and '_fileReady = result.Complete' in u,
         'explicit interface selection and valid-file action gates')
    need('NetworkTabs.SelectedIndex == 1 && _captureView is null' in parent and
         '_captureView?.SetActive(active && NetworkTabs.SelectedIndex == 1)' in parent,
         'packet pane is lazy and follows parent visibility')
    return bad

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--candidate',type=Path,required=True);parser.add_argument('--baseline-zip',type=Path,required=True);parser.add_argument('--output',type=Path,required=True)
    a=parser.parse_args();root=a.candidate
    assert hashlib.sha256(a.baseline_zip.read_bytes()).hexdigest()=='66139e181d3bcc684f1d4a492976f15b8ca1b08eed3aeb92d31d9b1a39e2d6dd'
    with zipfile.ZipFile(a.baseline_zip) as z:old={n.split('/',1)[1]:z.read(n) for n in z.namelist() if not n.endswith('/')}
    files={f.relative_to(root).as_posix():f.read_text(encoding='utf-8-sig') for f in root.rglob('*') if f.suffix in {'.cs','.xaml','.ps1'}}
    rows=[]
    def check(label,ok):rows.append({'test':label,'status':'PASS' if ok else 'FAIL','kind':'source/XML or source mutation only'});print(rows[-1]['status'],label)
    check('packet integration satisfies all source guards',not violations(files))
    allowed={'src/ProcessLens.App/NetworkDevicesView.xaml','src/ProcessLens.App/NetworkDevicesView.xaml.cs','src/ProcessLens.App/ProcessLens.App.csproj'}
    check('R13 changes plus exact authorized R14 and R15 source integration since R12',
          {rel for rel in old if rel.startswith('src/') and (root/rel).read_bytes()!=old[rel] and not any(approved_feature(root,rel,r) for r in ('R10_R14','R10_R14_R1','R10_R14_R2','R10_R14_R3','R10_R15','R10_R16','R10_R17','R10_R18','R10_R18_R1','R10_R18_R2','R10_R19'))}==allowed - {rel for rel in allowed if any(approved_feature(root,rel,r) for r in ('R10_R14','R10_R14_R1','R10_R14_R2','R10_R14_R3','R10_R15','R10_R16','R10_R17','R10_R18','R10_R18_R1','R10_R18_R2','R10_R19'))})
    check('existing sources exact to R12 outside R13 and exact authorized R14 and R15 integration',
          all((root/rel).read_bytes()==data or any(approved_feature(root,rel,r) for r in ('R10_R14','R10_R14_R1','R10_R14_R2','R10_R14_R3','R10_R15','R10_R16','R10_R17','R10_R18','R10_R18_R1','R10_R18_R2','R10_R19')) for rel,data in old.items() if rel.startswith('src/') and rel not in allowed))
    approved=json.loads((root/'audit/R10_R13/authorized-source-edits.json').read_text())['files']
    check('R13 source hashes retained or superseded by exact authorized R14 and R15 hashes',all(hashlib.sha256((root/r['file']).read_bytes()).hexdigest()==r['sha256_after'] or any(approved_feature(root,r['file'],revision) for revision in ('R10_R14','R10_R14_R1','R10_R14_R2','R10_R14_R3','R10_R15','R10_R16','R10_R17','R10_R18','R10_R18_R1','R10_R18_R2','R10_R19')) for r in approved))
    for f in [*root.rglob('*.xaml'),*root.rglob('*.csproj')]:ET.parse(f)
    check('all XAML and project XML parse',True)
    view=ET.parse(root/'src/ProcessLens.App/NetworkCaptureView.xaml').getroot();x='{http://schemas.microsoft.com/winfx/2006/xaml}';w='{http://schemas.microsoft.com/winfx/2006/xaml/presentation}';local='{clr-namespace:ProcessLens.App}'
    names=[e.get(x+'Name') for e in view.iter() if e.get(x+'Name')]
    check('capture view names unique and handlers connected',len(names)==len(set(names)) and all(re.search(r'\b'+re.escape(v)+r'\s*\(',files['src/ProcessLens.App/NetworkCaptureView.xaml.cs']) for e in view.iter() for k,v in e.attrib.items() if k in {'Click','SelectionChanged','Checked','Unchecked'}))
    check('capture outputs copied wrapped and virtualized',all(e.get('TextWrapping')=='Wrap' for e in view.iter(local+'SelectableText')) and all(e.get(local+'GridCopySupport.Enabled')=='True' and e.get('VirtualizingPanel.VirtualizationMode')=='Recycling' for e in view.iter(w+'DataGrid')))
    check('capture buttons wrap with no fixed height',all(e.get('Style')=='{StaticResource CaptureAction}' and 'Height' not in e.attrib for e in view.iter(w+'Button')))
    check('all clipboard calls preserve two required arguments',not any(invalid_calls(f.read_text()) for f in (root/'src/ProcessLens.App').rglob('*.cs')))
    t=files['tests/ProcessLens.PacketCaptureTests/Program.cs'];ui=files['tests/ProcessLens.NetworkUiTests/Program.cs']
    check('independent synthetic CLI parser and owned-process tests supplied',all(s in t for s in ['it-IT','en-US','--capture-fixture','OWNED FIXTURE','Runner','FixtureRunner','existing capture files cannot be overwritten','no decoded Wi-Fi does not establish absence','oversized imported capture']))
    check('real WPF fixture covers selection monitor and late results',all(s in ui for s in ['new NetworkCaptureView(packets)','captureView.VerifyMonitorAsync','captureView.CaptureAsync','packets.Block.SetResult','packet capture pane is lazy']))
    check('Windows build runs source gate and packet suite','check-packet-capture-r13.ps1' in files['build-dashboard.ps1'] and 'Tests.ProcessLens.PacketCaptureTests' in files['build-dashboard.ps1'])
    paths={'client':'src/ProcessLens.Windows/WiresharkTools.cs','policy':'src/ProcessLens.Core/PacketCaptureEvidence.cs','ui':'src/ProcessLens.App/NetworkCaptureView.xaml.cs'}
    mutations=[('shell enabled','client','UseShellExecute = false','UseShellExecute = true'),('unbounded output','client','PacketCapturePolicy.MaximumTextChars - destination.Length','int.MaxValue'),('unbounded child wait','client','budget.CancelAfter','NoTimeout'),('foreign PID substitution','client','child.Kill();','Process.GetProcessById(5).Kill();'),('overwrite capture','client','FileMode.CreateNew','FileMode.Create'),('decoder input not locked','client','FileShare.Read,','FileShare.ReadWrite,'),('missing source hash','client','SHA256.HashDataAsync','NoHash'),('remote or index input accepted','policy','LocalInterface(adapter.Name)','true'),('capture duration removed','policy','duration:20','duration:20000'),('packet cap removed','policy','"-c", "10000"','"-c", "10000000"'),('monitor gate bypassed','client','_verifiedMonitor.Contains(Stamp(tools, adapter))','true'),('failed decoder becomes success','client','if (!result.Success) throw new IOException','if (false) throw new IOException'),('payload SSID fabricated','policy','type == 0 && subtype is 5 or 8','type == 2'),('old UI generation accepted','ui','generation == _generation','true'),('hidden capture left running','ui','_operation?.Cancel()','KeepRunning()'),('first interface auto-selected','ui','CaptureInterfacesBox.SelectedIndex = -1','CaptureInterfacesBox.SelectedIndex = 0')]
    for label,kind,before,after in mutations:
        path=paths[kind];assert before in files[path],label
        changed=dict(files);changed[path]=changed[path].replace(before,after)
        check('reject '+label,bool(violations(changed)))
    a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(rows,indent=2)+'\n')
    raise SystemExit(0 if all(r['status']=='PASS' for r in rows) else 1)
if __name__=='__main__':main()
