"""R17 source/XML, mutation and current portable evidence guards. Does not qualify WPF or live Windows APIs."""
import argparse,hashlib,json,zipfile
from pathlib import Path
import xml.etree.ElementTree as ET
from ux_writing_rules import approved_feature
from check_wpf_test_context_r14r3 import valid_context,checks
from test_clipboard_calls_r10r9 import invalid_calls

def violations(files):
 n=files['src/ProcessLens.Windows/WindowsNativeCapture.cs'];c=files['src/ProcessLens.Core/NativeCaptureEvidence.cs'];u=files['src/ProcessLens.App/NativeCaptureView.xaml.cs'];bad=[]
 def need(ok,label):
  if not ok:bad.append(label)
 need('NativeLibrary.TryGetExport' in n and 'Environment.SpecialFolder.System' in n and '0x00010000' in n and 'Architecture.X64' in n,'exact dynamic system exports API version and supported ABI')
 need('PacketMonitorCreateLiveSession' in n and '"ProcessLens-" + Guid.NewGuid()' in n and 'PacketMonitorCloseSessionHandle' in n and 'PacketMonitorCloseRealtimeStream' in n,'private session and stream handles')
 need('BufferSizeMultiplier = 1' in n and 'TruncationSize = NativeCaptureEvidence.SnapshotBytes' in n and 'MaximumRows = 2048' in c and 'SnapshotBytes = 128' in c and 'DurationSeconds = 20;' in c and 'rows.Count >= NativeCaptureEvidence.MaximumRows' in n,'bounded callback rows snap length ring and active wait')
 need('(ulong)d.MetadataOffset + 48 > d.DataSize' in n and '(ulong)d.PacketOffset + d.PacketLength > d.DataSize' in n and 'd.PacketLength > NativeCaptureEvidence.SnapshotBytes' in n and 'd.DataSize > 65536' in n,'validate descriptor offsets lengths before dereference')
 need('MaximumNativeBuffer = 1048576' in c and 'MaximumSources = 256' in c and 'offset < 8L + count * IntPtr.Size' in n and 'offset > length - 8' in n and 'count > NativeCaptureEvidence.MaximumSources' in n,'bounded owned enumeration and pointer range')
 need('source.Name != NativeCaptureEvidence.Clean(selected.Name)' in n and '_sources.TryGetValue(source.Key' in n and '_sources.Clear()' in n,'opaque discovery lease rejects stale selection')
 need('GC.KeepAlive(data)' in n and 'GC.KeepAlive(events)' in n and 'if (stream != IntPtr.Zero) _api.CloseStream(stream)' in n and 'if (session != IntPtr.Zero) _api.CloseSession(session)' in n and 'if (session != IntPtr.Zero) _api.SetActive(session, false)' in n,'callback roots retained through owned cleanup')
 need('missedWrites += d.MissedWrites' in n and 'missedReads += d.MissedReads' in n and 'partial || missedWrites > 0 || missedReads > 0' in n and 'kind == 2' in n and 'if (!stopping)' in n,'loss fatal events and unexpected stop preserve partial status')
 need('[MarshalAs(UnmanagedType.U1)] bool active' in n and '[MarshalAs(UnmanagedType.U1)] bool hidden' in n and '[MarshalAs(UnmanagedType.Bool)] bool security' in n,'BOOLEAN versus BOOL marshaling')
 need('WlanOpenHandle(2' in n and 'WlanEnumInterfaces' in n and 'WlanGetNetworkBssList(client, ref id, IntPtr.Zero, 1, false' in n and 'i * 532' in n and 'count > 16' in n,'manual native WLAN enumeration and SDK interface layout')
 need('finally { if (bss != IntPtr.Zero) WlanFreeMemory(bss); }' in n and 'WlanFreeMemory(interfaces)' in n and 'WlanCloseHandle(client' in n and 'Marshal.FreeHGlobal(_buffer)' in n and '_uninitialize(_handle)' in n and 'NativeLibrary.Free(_module)' in n,'WLAN enumeration and engine module resources released')
 need('BssEntrySize = 360' in c and 'MaximumBssRows = 512' in c and 'len > 32' in c and 'count > MaximumBssRows' in c and '8L + count * BssEntrySize > size' in c and 'UTF8Encoding(false, true)' in c and 'Clean(ssid, 96)' in c,'bounded BSS array and untrusted SSID encoding')
 need('non client connessi' in c and 'non all’intera rete' in c and 'dati possono essere precedenti' in c and 'i privilegi amministrativi non sostituiscono questo consenso' in n and 'Posizione' in n,'cache visibility location permissions and coverage explicit')
 need('if (!_active || _busy) return;' in u and 'generation != _generation' in u and '_operation?.Cancel()' in u and 'NativeCaptureSourcesBox.SelectedIndex = -1' in u and 'NativePacketsTable.ItemsSource = null' in u and 'WifiBssTable.ItemsSource = null' in u,'manual coalesced UI selection cancellation and late result checks')
 need(('ClipboardText.Copy(this, _report)' in u or 'ClipboardText.Copy(this, _report.Length > 0 ? _report : _lastDiagnostics)' in u) and 'StartedUtc' in c and 'SourceName' in c and 'Perdite segnalate: N/D' in c,'copy retains source bounds and unavailable metrics')
 need(not any(t in c+n+u for t in ['Process.Start(', 'DispatcherTimer', 'WlanSetInterface', 'WlanScan(', 'Socket.Connect', 'HttpClient', 'AuditSetSystemPolicy', 'FwpmFilterAdd']), 'no external child auto refresh radio mode policy change or network probe')
 return bad

def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--candidate',type=Path,required=True);p.add_argument('--baseline-zip',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args();r=a.candidate.resolve();audit=r/'audit/R10_R17';sha=lambda b:hashlib.sha256(b).hexdigest()
 assert sha(a.baseline_zip.read_bytes())=='dfc956ea3405d82e1a1e01d94c4d685030e183d73af53c4c1fa5399ea6a813e3'
 with zipfile.ZipFile(a.baseline_zip) as z:old={n.split('/',1)[1]:z.read(n) for n in z.namelist() if not n.endswith('/')}
 rows=[]
 def check(label,ok):rows.append({'test':label,'status':'PASS' if ok else 'FAIL','kind':'source/XML/mutation or portable evidence correlation; not Windows runtime'});print(rows[-1]['status'],label)
 ledger=json.loads((audit/'authorized-source-edits.json').read_text())['files'];allowed={x['file'] for x in ledger}
 check('exact R16 baseline and R17 source/test ledger',all((sha((r/x['file']).read_bytes())==x['sha256_after'] or any(approved_feature(r,x['file'],revision181) for revision181 in ('R10_R18','R10_R18_R1','R10_R18_R2','R10_R19'))) and (sha(old[x['file']]) if x['file'] in old else None)==x['sha256_before'] for x in ledger))
 check('all prior files retained',all((r/n).is_file() for n in old))
 check('no source or C# test outside authorized delta',all(n in allowed or any(approved_feature(r,n,revision181) for revision181 in ('R10_R18','R10_R18_R1','R10_R18_R2','R10_R19')) for n in {f.relative_to(r).as_posix() for f in r.rglob('*') if f.is_file()} if (n.startswith('src/') or n.startswith('tests/') and n.endswith(('.cs','.csproj'))) and old.get(n)!=(r/n).read_bytes()))
 check('Guardian MemoryAgent profiler and all other baseline collectors retained byte for byte',all((r/n).read_bytes()==b or any(approved_feature(r,n,revision181) for revision181 in ('R10_R18','R10_R18_R1','R10_R18_R2','R10_R19')) for n,b in old.items() if n.startswith('src/') and n not in allowed))
 check('legacy external capture source and fixtures retained byte for byte',all((r/n).read_bytes()==old[n] for n in ['src/ProcessLens.Windows/WiresharkTools.cs','src/ProcessLens.Core/PacketCaptureEvidence.cs','src/ProcessLens.App/NetworkCaptureView.xaml','src/ProcessLens.App/NetworkCaptureView.xaml.cs','tests/ProcessLens.PacketCaptureTests/Program.cs']))
 files={f.relative_to(r).as_posix():f.read_text(encoding='utf-8-sig') for f in r.rglob('*') if f.suffix in {'.cs','.xaml','.ps1'}}
 check('all native source guards',not violations(files))
 x='{http://schemas.microsoft.com/winfx/2006/xaml}';w='{http://schemas.microsoft.com/winfx/2006/xaml/presentation}';local='{clr-namespace:ProcessLens.App}'
 view=ET.fromstring(files['src/ProcessLens.App/NativeCaptureView.xaml']);names=[e.get(x+'Name') for e in view.iter() if e.get(x+'Name')]
 check('new markup names unique and handlers connected',len(names)==len(set(names)) and all(v+'(' in files['src/ProcessLens.App/NativeCaptureView.xaml.cs'] for e in view.iter() for k,v in e.attrib.items() if k in {'Click','SelectionChanged'}))
 check('both real tables read-only copied and virtualized',len(list(view.iter(w+'DataGrid')))==2 and all(e.get('IsReadOnly')=='True' and e.get(local+'GridCopySupport.Enabled')=='True' and e.get('VirtualizingPanel.VirtualizationMode')=='Recycling' for e in view.iter(w+'DataGrid')))
 check('buttons wrap without fixed height and report text selectable',all(e.get('Style')=='{StaticResource CaptureAction}' and 'Height' not in e.attrib for e in view.iter(w+'Button')) and all(e.get('TextWrapping')=='Wrap' for e in view.iter(local+'SelectableText')))
 parent=ET.fromstring(files['src/ProcessLens.App/NetworkDevicesView.xaml']);tabs=next(e for e in parent.iter(w+'TabControl') if e.get(x+'Name')=='NetworkTabs')
 check('native capture replaces external installation pane and initial cache remains selected',tabs.get('SelectedIndex')=='0' and [e.get('Header') for e in tabs.findall(w+'TabItem')]==['Dispositivi nella cache','Cattura Windows / Wi-Fi','Segnali Windows'])
 source=files['src/ProcessLens.App/NetworkDevicesView.xaml.cs'];check('native pane lazy activation follows visibility',all(t in source for t in ['NetworkTabs.SelectedIndex == 1 && _captureView is null','new NativeCaptureView()','_captureView?.SetActive(active && NetworkTabs.SelectedIndex == 1)','_captureView?.SetActive(_active && NetworkTabs.SelectedIndex == 1)']))
 ui=files['tests/ProcessLens.NetworkUiTests/Program.cs'];before=old['tests/ProcessLens.NetworkUiTests/Program.cs'].decode('utf-8-sig');previous=checks(before);current=checks(ui)
 changed=[s for s in previous if s not in current]
 check('all previous UI assertions preserved except explicit capture implementation replacement',len(changed)==1 and 'packetHost.ContentisNetworkCaptureView' in changed[0] and 'packetHost.Content is NativeCaptureView' in ui and valid_context(ui))
 check('new UI fixtures cover lazy selection copies pending work late results unavailable state',all(t in ui for t in ['new NativeCaptureView(nativeCaptureFixture)','nativeSources.SelectedIndex == -1','nativeCaptureFixture.Started.Wait','nativeCaptureFixture.Calls == calls','nativeCaptureFixture.Released > 0','fixture access denied','wifiGrid.Items.Count == 0']))
 check('all copy calls keep owner and text',not any(invalid_calls(p.read_text()) for p in (r/'src/ProcessLens.App').rglob('*.cs')))
 build=files['build-dashboard.ps1'];check('standard Windows pipeline includes source gate independent native suite and WPF suite','check-native-capture-r17.ps1' in build and 'Tests.ProcessLens.NativeCaptureTests' in build and 'Tests.ProcessLens.NetworkUiTests' in build)
 prior_stages=[line for line in old['build-dashboard.ps1'].decode('utf-8-sig').splitlines() if line.startswith('Invoke-CheckedDotnet ')]
 check('all prior Windows build and test stages retained verbatim and in order',all(line in build for line in prior_stages) and [build.index(line) for line in prior_stages]==sorted(build.index(line) for line in prior_stages))
 typepath=r/'audit/R10_R19/wpf-typecheck-evidence.json' if approved_feature(r,'src/ProcessLens.App/MainWindow.xaml.cs','R10_R19') else audit/'wpf-typecheck-evidence.json'
 typecheck=json.loads(typepath.read_text())
 check('real C# type checking with explicit field stubs matches current sources and all six assemblies',len(typecheck['assemblies'])==6 and all(row['status']=='PASS' for row in typecheck['assemblies']) and all(sha((r/n).read_bytes())==h or any(approved_feature(r,n,revision181) for revision181 in ('R10_R18','R10_R18_R1','R10_R18_R2','R10_R19')) for n,h in typecheck['inputs'].items()))
 evidence=json.loads((audit/'runtime-evidence.json').read_text());check('portable evidence matches all current compiled inputs',all(sha((r/n).read_bytes())==h or any(approved_feature(r,n,revision181) for revision181 in ('R10_R18','R10_R18_R1','R10_R18_R2','R10_R19')) for n,h in evidence['source_hashes'].items()))
 for suite,count in [('NativeCaptureTests',75),('NativeNetworkTests',40),('WindowsComponentTests',58),('SystemProcessTests',22),('NetworkDeviceTests',81)]:
  name='ProcessLens.'+suite;item=next(t for t in evidence['outcomes'] if t['suite']==name);log=(audit/(name+'-run.log')).read_text()
  check('executed portable '+suite,item['status']=='PASS' and item['assertions']==count and sum(l.startswith('PASS ') for l in log.splitlines())==count and (audit/(name+'-build.log')).read_text()=='')
 for label,path,before,after in [
  ('row cap','src/ProcessLens.Core/NativeCaptureEvidence.cs','MaximumRows = 2048','MaximumRows = int.MaxValue'),
  ('snap length','src/ProcessLens.Core/NativeCaptureEvidence.cs','SnapshotBytes = 128','SnapshotBytes = 9000'),
  ('duration','src/ProcessLens.Core/NativeCaptureEvidence.cs','DurationSeconds = 20','DurationSeconds = 2000'),
  ('BSS count','src/ProcessLens.Core/NativeCaptureEvidence.cs','count > MaximumBssRows','false'),
  ('SSID size','src/ProcessLens.Core/NativeCaptureEvidence.cs','len > 32','false'),
  ('metadata bound','src/ProcessLens.Windows/WindowsNativeCapture.cs','(ulong)d.MetadataOffset + 48 > d.DataSize','false'),
  ('payload bound','src/ProcessLens.Windows/WindowsNativeCapture.cs','(ulong)d.PacketOffset + d.PacketLength > d.DataSize','false'),
  ('source pointer bound','src/ProcessLens.Windows/WindowsNativeCapture.cs','offset > length - 8','false'),
  ('stream release','src/ProcessLens.Windows/WindowsNativeCapture.cs','if (stream != IntPtr.Zero) _api.CloseStream(stream)','if (false) _api.CloseStream(stream)'),
  ('callback root','src/ProcessLens.Windows/WindowsNativeCapture.cs','GC.KeepAlive(data)','GC.KeepAlive(null)'),
  ('WLAN release','src/ProcessLens.Windows/WindowsNativeCapture.cs','WlanFreeMemory(bss)','WlanFreeMemory(IntPtr.Zero)'),
  ('loss status','src/ProcessLens.Windows/WindowsNativeCapture.cs','partial || missedWrites > 0 || missedReads > 0','partial'),
  ('BOOLEAN ABI','src/ProcessLens.Windows/WindowsNativeCapture.cs','[MarshalAs(UnmanagedType.U1)] bool active','[MarshalAs(UnmanagedType.Bool)] bool active'),
  ('late publication','src/ProcessLens.App/NativeCaptureView.xaml.cs','generation != _generation','false')]:
  probe=dict(files);assert before in probe[path];probe[path]=probe[path].replace(before,after);check('reject '+label,bool(violations(probe)))
 for p in [*r.rglob('*.xaml'),*r.rglob('*.csproj')]:ET.parse(p)
 check('all project and XAML XML parse',True)
 syntax=json.loads((audit/'syntax-evidence.json').read_text());check('current edited C# syntax corresponds to exact source bytes',syntax['status']=='PASS' and all(sha((r/n).read_bytes())==h or any(approved_feature(r,n,revision181) for revision181 in ('R10_R18','R10_R18_R1','R10_R18_R2','R10_R19')) for n,h in syntax['source_hashes'].items()))
 a.output.write_text(json.dumps(rows,indent=2)+'\n');raise SystemExit(0 if all(row['status']=='PASS' for row in rows) else 1)
if __name__=='__main__':main()
