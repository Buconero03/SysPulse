"""R14 R2 source/XML and actual XML-guard mutation checks. Does not execute WPF."""
import argparse,copy,hashlib,json,zipfile
from pathlib import Path
import xml.etree.ElementTree as ET
from network_device_rules import violations as network_violations
from check_packet_capture_r13 import violations as packet_violations
from ux_writing_rules import approved_feature

X='{http://schemas.microsoft.com/winfx/2006/xaml}'
W='{http://schemas.microsoft.com/winfx/2006/xaml/presentation}'
def valid_initial_tab(tree):
    tabs=next((e for e in tree.iter(W+'TabControl') if e.get(X+'Name')=='NetworkTabs'),None)
    if tabs is None or tabs.get('SelectedIndex')!='0':return False
    items=list(tabs.findall(W+'TabItem'))
    return [e.get('Header') for e in items] in (["Dispositivi nella cache","Wireshark / Wi-Fi"], ["Dispositivi nella cache","Wireshark / Wi-Fi","Segnali Windows"], ["Dispositivi nella cache","Cattura Windows / Wi-Fi","Segnali Windows"]) and not any(e.get('IsSelected')=='True' for e in items[1:])

def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--candidate',type=Path,required=True);p.add_argument('--baseline-zip',type=Path,required=True)
    p.add_argument('--output',type=Path,required=True);a=p.parse_args();root=a.candidate.resolve()
    sha=lambda b:hashlib.sha256(b).hexdigest()
    assert sha(a.baseline_zip.read_bytes())=='e0fbe1501c5014eb11bc9295a34acfd8e771b40007f7ab5ab41653a337195792'
    with zipfile.ZipFile(a.baseline_zip) as z:old={n.split('/',1)[1]:z.read(n) for n in z.namelist() if not n.endswith('/')}
    results=[]
    def check(label,ok):
        results.append({'test':label,'status':'PASS' if ok else 'FAIL','kind':'source/XML only; no WPF execution'})
        print(results[-1]['status'],label)
    view='src/ProcessLens.App/NetworkDevicesView.xaml';app='src/ProcessLens.App/ProcessLens.App.csproj';test='tests/ProcessLens.NetworkUiTests/Program.cs'
    before=b'<TabControl x:Name="NetworkTabs" SelectionChanged="NetworkTabs_Changed">'
    after=b'<TabControl x:Name="NetworkTabs" SelectedIndex="0" SelectionChanged="NetworkTabs_Changed">'
    check('initial tab delta retained or exact authorized R16 third tab',(old[view].count(before)==1 and (root/view).read_bytes()==old[view].replace(before,after,1)) or any(approved_feature(root,view,revision17) for revision17 in ('R10_R16','R10_R17','R10_R18','R10_R18_R1','R10_R18_R2','R10_R19')))
    check('R2 production delta plus exact authorized R15/R16 presentation',
          {n for n,data in old.items() if n.startswith('src/') and (root/n).read_bytes()!=data}- {n for n in old if n.startswith('src/') and any(approved_feature(root,n,revision) for revision in ('R10_R15','R10_R16','R10_R17','R10_R18','R10_R18_R1','R10_R18_R2','R10_R19'))} == {view,app} - {n for n in (view,app) if any(approved_feature(root,n,revision) for revision in ('R10_R15','R10_R16','R10_R17','R10_R18','R10_R18_R1','R10_R18_R2','R10_R19'))})
    check('production C# retained outside exact authorized R15/R16 presentation',all((root/n).read_bytes()==data or any(approved_feature(root,n,revision) for revision in ('R10_R15','R10_R16','R10_R17','R10_R18','R10_R18_R1','R10_R18_R2','R10_R19')) for n,data in old.items() if n.startswith('src/') and n.endswith('.cs')))
    check('network UI fixture retained outside exact authorized R3/R15/R16 changes',
          {n for n,data in old.items() if n.startswith('tests/') and n.endswith('.cs') and (root/n).read_bytes()!=data and not any(approved_feature(root,n,rev) for rev in ('R10_R14_R3','R10_R15','R10_R16','R10_R17','R10_R18','R10_R18_R1','R10_R18_R2','R10_R19'))}=={test} - {n for n in (test,) if any(approved_feature(root,n,revision) for revision in ('R10_R14_R3','R10_R16','R10_R17','R10_R18','R10_R18_R1','R10_R18_R2','R10_R19'))})
    check('candidate version delta exact',(root/app).read_bytes()==old[app].replace(b'R14-R1-device-catalog-fix-candidate',b'R14-R2-network-tab-init-candidate') or any(approved_feature(root,app,rev) for rev in ('R10_R14_R3','R10_R15','R10_R16','R10_R17','R10_R18','R10_R18_R1','R10_R18_R2','R10_R19')))
    tree=ET.parse(root/view).getroot();check('actual XAML explicitly selects cache before Loaded',valid_initial_tab(tree))
    check('the same XML guard rejects original R1 initialization',not valid_initial_tab(ET.fromstring(old[view])))
    for label,value in [('missing selection',None),('no selected tab','-1'),('capture tab selected','1')]:
        probe=copy.deepcopy(tree);tabs=next(e for e in probe.iter(W+'TabControl') if e.get(X+'Name')=='NetworkTabs')
        if value is None:tabs.attrib.pop('SelectedIndex')
        else:tabs.set('SelectedIndex',value)
        check('reject '+label,not valid_initial_tab(probe))
    probe=copy.deepcopy(tree);tabs=next(e for e in probe.iter(W+'TabControl') if e.get(X+'Name')=='NetworkTabs')
    list(tabs.findall(W+'TabItem'))[1].set('IsSelected','True')
    check('reject conflicting second tab selection',not valid_initial_tab(probe))
    files={f.relative_to(root).as_posix():f.read_text(encoding='utf-8-sig') for f in root.rglob('*') if f.suffix in {'.cs','.ps1','.xaml'}}
    check('cache acquisition and recognition guards retained',not network_violations(files))
    check('packet capability capture and cancellation guards retained',not packet_violations(files))
    ui=files[test]
    check('original three conditions retained as strict independent assertions',all(s in ui for s in ['Check(source.Calls == 1,','Check(grid.Items.Count == 1,','Check(notice.Text == "",','calls={source.Calls}','rows={grid.Items.Count}','notice={notice.Text}']))
    check('headless initial state and tab switch regressions supplied',all(s in ui for s in ['cacheTabs.SelectedIndex == 0','manual refresh enabled before the control is shown','tabs.SelectedIndex = 1','Wireshark tab cannot refresh the cache source','returning to cache tab does not automatically refresh','explicit cache refresh works after switching tabs before Loaded']))
    check('nullable cast checks expected view explicitly','host.Content as NetworkDevicesView' in ui and '?? throw new Exception' in ui and '(NetworkDevicesView)host.Content' not in ui)
    gate=files['tests/check-network-devices-r12.ps1']
    check('standard Windows source gate requires explicit initial tab',"$tabs.GetAttribute('SelectedIndex') -ne '0'" in gate)
    approved=json.loads((root/'audit/R10_R14_R2/authorized-source-edits.json').read_text())['files']
    check('R2 delta hashes retained or superseded by exact authorized R3/R15/R16 hashes',len(approved)==3 and all((sha((root/r['file']).read_bytes())==r['sha256_after'] or any(approved_feature(root,r['file'],rev) for rev in ('R10_R14_R3','R10_R15','R10_R16','R10_R17','R10_R18','R10_R18_R1','R10_R18_R2','R10_R19'))) for r in approved))
    for f in [*root.rglob('*.xaml'),*root.rglob('*.csproj')]:ET.parse(f)
    check('all project and XAML XML parse',True)
    reported=(root/'audit/R10_R14_R2/reported-windows-build.log').read_text()
    check('reported evidence distinguishes completed data suites from failed UI suite',all(s in reported for s in ['Network device regressions complete','Packet capture tests complete','FAIL network UI: first explicit refresh']))
    a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(results,indent=2)+'\n')
    raise SystemExit(0 if all(r['status']=='PASS' for r in results) else 1)
if __name__=='__main__':main()
