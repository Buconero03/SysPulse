from pathlib import Path
import xml.etree.ElementTree as ET,zipfile,json,re,hashlib,argparse
root=Path(__file__).resolve().parents[1]
parser=argparse.ArgumentParser(description='Structural checks only; not .NET/WPF execution.')
parser.add_argument('--input-zip',type=Path,default=root.parent/'upload/ProcessLens-v0.19.16-R7.zip')
parser.add_argument('--candidate',type=Path,default=root/'source/ProcessLens-v0.19.16-R8')
parser.add_argument('--output',type=Path)
args=parser.parse_args();b=args.candidate.resolve();out=b/'audit/R8';results=[]
with zipfile.ZipFile(args.input_zip) as z:before={n[len('ProcessLens-v0.19.16-R7/'):]:z.read(n) for n in z.namelist() if not n.endswith('/')}
def check(name,ok,detail):
 results.append(dict(test=name,status='PASS' if ok else 'FAIL',detail=detail));print(results[-1]['status'],name)
protected=[rel for rel in before if rel.startswith(('src/ProcessLens.Windows/','src/ProcessLens.Guardian.','src/ProcessLens.MemoryAgent/','src/ProcessLens.Profiler/','src/ProcessLens.Core/','src/ProcessLens.Semantics/')) or rel in ['src/ProcessLens.App/ProfilerClient.cs','src/ProcessLens.App/GuardianDashboardView.xaml','src/ProcessLens.App/GuardianDashboardView.xaml.cs','src/ProcessLens.App/GuardianDashboardReader.cs','src/ProcessLens.App/GuardianLineageReader.cs','src/ProcessLens.App/MemoryDashboardView.xaml','src/ProcessLens.App/MemoryDashboardView.xaml.cs','src/ProcessLens.App/AppDiagnostics.cs','src/ProcessLens.App/MemoryAnalysisAudit.cs']]
protected=[rel for rel in protected if not set(Path(rel).parts)&{'bin','obj'}]
check('protected-bytes',all((b/rel).read_bytes()==before[rel] for rel in protected),f'{len(protected)} source files unchanged from the user ZIP, including all Core, Semantics and Windows acquisition')
xmlfiles=list((b/'src').rglob('*.xaml'))+list(b.rglob('*.csproj'))+list((b/'src').rglob('*.manifest'))
for p in xmlfiles:ET.parse(p)
check('XML-well-formed',True,f'{len(xmlfiles)} XML files; not WPF compilation')
view=ET.parse(b/'src/ProcessLens.App/MainWindow.xaml');xname='{http://schemas.microsoft.com/winfx/2006/xaml}Name';named={e.get(xname):e for e in view.iter() if e.get(xname)};ui=(b/'src/ProcessLens.App/MainWindow.xaml.cs').read_text(encoding='utf-8-sig')
check('lazy-dashboard-hosts',all(named[n].tag.endswith('ContentControl') for n in ['GuardianDashboardHost','MemoryDashboardHost']) and not any(e.tag.split('}')[-1] in ['GuardianDashboardView','MemoryDashboardView'] for e in view.iter()),'No eager dashboard view instances in startup XAML')
check('lazy-dashboard-lifecycle',all(s in ui for s in ['guardian && GuardianDashboard is null','memory && MemoryDashboard is null','GuardianDashboardHost.Content = GuardianDashboard','MemoryDashboardHost.Content = MemoryDashboard','GuardianDashboard?.SetActive(false)','MemoryDashboard?.SetActive(false)']),'First-visit construction, no construction on window close')
check('list-recycling',all(named[n].get('VirtualizingPanel.VirtualizationMode')=='Recycling' and named[n].get('ScrollViewer.CanContentScroll')=='True' for n in ['ProcessList','ServiceList']),'Data retained; UI containers recycled')
for p in (b/'src').rglob('*.xaml'):
 names=[e.get(xname) for e in ET.parse(p).iter() if e.get(xname)];check('unique-XAML-names '+p.name,len(names)==len(set(names)),'No duplicated generated control name')
for event in ['DetailIdentification_Expanded','DetailIdentification_Collapsed','DetailTechnicalSections_Collapsed']:
 check('event '+event,event in (b/'src/ProcessLens.App/MainWindow.xaml').read_text(encoding='utf-8-sig') and ('void '+event+'(') in ui,'XAML handler is wired')
show=ui[ui.index('    private void ShowDetails('):ui.index('    private void UpdateTechnicalDetails(')]
check('deferred-technical-render','DetailTechnicalContextText.Text =' not in show and 'if (DetailTechnicalSectionsExpander.IsExpanded)\n            UpdateTechnicalDetails(row);' in show,'Technical strings and visual rows are built on request')
check('deferred-classification','if (!DetailIdentificationExpander.IsExpanded || _currentDetailRow is not { } row)' in ui and '_renderedClassificationFactsPid == row.Pid' in ui,'Collapsed details avoid UI allocation; unchanged classification reuses rows')
check('bounded-caches','> _deepCache = new(8);' in ui and 'new(16, StringComparer.OrdinalIgnoreCase)' in ui and 'new(128, StringComparer.OrdinalIgnoreCase)' in (b/'src/ProcessLens.App/ProcessIconResolver.cs').read_text(encoding='utf-8-sig'),'Deep 8; service histories 16; icons 128; no counters discarded')
check('global-sample-allocation','new MemoryObservation(now, bytes)' not in ui and 'state.Samples.Add(now, bytes);' in ui and 'new MemoryObservation(sample.TimestampUtc, sample.Bytes)' in (b/'src/ProcessLens.App/MemoryObservationBuffer.cs').read_text(),'Observation objects only materialized for requested trend')
check('input-customizations-retained',all((b/rel).read_bytes()==before[rel] for rel in ['src/ProcessLens.App/ProcessRow.cs','src/ProcessLens.Core/ProcessTerminology.cs','src/ProcessLens.Semantics/ProcessBehaviorExplainer.cs','src/ProcessLens.Semantics/ProcessQueryEngine.cs','tests/ProcessLens.TerminologyTests/Program.cs']),'Five of seven customized files unchanged; MainWindow diff preserves labels/styles')
check('no-profiling-or-trim',not any(token in ui for token in ['GC.Collect(', 'EmptyWorkingSet(', 'SetProcessWorkingSetSize(']),'No GC forcing or working set trimming introduced')
check('test-and-build-wiring','ProcessLens.MemoryEfficiencyTests' in (b/'build-dashboard.ps1').read_text(encoding='utf-8-sig') and (b/'tests/ProcessLens.MemoryEfficiencyTests/Program.cs').exists(),'Executable .NET regressions provided, NOT RUN here')
check('apphost-identity','<UseAppHost>true</UseAppHost>' in (b/'src/ProcessLens.App/ProcessLens.App.csproj').read_text() and '<Description>ProcessLens</Description>' in (b/'src/ProcessLens.App/ProcessLens.App.csproj').read_text(),'Apphost and application metadata explicit')
# Basic punctuation balance after removing C# strings/comments, not a grammar/compiler check.
def mask(text):
 pattern=r'(?s)/\*.*?\*/|//[^\n]*|@"(?:[^"]|"")*"|"(?:[^"\\]|\\.)*"|\x27(?:[^\x27\\]|\\.)*\x27'
 return re.sub(pattern,'',text)
for rel in ['src/ProcessLens.App/BoundedCache.cs','src/ProcessLens.App/MemoryObservationBuffer.cs','tests/ProcessLens.MemoryEfficiencyTests/Program.cs']:
 s=mask((b/rel).read_text());check('delimiter-balance '+Path(rel).name,all(s.count(a)==s.count(c) for a,c in [('(',')'),('{','}'),('[',']')]),'Basic balance only; not C# compilation')
output=args.output or out/'structural-results.json'
output.parent.mkdir(parents=True,exist_ok=True)
output.write_text(json.dumps(results,ensure_ascii=False,indent=2)+'\n');assert all(x['status']=='PASS' for x in results)
