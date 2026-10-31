"""Portable structural checks only; not a C# compiler or WPF renderer."""
from pathlib import Path
import hashlib,json,xml.etree.ElementTree as ET,re,argparse
root=Path(__file__).resolve().parents[1]
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--baseline',type=Path,default=root/'source/ProcessLens-v0.19.16-R6')
parser.add_argument('--candidate',type=Path,default=root/'source/ProcessLens-v0.19.16-R7')
parser.add_argument('--output',type=Path,default=root/'audit/R7/static-results.json')
args=parser.parse_args(); a=args.baseline.resolve(); b=args.candidate.resolve()
assert a.is_dir() and b.is_dir(), 'Specificare directory R6 e R7 con --baseline e --candidate' 
results=[]
def check(name,ok,detail):
 results.append(dict(test=name,status='PASS' if ok else 'FAIL',detail=detail)); print(results[-1]['status'],name)
def protected(p):
 s=p.as_posix()
 return s.startswith(('src/ProcessLens.Windows/','src/ProcessLens.Guardian.','src/ProcessLens.MemoryAgent/','src/ProcessLens.Profiler/')) or s in ['src/ProcessLens.Core/StructuredDiagnostics.cs','src/ProcessLens.Core/ProfilerModels.cs','src/ProcessLens.Core/MemoryAlertPolicy.cs','src/ProcessLens.Core/WindowsAllocationEvidence.cs','src/ProcessLens.App/ProfilerClient.cs','src/ProcessLens.App/MemoryAnalysisAudit.cs','src/ProcessLens.App/AppDiagnostics.cs'] or 'Guardian' in p.name
protected_files=[p for p in a.rglob('*') if p.is_file() and protected(p.relative_to(a))]
check('protected-bytes',all((b/p.relative_to(a)).read_bytes()==p.read_bytes() for p in protected_files),f'{len(protected_files)} file invariati byte per byte')
xmlfiles=list(b.rglob('*.xaml'))+list(b.rglob('*.csproj'))+list(b.rglob('*.manifest'))
for p in xmlfiles:
 try:ET.parse(p)
 except ET.ParseError as e:check('XML '+str(p.relative_to(b)),False,str(e));break
else: check('XML-well-formed',True,f'{len(xmlfiles)} XML; non compilazione XAML')
for p in b.rglob('*.xaml'):
 tree=ET.parse(p); names=[e.get('{http://schemas.microsoft.com/winfx/2006/xaml}Name') for e in tree.iter() if e.get('{http://schemas.microsoft.com/winfx/2006/xaml}Name')]
 check('XAML-names '+p.name,len(names)==len(set(names)),f'{len(names)} nomi univoci; non rendering')
editorial={'label','summary','meaning','whyRunning','closeImpact','evidence'}
def predicates(v):
 if isinstance(v,list):return [predicates(x) for x in v]
 if isinstance(v,dict):return {k:predicates(x) for k,x in v.items() if k not in editorial}
 return v
for p in b.rglob('*.json'):
 if p.name in ['role-signatures.json','framework-signatures.json']:
  old=json.loads((a/p.relative_to(b)).read_text(encoding='utf-8-sig'));new=json.loads(p.read_text(encoding='utf-8-sig'))
  check('rule-contract '+p.name,predicates(old)==predicates(new),'ID, punteggi, categorie, vincoli e predicati invariati')
 else:json.loads(p.read_text(encoding='utf-8-sig'))
# Public data records and acquisition contracts: declarations untouched (the one changed EXE version is metadata).
for name in ['ProcessSnapshot','ProcessInsight','ServiceSnapshot']:
 old=(a/f'src/ProcessLens.Core/{name}.cs').read_text(encoding='utf-8-sig');new=(b/f'src/ProcessLens.Core/{name}.cs').read_text(encoding='utf-8-sig')
 decl=lambda s:re.findall(r'^\s*public [^\n=>]+(?:\{ get; init; \}|=>)',s,re.M)
 check('model-contract '+name,decl(old)==decl(new),'Dichiarazioni e campi pubblici invariati')
x=(b/'src/ProcessLens.App/MainWindow.xaml').read_text(encoding='utf-8-sig');ui=(b/'src/ProcessLens.App/MainWindow.xaml.cs').read_text(encoding='utf-8-sig');q=(b/'src/ProcessLens.Semantics/ProcessQueryEngine.cs').read_text(encoding='utf-8-sig');r=(b/'src/ProcessLens.Semantics/RoleClassifier.cs').read_text(encoding='utf-8-sig')
check('layout-constraints', 'Property="TextWrapping" Value="Wrap"' in x and x.count('MinHeight="40"')==3 and 'Header="Che cosa significa RAM privata?"' in x,'Titoli a capo e minimi nella terna; DPI NON ESEGUITO')
check('missing-CPU-presentation','p.CpuCumulativeAvailable ?' in ui and 'p.CpuSampleAvailable ? $"{p.CpuAverage' in ui,'N/D rispettato anche nel rapporto tecnico')
check('missing-health-guard','if (!p.CpuSampleAvailable && recentCpu is not { Count: > 0 })' in q,'Guardia di sola presentazione; esecuzione .NET NON ESEGUITA')
check('no-launch-causality',not any(t in r for t in ['lo ha avviato','Avviato da {p.ParentName}','Avviata da {p.ParentName}','Windows lo avvia e lo mantiene']), 'RoleClassifier: cause storiche inventate rimosse')
check('network-boundaries','non garantisce assenza' in q and 'non dimostra che questo PID gli abbia delegato' in q and 'Conteggi nelle letture disponibili, non byte trasferiti' in ui,'Verifica strutturale messaggi, non test reti Windows')
check('ETA-boundaries','non è un intervallo di confidenza' in q and 'INDICATORE TESTUALE DA VERIFICARE' in q,'Fonte testuale e scenario aritmetico qualificati')
check('memory-endpoint-limits','VARIAZIONE NETTA LIMITATA' in q and 'oscillazioni intermedie' in q,'Saldo distinto da monotonia')
check('candidate-metadata','Candidate' in (b/'VERSION.txt').read_text() and 'R7' in (b/'VERSION.txt').read_text() and '-candidate' in (b/'src/ProcessLens.App/ProcessLens.App.csproj').read_text(),'Nessuna promozione stabile')
check('verification-no-actions',all(t not in (b/'verify-candidate-r7.ps1').read_text() for t in ['Start-Process','Stop-Process','Install-Service','-RunCapture','install-memory-alerts.ps1','install-guardian.ps1']),'La procedura non avvia GUI, target profiler, terminazioni o installazioni')
args.output.parent.mkdir(parents=True,exist_ok=True)
args.output.write_text(json.dumps(results,ensure_ascii=False,indent=2)+'\n')
assert all(x['status']=='PASS' for x in results)
