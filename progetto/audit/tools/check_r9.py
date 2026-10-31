"""Portable source/markup checks. Does not compile or run .NET, PowerShell or WPF."""
from pathlib import Path
import argparse, hashlib, json, re, zipfile
import xml.etree.ElementTree as ET

p = argparse.ArgumentParser(description=__doc__)
p.add_argument('--baseline-zip', type=Path, required=True)
p.add_argument('--candidate', type=Path, default=Path('.'))
p.add_argument('--output', type=Path)
a = p.parse_args(); root = a.candidate.resolve(); results = []
def check(name, condition, details):
    results.append(dict(test=name, status='PASS' if condition else 'FAIL', detail=details))
    print(results[-1]['status'], name)
with zipfile.ZipFile(a.baseline_zip) as archive:
    prefix = 'ProcessLens-v0.19.16-R8/'
    old = {n[len(prefix):]:archive.read(n) for n in archive.namelist() if n.startswith(prefix) and not n.endswith('/')}
check('baseline identity', len(old) == 322, 'Delivered R8 source archive, not the earlier R7')
allowed = {'src/ProcessLens.App/MainWindow.xaml','src/ProcessLens.App/MainWindow.xaml.cs',
    'src/ProcessLens.App/ProcessRow.cs','src/ProcessLens.App/ProcessLens.App.csproj','build-dashboard.ps1','README.md','start-processlens-all.ps1'}
changed_old = {rel for rel,data in old.items() if not (root/rel).exists() or (root/rel).read_bytes() != data}
check('bounded existing-file changes', changed_old <= allowed, ', '.join(sorted(changed_old)))
protected = [rel for rel in old if rel.startswith(('src/ProcessLens.Core/', 'src/ProcessLens.Semantics/',
    'src/ProcessLens.Windows/', 'src/ProcessLens.Guardian.', 'src/ProcessLens.MemoryAgent/', 'src/ProcessLens.Profiler/', 'knowledge/'))
    or rel in {'src/ProcessLens.App/ProfilerClient.cs','src/ProcessLens.App/AppDiagnostics.cs','src/ProcessLens.App/MemoryAnalysisAudit.cs'}
    or ('src/ProcessLens.App/' in rel and ('Guardian' in rel or 'MemoryDashboard' in rel))]
check('protected source and rules identical', all((root/r).read_bytes()==old[r] for r in protected), str(len(protected))+' unchanged files')
ram = ['src/ProcessLens.App/BoundedCache.cs','src/ProcessLens.App/MemoryObservationBuffer.cs',
    'src/ProcessLens.App/ProcessIconResolver.cs','tests/ProcessLens.MemoryEfficiencyTests/Program.cs','tests/check-memory-efficiency-r8.ps1']
check('R8 RAM implementation retained', all((root/r).read_bytes()==old[r] for r in ram), 'Cache, history buffer, icons and R8 regressions unchanged')
xmlpaths = sorted(list(root.rglob('*.xaml'))+list(root.rglob('*.csproj'))+list(root.rglob('*.manifest')))
for path in xmlpaths: ET.parse(path)
check('XML well-formed', True, str(len(xmlpaths))+' markup/project files; not a WPF compile')
ns = '{http://schemas.microsoft.com/winfx/2006/xaml}'; local=lambda e:e.tag.split('}')[-1]
view = ET.parse(root/'src/ProcessLens.App/MainWindow.xaml'); named={e.get(ns+'Name'):e for e in view.iter() if e.get(ns+'Name')}
for path in root.rglob('*.xaml'):
    names=[e.get(ns+'Name') for e in ET.parse(path).iter() if e.get(ns+'Name')]
    check('unique names '+path.name, len(names)==len(set(names)), 'No duplicated generated control names')
styles={e.get(ns+'Key'):e for e in view.iter() if local(e)=='Style'}
style=styles['ActionButtonStyle']; setters={e.get('Property'):e for e in style if local(e)=='Setter'}
check('action label wrapping and spacing', any(local(e)=='TextBlock' and e.get('TextWrapping')=='Wrap' and e.get('LineHeight')=='20' for e in style.iter())
    and setters['MinHeight'].get('Value')=='36' and setters['Padding'].get('Value')=='12,8', 'Native button template, wrapped content, no fixed height')
buttons=[e for e in view.iter() if local(e)=='Button']
check('all main-window buttons styled', all(e.get('Style') in ['{StaticResource FilterButtonStyle}','{StaticResource ActionButtonStyle}'] and e.get('Height') is None for e in buttons), str(len(buttons))+' buttons')
check('quick analysis header wrapping', any(local(e)=='MenuItem.HeaderTemplate' for e in named['QuickQuestionRoot']) and
    any(local(e)=='TextBlock' and e.get('TextWrapping')=='Wrap' for e in named['QuickQuestionRoot'].iter()), 'Header text is templated; existing hover/click events retained')
metrics=['DetailCpuAverageText','DetailCpuPeakText','DetailCpuText','DetailMemoryText','DetailPrivateWorkingSetText','DetailWorkingSetText','DetailPrivateCommitText','CpuGraphScaleText']
check('metric cells wrap', all(named[n].get('TextWrapping')=='Wrap' for n in metrics), 'No text can draw into an adjacent metric cell')
check('network line spacing', named['DetailNetworkStateText'].get('TextWrapping')=='Wrap' and named['DetailNetworkStateText'].get('LineHeight')=='21', 'Multiline text has explicit line pitch')
check('filter groups wrap', all(local(named[n])=='WrapPanel' for n in ['ProcessFilters','ServiceFilters']), 'Wrapping filter groups at reduced width')
check('DPI rounding', view.getroot().get('UseLayoutRounding')=='True' and view.getroot().get('SnapsToDevicePixels')=='True', 'Layout rounding, no Windows DPI setting changed')
ui=(root/'src/ProcessLens.App/MainWindow.xaml.cs').read_text(encoding='utf-8-sig')
fmt=(root/'src/ProcessLens.App/ProcessDisplayFormatter.cs').read_text()
network=(root/'src/ProcessLens.App/ProcessNetworkPresentation.cs').read_text()
check('CPU formatting connected and guarded', 'TimeSpan.FromSeconds(p.CpuCumulativeSeconds):c' not in ui and
    'p.CpuCumulativeAvailable\n            ? $"Tempo CPU accumulato: {ProcessDisplayFormatter.CpuDuration(p.CpuCumulativeSeconds)}.' in ui and
    'p.CpuSampleAvailable ? ProcessDisplayFormatter.Percent(p.CpuPercent)' in ui, 'Availability still required; no default zero substituted')
check('raw CPU counter preserved', 'p.CpuCumulativeSeconds:G17' in ui and 'distinta dal tempo trascorso' in ui and 'DetailCpuTotalTimeText.ToolTip = null;' in ui, 'Exact round-trip numeric representation in tooltip; stale tooltip cleared')
check('culture and bounded duration precision', 'CultureInfo.CurrentCulture' in fmt and '"0.###"' in fmt and '"\\u00a0"' in fmt and 'MidpointRounding.AwayFromZero' in fmt, 'Culture-aware formatting, millisecond rounding, nonbreaking space before unit')
check('unknown invalid duration remains unknown', 'double.IsFinite(seconds)' in fmt and 'seconds < 0' in fmt and 'seconds < 0.001' in fmt, 'Invalid readings N/D; positive sub-millisecond is not zero')
check('network formatting connected', 'ProcessNetworkPresentation.DescribeEndpointOwners(p)' in ui and 'private static string BuildNetworkSummary' not in ui, 'One formatter used by UI and linked regression suite')
check('network multiline and acquisition limits', 'string.Join("\\n\\n", sections)' in network and 'if (!p.IsDeepInspected)' in network and 'p.DeepInspectionNote' in network and 'non sono misurati' in network, 'Direct PID, group, role and acquisition note remain distinct')
check('all XAML handlers remain declared', all(re.search(r'\b'+re.escape(value)+r'\s*\(',ui) for e in view.iter() for attr,value in e.attrib.items()
    if attr in {'Click','Expanded','Collapsed','MouseEnter','SelectionChanged','KeyDown','SizeChanged','TextChanged','ContextMenuOpening'}), 'Method declaration/name existence only; not a compiler check')
check('R8 lazy lifecycle and recycling retained', all(t in ui for t in ['guardian && GuardianDashboard is null','memory && MemoryDashboard is null','if (DetailTechnicalSectionsExpander.IsExpanded)', 'state.Samples.Add(now, bytes);']) and
    all(named[n].get('VirtualizingPanel.VirtualizationMode')=='Recycling' for n in ['ProcessList','ServiceList']), 'R8 memory changes preserved in modified UI file')
row=(root/'src/ProcessLens.App/ProcessRow.cs').read_bytes()
check('user process-row customizations retained', row==old['src/ProcessLens.App/ProcessRow.cs'].replace(b'Process.CpuSampleAvailable ? $"{Process.CpuPercent:F1}%" : "N/D"',b'Process.CpuSampleAvailable ? ProcessDisplayFormatter.Percent(Process.CpuPercent) : "N/D"'), 'Only percentage display expression changed in ProcessRow')
build=(root/'build-dashboard.ps1').read_text(encoding='utf-8-sig');test=(root/'tests/ProcessLens.PresentationTests/Program.cs').read_text()
check('new and prior tests wired', 'Tests.ProcessLens.PresentationTests' in build and 'check-ui-format-r9.ps1' in build and 'Tests.ProcessLens.MemoryEfficiencyTests' in build and all(t in test for t in ['it-IT','en-US','636.0625','59.9996','86399.9996','AccessDenied']), 'Regression source supplied; .NET/PowerShell execution NOT performed')
output=a.output or root/'audit/R9/structural-results.json';output.parent.mkdir(parents=True,exist_ok=True)
output.write_text(json.dumps(results,ensure_ascii=False,indent=2)+'\n')
raise SystemExit(0 if all(r['status']=='PASS' for r in results) else 1)
