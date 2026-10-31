"""Native network source/XML guard and actual portable-evidence checks. No real Windows API execution."""
import argparse,hashlib,json,zipfile
from pathlib import Path
import xml.etree.ElementTree as ET
from check_packet_capture_r13 import violations as packet_guards
from network_device_rules import violations as cache_guards
from check_wpf_test_context_r14r3 import valid_context,checks
from test_clipboard_calls_r10r9 import invalid_calls
from ux_writing_rules import approved_feature

def violations(files):
    n=files['src/ProcessLens.Windows/WindowsNativeNetworkEvidence.cs'];c=files['src/ProcessLens.Core/NativeNetworkEvidence.cs'];u=files['src/ProcessLens.App/NativeNetworkEvidenceView.xaml.cs'];bad=[]
    def need(ok,label):
        if not ok:bad.append(label)
    need('EvtQuery(IntPtr.Zero, "Security", xpath, 0x201)' in n and 'EventID=5157 or EventID=4625' in n and
         "Provider[@Name='Microsoft-Windows-Security-Auditing']" in n and '@SystemTime >=' in n and '@SystemTime <=' in n,'local fixed provider channel events and absolute window')
    need('MaximumEvents = 256' in c and 'MaximumXmlChars = 32768' in c and 'MaximumTotalXmlChars = 1048576' in c and
         'TimeSpan.FromSeconds(3)' in n and 'MaximumTotalXmlChars - chars' in n and 'scanned < NativeNetworkEvidence.MaximumEvents' in n,'bounded per-read event time text and render allocations')
    need('EvtNext(query, 1, events, 250, 0' in n and 'if (error == 259)' in n and 'throw new Win32Exception(error' in n,'finite native wait and only normal ending accepted')
    need('finally { if (query != IntPtr.Zero) _api.Close(query); }' in n and
         'finally { if (record != IntPtr.Zero) _api.Close(record); }' in n and 'finally { Marshal.FreeHGlobal(buffer); }' in n and
         'finally { if (policy != IntPtr.Zero) AuditFree(policy); }' in n,'all query event render and policy resources released')
    need('[return: MarshalAs(UnmanagedType.U1)]' in n and 'AuditQuerySystemPolicy(ref category, 1' in n and 'info.Subcategory != category' in n,
         'BOOLEAN ABI and audit subcategory checked')
    need('needed > NativeNetworkEvidence.MaximumXmlChars * 2u' in n and 'used > needed' in n and
         'Marshal.ReadInt16(buffer, (int)used - 2) != 0' in n,'render buffer bounded null-terminated UTF16')
    need(not any(t in n+c+u for t in ['Process.Start(', 'AuditSetSystemPolicy(', 'FwpmFilterAdd', 'EvtSubscribe', 'AdjustTokenPrivileges',
          'Socket.Connect','HttpClient','DispatcherTimer','WlanSet']), 'no external tool configuration mutation active probe or background subscription')
    need('DtdProcessing = DtdProcessing.Prohibit' in c and 'XmlResolver = null' in c and 'MaxCharactersInDocument = MaximumXmlChars' in c and
         'fields.TryAdd(name, e.Value)' in c,'bounded XML rejects external entities and ambiguous fields')
    need('logon is not ("3" or "10")' in c and 'conteggi N/D' in c and 'non dimostra' in c and 'non identifica l\'istanza attuale' in c,
         'only relevant logon types unavailable counts and historical attribution explicit')
    need('if (!_active || _busy) return;' in u and 'generation != _generation' in u and '_operation?.Cancel()' in u and
         '_snapshot = null' in u and 'NativeNetworkEventsTable.ItemsSource = null;' in u,'manual coalesced operation rejects late results and releases snapshot')
    need('ClipboardText.Copy(this, NativeNetworkEvidence.Report(_snapshot))' in u,'complete bounded report copied from its actual snapshot')
    return bad

def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--candidate',type=Path,required=True);p.add_argument('--baseline-zip',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args();r=a.candidate.resolve();sha=lambda b:hashlib.sha256(b).hexdigest()
    assert sha(a.baseline_zip.read_bytes())=='80b42aa8f5a194c43bf66a9e50ab6ddcfaee2853424d8cc1aaed720f1065bf14'
    with zipfile.ZipFile(a.baseline_zip) as z:old={n.split('/',1)[1]:z.read(n) for n in z.namelist() if not n.endswith('/')}
    rows=[]
    def check(label,ok):rows.append({'test':label,'status':'PASS' if ok else 'FAIL','kind':'source/XML/mutation and portable evidence correlation only'});print(rows[-1]['status'],label)
    files={f.relative_to(r).as_posix():f.read_text(encoding='utf-8-sig') for f in r.rglob('*') if f.suffix in {'.cs','.xaml','.ps1'}}
    ledger=json.loads((r/'audit/R10_R16/authorized-source-edits.json').read_text())['files'];allowed={x['file'] for x in ledger}
    check('exact source and C# test ledger before and after hashes',all((sha((r/x['file']).read_bytes())==x['sha256_after'] or any(approved_feature(r,x['file'],revision18) for revision18 in ('R10_R17','R10_R18','R10_R18_R1','R10_R18_R2','R10_R19'))) and (sha(old[x['file']]) if x['file'] in old else None)==x['sha256_before'] for x in ledger))
    check('existing R15 files retained',all((r/n).is_file() for n in old))
    check('no production or C# test change outside native feature manifest',all(n in allowed or any(approved_feature(r,n,revision18) for revision18 in ('R10_R17','R10_R18','R10_R18_R1','R10_R18_R2','R10_R19')) for n in {f.relative_to(r).as_posix() for f in r.rglob('*') if f.is_file()} if (n.startswith('src/') or n.startswith('tests/') and n.endswith(('.cs','.csproj'))) and old.get(n)!=(r/n).read_bytes()))
    check('existing native collectors semantics Guardian profiler and RAM monitor unchanged',all((r/n).read_bytes()==b or any(approved_feature(r,n,revision18) for revision18 in ('R10_R17','R10_R18','R10_R18_R1','R10_R18_R2','R10_R19')) for n,b in old.items() if n.startswith('src/') and n not in allowed))
    check('all native security evidence guards pass',not violations(files))
    check('cache and Wireshark safety guards retained',not cache_guards(files) and not packet_guards(files))
    x='{http://schemas.microsoft.com/winfx/2006/xaml}';w='{http://schemas.microsoft.com/winfx/2006/xaml/presentation}';local='{clr-namespace:ProcessLens.App}'
    parent=ET.fromstring(files['src/ProcessLens.App/NetworkDevicesView.xaml']);tabs=next(e for e in parent.iter(w+'TabControl') if e.get(x+'Name')=='NetworkTabs')
    check('cache remains explicitly selected and native tab is third',tabs.get('SelectedIndex')=='0' and [e.get('Header') for e in tabs.findall(w+'TabItem')] in (['Dispositivi nella cache','Wireshark / Wi-Fi','Segnali Windows'],['Dispositivi nella cache','Cattura Windows / Wi-Fi','Segnali Windows']))
    u=files['src/ProcessLens.App/NetworkDevicesView.xaml.cs'];native_ui='src/ProcessLens.App/NativeNetworkEvidenceView.xaml.cs'
    check('new pane lazy and follows tab visibility without automatic acquisition',all(t in u for t in ['NetworkTabs.SelectedIndex == 2 && _nativeView is null','NativeNetworkHost.Content = _nativeView','_nativeView?.SetActive(active && NetworkTabs.SelectedIndex == 2)','_nativeView?.SetActive(_active && NetworkTabs.SelectedIndex == 2)']))
    view=ET.fromstring(files['src/ProcessLens.App/NativeNetworkEvidenceView.xaml']);names=[e.get(x+'Name') for e in view.iter() if e.get(x+'Name')]
    check('new XAML names unique and all handlers connected',len(names)==len(set(names)) and all(v+'(' in files[native_ui] for e in view.iter() for k,v in e.attrib.items() if k in {'Click','SelectionChanged'}))
    check('event table copied read-only and virtualized',all(e.get('IsReadOnly')=='True' and e.get(local+'GridCopySupport.Enabled')=='True' and e.get('VirtualizingPanel.VirtualizationMode')=='Recycling' for e in view.iter(w+'DataGrid')))
    check('report selectable and wraps; action buttons have no fixed height',all(e.get('TextWrapping')=='Wrap' for e in view.iter(local+'SelectableText')) and all(e.get('Style')=='{StaticResource NativeAction}' and 'Height' not in e.attrib for e in view.iter(w+'Button')))
    ui=files['tests/ProcessLens.NetworkUiTests/Program.cs'];before=old['tests/ProcessLens.NetworkUiTests/Program.cs'].decode('utf-8-sig')
    check('every previous network UI assertion preserved with dispatcher context',valid_context(ui) and (all(c in checks(ui) for c in checks(before)) or any(approved_feature(r,'tests/ProcessLens.NetworkUiTests/Program.cs',revision18) for revision18 in ('R10_R17','R10_R18','R10_R18_R1','R10_R18_R2','R10_R19'))))
    check('new UI regressions cover lazy acquisition results copying coalescing errors and late work',all(t in ui for t in ['new NativeNetworkEvidenceView(nativeSource)','nativeSource.Calls == 0','nativeSource.Calls == 2','nativeView.SetActive(false)','fixture access denied','R16 native tab constructs','PID può essere stato riutilizzato']))
    check('all clipboard calls retain owner and text',not any(invalid_calls(p.read_text()) for p in (r/'src/ProcessLens.App').rglob('*.cs')))
    check('Windows build includes native source gate and independent portable suite','check-native-network-r16.ps1' in files['build-dashboard.ps1'] and 'Tests.ProcessLens.NativeNetworkTests' in files['build-dashboard.ps1'])
    evidence=json.loads((r/'audit/R10_R16/runtime-evidence.json').read_text())
    check('runtime evidence matches every compiled source',all(sha((r/n).read_bytes())==h for n,h in evidence['source_hashes'].items()))
    for suite,count in [('NativeNetworkTests',40),('WindowsComponentTests',58),('SystemProcessTests',22),('NetworkDeviceTests',81)]:
        log=(r/('audit/R10_R16/ProcessLens.'+suite+'-run.log')).read_text();item=next(i for i in evidence['outcomes'] if i['suite']=='ProcessLens.'+suite)
        check('actual portable '+suite+' assertions',item['status']=='PASS' and item['assertions']==count and sum(l.startswith('PASS ') for l in log.splitlines())==count)
    for label,path,before,after in [
        ('event cap','src/ProcessLens.Core/NativeNetworkEvidence.cs','MaximumEvents = 256','MaximumEvents = int.MaxValue'),
        ('DTD prohibition','src/ProcessLens.Core/NativeNetworkEvidence.cs','DtdProcessing = DtdProcessing.Prohibit','DtdProcessing = DtdProcessing.Parse'),
        ('query release','src/ProcessLens.Windows/WindowsNativeNetworkEvidence.cs','finally { if (query != IntPtr.Zero) _api.Close(query); }','finally { }'),
        ('event release','src/ProcessLens.Windows/WindowsNativeNetworkEvidence.cs','finally { if (record != IntPtr.Zero) _api.Close(record); }','finally { }'),
        ('native timeout','src/ProcessLens.Windows/WindowsNativeNetworkEvidence.cs','EvtNext(query, 1, events, 250, 0','EvtNext(query, 1, events, uint.MaxValue, 0'),
        ('render bound','src/ProcessLens.Windows/WindowsNativeNetworkEvidence.cs','needed > NativeNetworkEvidence.MaximumXmlChars * 2u','false'),
        ('audit BOOLEAN ABI','src/ProcessLens.Windows/WindowsNativeNetworkEvidence.cs','[return: MarshalAs(UnmanagedType.U1)]','[return: MarshalAs(UnmanagedType.Bool)]'),
        ('policy mutation','src/ProcessLens.Windows/WindowsNativeNetworkEvidence.cs','AuditQuerySystemPolicy(ref category, 1','AuditSetSystemPolicy(ref category, 1'),
        ('late publication','src/ProcessLens.App/NativeNetworkEvidenceView.xaml.cs','generation != _generation','false')]:
        probe=dict(files);assert before in probe[path];probe[path]=probe[path].replace(before,after);check('reject '+label,bool(violations(probe)))
    for p in [*r.rglob('*.xaml'),*r.rglob('*.csproj')]:ET.parse(p)
    check('all XAML and project XML parse',True)
    syntax=json.loads((r/'audit/R10_R16/syntax-evidence.json').read_text())
    check('current edited C# syntax evidence matches exact sources',syntax['status']=='PASS' and all(sha((r/n).read_bytes())==h or any(approved_feature(r,n,revision18) for revision18 in ('R10_R17','R10_R18','R10_R18_R1','R10_R18_R2','R10_R19')) for n,h in syntax['source_hashes'].items()))
    a.output.write_text(json.dumps(rows,indent=2)+'\n');raise SystemExit(0 if all(row['status']=='PASS' for row in rows) else 1)
if __name__=='__main__':main()
