"""R19 source scope, formatter evidence and real PowerShell preflight. Does not execute WPF."""
import argparse,hashlib,json,re,subprocess,zipfile
from pathlib import Path
import xml.etree.ElementTree as ET
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--candidate',type=Path,required=True);p.add_argument('--baseline-zip',type=Path,required=True);p.add_argument('--pwsh',type=Path,required=True);a=p.parse_args()
r=a.candidate.resolve();audit=r/'audit/R10_R19';sha=lambda b:hashlib.sha256(b).hexdigest()
assert sha(a.baseline_zip.read_bytes())=='1e86eb08d6e4b87649a65ee69cbb1786c82933dc8ea95c7c6d9385b0d1cedee2'
with zipfile.ZipFile(a.baseline_zip) as z:old={n.split('/',1)[1]:z.read(n) for n in z.namelist() if not n.endswith('/')}
results=[]
def check(label,ok):results.append({'test':label,'status':'PASS' if ok else 'FAIL'});print(results[-1]['status'],label)
ledger=json.loads((audit/'authorized-source-edits.json').read_text())['files'];allowed={x['file'] for x in ledger}
changed={f.relative_to(r).as_posix() for folder in ['src','tests'] for f in (r/folder).rglob('*') if f.is_file() and old.get(f.relative_to(r).as_posix())!=f.read_bytes()}
check('exact seven-file source/test ledger',len(allowed)==7 and changed==allowed and all(sha((r/x['file']).read_bytes())==x['sha256_after'] and sha(old[x['file']])==x['sha256_before'] for x in ledger))
check('all baseline files retained',all((r/n).is_file() for n in old))
check('Core Windows collectors Guardian MemoryAgent and profiler unchanged',all((r/n).read_bytes()==b for n,b in old.items() if n.startswith(('src/ProcessLens.Core/','src/ProcessLens.Windows/','src/ProcessLens.Guardian','src/ProcessLens.MemoryAgent','src/ProcessLens.RamMonitor/'))))
check('standard build and corrected PowerShell gate unchanged',all((r/n).read_bytes()==old[n] for n in ['build-dashboard.ps1','tests/check-copy-layout-r18r1.ps1','tests/ProcessLens.CopyUiTests/Program.cs']))
x='{http://schemas.microsoft.com/winfx/2006/xaml}';w='{http://schemas.microsoft.com/winfx/2006/xaml/presentation}';local='{clr-namespace:ProcessLens.App}'
main=ET.parse(r/'src/ProcessLens.App/MainWindow.xaml').getroot();byname={e.get(x+'Name'):e for e in main.iter() if e.get(x+'Name')};parents={c:p for p in main.iter() for c in p}
ram=byname['SystemRamText'];cpu=byname['LogicalCpuText'];bar=parents[parents[cpu]]
check('RAM directly follows logical CPU inside responsive header',bar.tag==w+'WrapPanel' and list(bar).index(parents[ram])==list(bar).index(parents[cpu])+1)
check('RAM value selectable and initially pending',ram.tag==local+'SelectableText' and ram.get('Text')=='In attesa…' and parents[ram][0].get('Text')=='RAM sistema: ')
code=(r/'src/ProcessLens.App/MainWindow.xaml.cs').read_text(encoding='utf-8-sig');before=old['src/ProcessLens.App/MainWindow.xaml.cs'].decode('utf-8-sig')
check('same number of timers and no new process scans',code.count('new DispatcherTimer')==before.count('new DispatcherTimer') and code.count('_collector.Capture(')==before.count('_collector.Capture('))
worker=code[code.index('private async Task RefreshCpuAsync()'):code.index('    private GridLength?')]
check('RAM captured on existing worker with CPU',worker.index('SystemMemory: ReadHeaderMemory()')>worker.index('await Task.Run(') and 'UpdateSystemRam(result.SystemMemory)' in worker)
check('late RAM cannot publish after minimize or shutdown',worker.index('generation != _cpuGeneration')<worker.index('UpdateSystemRam(result.SystemMemory)'))
check('RAM read failure isolated and unavailable value clears old result','catch (Exception) { return null; }' in code and 'UpdateSystemRam(null);' in worker and 'SystemRamText.Text = "Sospeso"' in code)
read=code[code.index('private static SystemMemoryEvidence? ReadHeaderMemory()'):code.index('internal void UpdateSystemRam')]
check('header reads only small global memory counter','WindowsPhysicalMemoryReader.Read()' in read and not any(s in read for s in ['ReadFullAnalysis','GetProcesses','Task.Run','DispatcherTimer']))
check('GUI resources remain distinct','GUI ProcessLens · PID' in code and 'Solo processo GUI; Guardian e MemoryAgent esclusi' in code and 'Non è la somma dei Working Set' in code)
view=ET.parse(r/'src/ProcessLens.App/SystemProcessesView.xaml').getroot();nodes={e.get(x+'Name'):e for e in view.iter() if e.get(x+'Name')};grid=nodes['SystemProcessesTable']
check('table retains copy read-only and recycling',grid.get('IsReadOnly')=='True' and grid.get(local+'GridCopySupport.Enabled')=='True' and grid.get('VirtualizingPanel.VirtualizationMode')=='Recycling' and grid.get('EnableRowVirtualization')=='True')
check('table has horizontal rules and roomier rows',grid.get('GridLinesVisibility')=='Horizontal' and grid.get('MinRowHeight')=='40' and grid.get('AlternatingRowBackground')=='#F8FAFC')
check('instance selector and analysis action share lower panel',next(e for e in view.iter(w+'WrapPanel') if nodes['SystemInstancesBox'] in list(e)) is next(e for e in view.iter(w+'WrapPanel') if nodes['AnalyzeSystemButton'] in list(e)))
check('details selectable with larger text and splitter retained',nodes['SystemSelectionText'].tag==local+'SelectableText' and nodes['SystemSelectionText'].get('FontSize')=='13' and any(e.get('ResizeDirection')=='Rows' for e in view.iter(w+'GridSplitter')))
for path in [*r.rglob('*.xaml'),*r.rglob('*.csproj')]:ET.parse(path)
check('all XAML and project XML parses',True)
e=json.loads((audit/'presentation-evidence.json').read_text());check('86 real presentation assertions include 18 RAM regressions',e['status']=='PASS' and e['assertions']==86 and e['R19_assertions']==18 and all(sha((r/n).read_bytes())==h for n,h in e['source_hashes'].items()))
e=json.loads((audit/'wpf-typecheck-evidence.json').read_text());check('six current assemblies typecheck with zero warnings',len(e['assemblies'])==6 and all(x['status']=='PASS' and x['warning_count']==0 for x in e['assemblies']) and all(sha((r/n).read_bytes())==h for n,h in e['inputs'].items()))
e=json.loads((audit/'network-runtime-retention.json').read_text());check('332 prior network assertions retained with identical inputs',sum(x['assertions'] for x in e['outcomes'])==332 and all(sha((r/n).read_bytes())==h for n,h in e['source_hashes'].items()))
# Windows source-only preflight scripts can also be run on this Linux PowerShell host.
script=(r/'build-dashboard.ps1').read_text(encoding='utf-8-sig');gates=re.findall(r"^& \(Join-Path \$PSScriptRoot '(tests\\check-[^']+\.ps1)'\)",script,re.M);logs=[]
check('thirty preflight gates retained',len(gates)==30)
for n in gates:
 path=r/n.replace('\\','/');out=subprocess.run([str(a.pwsh.resolve()),'-NoLogo','-NoProfile','-NonInteractive','-File',str(path)],capture_output=True,text=True,timeout=45)
 logs.append(n+'\n'+out.stdout+out.stderr);check('PowerShell '+path.name,out.returncode==0)
(audit/'powershell-preflight.log').write_text('\n\n'.join(logs))
(audit/'source-results.json').write_text(json.dumps(results,indent=2)+'\n')
(audit/'preflight-evidence.json').write_text(json.dumps({'platform':'Linux','PowerShell':'7.4.21','hashes':{n.replace('\\','/'):sha((r/n.replace('\\','/')).read_bytes()) for n in gates},'Windows_PowerShell_5_1_and_WPF':'NOT EXECUTED'},indent=2)+'\n')
raise SystemExit(0 if all(x['status']=='PASS' for x in results) else 1)
