"""R14 R3 source/retention checks. Does not execute or compile WPF."""
import argparse,hashlib,json,re,zipfile
from pathlib import Path
import xml.etree.ElementTree as ET
from test_clipboard_calls_r10r9 import mask_non_code
from ux_writing_rules import approved_feature

SETUP='SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));'
WAIT='Task continuation = VerifyDispatcherContinuationAsync(completion.Task, dispatcher, ownerThread);'
QUEUE='ThreadPool.QueueUserWorkItem('
RESTORE='SynchronizationContext.SetSynchronizationContext(previousContext);'
def valid_context(s):
    main=s[s.index('public static void Main()'):]
    return (0 <= main.find(SETUP) < main.find(WAIT) < main.find(QUEUE) < main.find('Complete(continuation);') and
        RESTORE in main and 'finally' in main and 'var previousContext = SynchronizationContext.Current;' in main and
        'TaskCreationOptions.RunContinuationsAsynchronously' in main and
        'int workerThread = await completion;' in s and 'workerThread != ownerThread' in s and
        'dispatcher.CheckAccess() && Environment.CurrentManagedThreadId == ownerThread' in s and
        'SynchronizationContext.Current is DispatcherSynchronizationContext' in s)
def checks(s):
    masked=mask_non_code(s);result=[]
    for m in re.finditer(r'\bCheck\(',masked):
        start=m.end();depth=1;end=start
        while depth and end<len(masked):
            if masked[end]=='(':depth+=1
            elif masked[end]==')':depth-=1
            end+=1
        result.append(re.sub(r'\s+','',s[start:end-1]))
    return result

def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--candidate',type=Path,required=True);p.add_argument('--baseline-zip',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args();root=a.candidate.resolve()
    sha=lambda b:hashlib.sha256(b).hexdigest()
    assert sha(a.baseline_zip.read_bytes())=='cfc9b8bbea861833efd74defc7efeae76d1c649258b095ce371b8e239a085d30'
    with zipfile.ZipFile(a.baseline_zip) as z:old={n.split('/',1)[1]:z.read(n) for n in z.namelist() if not n.endswith('/')}
    results=[]
    def check(label,ok):
        results.append({'test':label,'status':'PASS' if ok else 'FAIL','kind':'source/XML/mutation only; no WPF execution'});print(results[-1]['status'],label)
    app='src/ProcessLens.App/ProcessLens.App.csproj';tests=['tests/ProcessLens.'+n+'/Program.cs' for n in ['NetworkUiTests','SystemProcessUiTests']]
    check('no baseline files removed',all((root/n).is_file() for n in old))
    check('R2 production retained outside exact authorized R15/R16 delta',all((root/n).read_bytes()==data or any(approved_feature(root,n,revision) for revision in ('R10_R15','R10_R16','R10_R17','R10_R18','R10_R18_R1','R10_R18_R2','R10_R19')) for n,data in old.items() if n.startswith('src/') and n.endswith(('.cs','.xaml'))))
    changed={n for n,data in old.items() if n.startswith('src/') and (root/n).read_bytes()!=data}
    check('R3 production delta or exact authorized R15/R16 presentation',(changed - {n for n in changed if any(approved_feature(root,n,revision) for revision in ('R10_R15','R10_R16','R10_R17','R10_R18','R10_R18_R1','R10_R18_R2','R10_R19'))} == {app} - {n for n in (app,) if any(approved_feature(root,n,revision) for revision in ('R10_R15','R10_R16','R10_R17','R10_R18','R10_R18_R1','R10_R18_R2','R10_R19'))}) and ((root/app).read_bytes()==old[app].replace(b'R14-R2-network-tab-init-candidate',b'R14-R3-wpf-test-context-candidate') or any(approved_feature(root,app,revision) for revision in ('R10_R15','R10_R16','R10_R17','R10_R18','R10_R18_R1','R10_R18_R2','R10_R19'))))
    check('only two original WPF fixtures changed outside exact R18 R1 copy-layout fix', {n for n,data in old.items() if n.startswith('tests/') and n.endswith('.cs') and (root/n).read_bytes()!=data and not (approved_feature(root,n,'R10_R18_R1') or (approved_feature(root,n,'R10_R18_R2') or approved_feature(root,n,'R10_R19')))}==set(tests)-{n for n in tests if approved_feature(root,n,'R10_R19')})
    for n in tests:
        s=(root/n).read_text();before=old[n].decode('utf-8-sig');label=n.split('/')[1]
        check(label+' installs context before pending await and worker completion',valid_context(s))
        check(label+' retains original checks or exact authorized R15/R16 replacement suite',all(c in checks(s) for c in checks(before)) or any(approved_feature(root,n,revision) for revision in ('R10_R15','R10_R16','R10_R17','R10_R18','R10_R18_R1','R10_R18_R2','R10_R19')))
        check(label+' retains original dispatcher pump and exception propagation',all(token in s for token in ['Dispatcher.PushFrame(frame)','task.GetAwaiter().GetResult();','TaskScheduler.Default','[STAThread]']))
        check(label+' does not suppress thread ownership checks or awaiting context',not any(token in s for token in ['ConfigureAwait(false)','DisableProcessing','catch (InvalidOperationException','catch {']))
        for name,mutant in [
            ('missing setup',s.replace(SETUP,'')),
            ('setup after asynchronous start',s.replace(SETUP,'').replace(WAIT,WAIT+'\n'+SETUP)),
            ('worker scheduled before await',s.replace(WAIT,'').replace('Complete(continuation);',WAIT+'\nComplete(continuation);')),
            ('missing restoration',s.replace(RESTORE,'')),
            ('missing owner verification',s.replace('dispatcher.CheckAccess() && Environment.CurrentManagedThreadId == ownerThread','true'))]:
            check(label+' guard rejects '+name,not valid_context(mutant))
    ledger=json.loads((root/'audit/R10_R14_R3/authorized-source-edits.json').read_text())
    check('R3 baseline hashes and current hashes or exact authorized R15/R16 successors',len(ledger['files'])==3 and {r['file'] for r in ledger['files']}=={app,*tests} and all(sha(old[r['file']])==r['sha256_before'] and (sha((root/r['file']).read_bytes())==r['sha256_after'] or any(approved_feature(root,r['file'],revision) for revision in ('R10_R15','R10_R16','R10_R17','R10_R18','R10_R18_R1','R10_R18_R2','R10_R19'))) for r in ledger['files']))
    gate=(root/'tests/check-wpf-test-context-r14r3.ps1').read_text();build=(root/'build-dashboard.ps1').read_text(encoding='utf-8-sig')
    check('standard Windows pipeline includes source gate',"tests\\check-wpf-test-context-r14r3.ps1" in build and all(t in gate for t in ['NetworkUiTests','SystemProcessUiTests','IndexOf($setup)','IndexOf($firstAwait)',RESTORE]))
    for path in [*root.rglob('*.csproj'),*root.rglob('*.xaml')]:ET.parse(path)
    check('all XAML and project XML parse',True)
    log=(root/'audit/R10_R14_R3/reported-windows-build-excerpt.log').read_text()
    check('reported R2 error retained separately from current source checks',all(t in log for t in ['UpdateButtons','RefreshAsync','InvalidOperationException','PASS network UI: R14 R2','not locally executed']))
    a.output.parent.mkdir(exist_ok=True,parents=True);a.output.write_text(json.dumps(results,indent=2)+'\n');raise SystemExit(0 if all(r['status']=='PASS' for r in results) else 1)
if __name__=='__main__':main()
