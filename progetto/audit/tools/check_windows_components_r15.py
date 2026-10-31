"""R15 source/XML and retained real portable results; does not qualify WPF."""
import argparse,hashlib,json,zipfile
from pathlib import Path
import xml.etree.ElementTree as ET
from check_system_processes_r14 import violations
from ux_writing_rules import approved_feature
from check_wpf_test_context_r14r3 import valid_context

def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--candidate',type=Path,required=True);p.add_argument('--baseline-zip',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args();r=a.candidate.resolve();sha=lambda b:hashlib.sha256(b).hexdigest()
    assert sha(a.baseline_zip.read_bytes())=='ca36522237f953f5dee8bc51822c8fab2374a76ed366a146a280ed32c780b696'
    with zipfile.ZipFile(a.baseline_zip) as z:old={n.split('/',1)[1]:z.read(n) for n in z.namelist() if not n.endswith('/')}
    result=[]
    def check(label,ok):result.append({'test':label,'status':'PASS' if ok else 'FAIL','kind':'source/XML and actual portable result correlation; not WPF runtime'});print(result[-1]['status'],label)
    ledger=json.loads((r/'audit/R10_R15/authorized-source-edits.json').read_text())['files'];allowed={x['file'] for x in ledger}
    check('R15 exact ledger retained or exact authorized R16 successor',all((r/n).is_file() for n in old) and all((sha((r/x['file']).read_bytes())==x['sha256_after'] or any(approved_feature(r,x['file'],revision17) for revision17 in ('R10_R16','R10_R17','R10_R18','R10_R18_R1','R10_R18_R2','R10_R19'))) and (sha(old[x['file']]) if x['file'] in old else None)==x['sha256_before'] for x in ledger))
    current={f.relative_to(r).as_posix():f.read_bytes() for f in r.rglob('*') if f.is_file()}
    check('no production or C# test delta outside exact R15/R16 ledgers',all(n in allowed or any(approved_feature(r,n,revision17) for revision17 in ('R10_R16','R10_R17','R10_R18','R10_R18_R1','R10_R18_R2','R10_R19')) for n,b in current.items() if (n.startswith('src/') or (n.startswith('tests/') and n.endswith(('.cs','.csproj')))) and old.get(n)!=b))
    check('R3 sources retained outside exact R15/R16 ledgers',all(current[n]==b or any(approved_feature(r,n,revision17) for revision17 in ('R10_R16','R10_R17','R10_R18','R10_R18_R1','R10_R18_R2','R10_R19')) for n,b in old.items() if n.startswith('src/') and n not in allowed))
    files={n:b.decode('utf-8-sig') for n,b in current.items() if n.endswith(('.cs','.xaml','.ps1'))}
    check('existing native lifetime navigation and observation guards pass',not violations(files))
    u=files['src/ProcessLens.App/SystemProcessesView.xaml.cs'];core=files['src/ProcessLens.Core/WindowsComponentCatalog.cs'];test=files['tests/ProcessLens.SystemProcessUiTests/Program.cs']
    check('native result trimmed on worker before publication', 'Task.Run(() => WindowsComponentCatalog.Retain(_source.Capture(operation.Token), _windowsDirectory)' in u and '_catalog = result;' in u)
    check('only component groups assigned to system table','SystemProcessesTable.ItemsSource = visible;' in u and 'WindowsComponentCatalog.Groups(c.Processes' in u and 'WindowsGroup(' not in u)
    check('groups keep individual identity navigation','SystemInstancesBox.SelectedItem is SystemProcessRow { Entry.StartUtc: not null }' in u and 'AnalysisRequested?.Invoke(row.Entry)' in u)
    check('hidden workspace releases catalog groups and instance selector',all(t in u for t in ['_catalog = null','SystemProcessesTable.ItemsSource = null;','SystemInstancesBox.ItemsSource = null;','_operation?.Cancel()']))
    check('no new timer child process profiler or live event collector',not any(t in core+u for t in ['DispatcherTimer','Process.Start(','.Kill(','EventLogWatcher','TraceEventSession','GetProcesses(']))
    check('RAM is a maximum over available counters without a group sum','DefaultIfEmpty(null).Max()' in core and '.Sum(' not in core and 'non la somma' in core and 'MemoryCount' in core)
    check('observed sample and catalog function explicitly distinct',all(t in core for t in ['Funzione da catalogo; operazione corrente non misurata','non registra eventi','origine non verificata','firma digitale']))
    x='{http://schemas.microsoft.com/winfx/2006/xaml}';w='{http://schemas.microsoft.com/winfx/2006/xaml/presentation}';tree=ET.fromstring(files['src/ProcessLens.App/SystemProcessesView.xaml'])
    scope=next(e for e in tree.iter(w+'ComboBox') if e.get(x+'Name')=='SystemScopeBox')
    check('scope has only all-components and the five categories',[e.get('Content') for e in scope]==['Tutti i componenti','Kernel e memoria','Avvio e sessioni','Servizi','Sicurezza','Desktop e console'])
    check('details have a row splitter and no fixed maximum height',len(list(tree.iter(w+'GridSplitter')))==1 and 'MaxHeight="140"' not in files['src/ProcessLens.App/SystemProcessesView.xaml'])
    check('all UI handlers exist and names unique',len([e for e in tree.iter() if e.get(x+'Name')])==len({e.get(x+'Name') for e in tree.iter() if e.get(x+'Name')}) and all(v+'(' in u for e in tree.iter() for k,v in e.attrib.items() if k in ['Click','SelectionChanged','TextChanged']))
    check('process counters hidden only while system pane active','ProcessDetectedCounter.Visibility = system ? Visibility.Collapsed : Visibility.Visible;' in files['src/ProcessLens.App/MainWindow.xaml.cs'] and 'ProcessVisibleCounter.Visibility = system ? Visibility.Collapsed : Visibility.Visible;' in files['src/ProcessLens.App/MainWindow.xaml.cs'])
    check('WPF suite retains dispatcher context forced worker and finally restoration',valid_context(test))
    check('WPF regressions supplied for groups apps categories selector and late completion',all(t in test for t in ['grid.Items.Count == 3','scope.Items.Count == 6','scope.SelectedIndex = 3','Count == 2','request?.Pid == 112','source.Release.Set()','synthetic failure','ProcessDetectedCounter']))
    build=files['build-dashboard.ps1']
    check('portable suite and source gate run in normal Windows pipeline','Tests.ProcessLens.WindowsComponentTests' in build and 'check-windows-components-r15.ps1' in build)
    evidence=json.loads((r/'audit/R10_R15/runtime-evidence.json').read_text())
    check('real portable compiler and runner evidence matches all compiled sources',all(sha((r/n).read_bytes())==v for n,v in evidence['source_hashes'].items()))
    for suite,count in [('WindowsComponentTests',58),('SystemProcessTests',22),('NetworkDeviceTests',81)]:
        log=(r/('audit/R10_R15/ProcessLens.'+suite+'-run.log')).read_text();row=next(x for x in evidence['outcomes'] if x['suite']=='ProcessLens.'+suite)
        check('actual portable '+suite+' assertions and build logs',row['status']=='PASS' and row['assertions']==count and sum(l.startswith('PASS ') for l in log.splitlines())==count and (r/('audit/R10_R15/ProcessLens.'+suite+'-build.log')).read_text()=='')
    syntax=json.loads((r/'audit/R10_R15/syntax-evidence.json').read_text())
    current_types=json.loads((r/'audit/R10_R19/wpf-typecheck-evidence.json').read_text()) if approved_feature(r,'src/ProcessLens.App/MainWindow.xaml.cs','R10_R19') else {'inputs':{}}
    check('C# syntax or fresh type checking matches exact reviewed inputs',syntax['status']=='PASS' and len(syntax['source_hashes'])==5 and all(sha((r/n).read_bytes())==h or (approved_feature(r,n,'R10_R19') and current_types['inputs'].get(n)==sha((r/n).read_bytes())) for n,h in syntax['source_hashes'].items()))
    check('all XML projects and markup parse',all(ET.fromstring(b) is not None for n,b in current.items() if n.endswith(('.xaml','.csproj'))))
    a.output.parent.mkdir(exist_ok=True,parents=True);a.output.write_text(json.dumps(result,indent=2)+'\n');raise SystemExit(0 if all(x['status']=='PASS' for x in result) else 1)
if __name__=='__main__':main()
