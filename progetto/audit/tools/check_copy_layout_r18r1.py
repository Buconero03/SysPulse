"""Source/mutation and type-check evidence for the WPF layout fixture; does not execute WPF."""
import argparse,hashlib,json,re,zipfile
from pathlib import Path
import xml.etree.ElementTree as ET
from check_wpf_test_context_r14r3 import checks
from ux_writing_rules import approved_feature

def violations(s):
 bad=[]
 def need(ok,label):
  if not ok:bad.append(label)
 try:helper=s[s.index('    private static void DrainLayoutWork('):s.index('    [STAThread]')]
 except ValueError:return ['missing Dispatcher and layout helpers']
 need('DispatcherPriority.ContextIdle' in helper and 'DispatcherPriority.Send' in helper and 'Dispatcher.PushFrame(frame)' in helper,'native queue drained below Background with higher priority deadline')
 need('remaining <= TimeSpan.Zero' in helper and 'TimeSpan.FromSeconds(5)' in helper and 'passes < 16' in helper and 'new DispatcherTimer' in helper,'finite time and pass bounds')
 need('deadline.Stop();' in helper and 'deadline.Tick -= onDeadline;' in helper and 'sentinel.Abort();' in helper and 'finally' in helper,'timer and queued sentinel cleaned up on all exits')
 need('text.Dispatcher.VerifyAccess();' in helper and 'current == previous && lines == previousLines' in helper and 'Check(settled,' in helper,'owning dispatcher and stable size plus line count')
 need('text.Measure(new Size(width, double.PositiveInfinity));' in helper and helper.count('text.Arrange(new Rect(new Point(), text.DesiredSize));')==2 and helper.count('text.UpdateLayout();')==2,'measure arrange update performed before and after deferred work')
 need('double.IsFinite(text.DesiredSize.Height) && text.DesiredSize.Height > 20' in helper and 'text.IsMeasureValid && text.IsArrangeValid && text.LineCount >= 3' in helper and 'text.Template?.FindName("PART_ContentHost", text)' in helper,'original native-template height and line assertions unchanged')
 loop=helper[helper.index('for (;'):helper.index('string state =')]
 need('> 20' not in loop and '>= 3' not in loop,'settling does not retry until an expected height or line count happens to pass')
 need(not re.search(r'\bTemplate\s*=|\bMinHeight\s*=|\bMinLines\s*=|Thread\.Sleep|\.Show\s*\(|\.Run\s*\(',helper),'no synthetic template forced height sleeps or shown window')
 need('deferred.Dispatcher.BeginInvoke(DispatcherPriority.Background' in s and 'Size deferredSize = LayoutOutput(deferred, 210);' in s and 'backgroundRan && deferred.LineCount >= 3 && deferredSize.Height > 20' in s,'deterministic pending Background regression using actual WPF control')
 need('narrow.Height > wide.Height && text.LineCount > wideLines' in s and 'copied == "perché 🧪"' in s and 'layout and wrapping preserve original text and selection' in s,'wrap original text and Unicode selection checks retained')
 return bad

def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--candidate',type=Path,required=True);p.add_argument('--baseline-zip',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args();r=a.candidate.resolve();audit=r/'audit/R10_R18_R1';sha=lambda b:hashlib.sha256(b).hexdigest()
 assert sha(a.baseline_zip.read_bytes())=='991e0073b8d5cc47e2bcaf52ee0c968f8bf2db2402ab14b7003ecf351c132742'
 with zipfile.ZipFile(a.baseline_zip) as z:old={n.split('/',1)[1]:z.read(n) for n in z.namelist() if not n.endswith('/')}
 rows=[]
 def check(label,ok):rows.append({'test':label,'status':'PASS' if ok else 'FAIL','kind':'source/mutation/type-check correlation; not WPF execution'});print(rows[-1]['status'],label)
 ledger=json.loads((audit/'authorized-source-edits.json').read_text())['files'];allowed={x['file'] for x in ledger};test='tests/ProcessLens.CopyUiTests/Program.cs';app='src/ProcessLens.App/ProcessLens.App.csproj';s=(r/test).read_text();before=old[test].decode('utf-8-sig')
 check('all R18 files retained',all((r/n).is_file() for n in old))
 check('exact four-file source/test/pipeline ledger',allowed=={test,app,'build-dashboard.ps1','tests/check-copy-layout-r18r1.ps1'} and all((sha((r/x['file']).read_bytes())==x['sha256_after'] or (approved_feature(r,x['file'],'R10_R18_R2') or approved_feature(r,x['file'],'R10_R19'))) and (sha(old[x['file']]) if x['file'] in old else None)==x['sha256_before'] for x in ledger))
 check('production C# and XAML retained outside exact R19 header/layout hashes',all((r/n).read_bytes()==b or approved_feature(r,n,'R10_R19') for n,b in old.items() if n.startswith('src/') and n.endswith(('.cs','.xaml'))))
 check('only production project identity changed',((r/app).read_bytes()==old[app].replace(b'R18-network-recovery-candidate',b'R18-R1-wpf-layout-candidate') or (approved_feature(r,app,'R10_R18_R2') or approved_feature(r,app,'R10_R19'))))
 changed={f.relative_to(r).as_posix() for f in r.rglob('*') if f.is_file() and (f.relative_to(r).as_posix().startswith('src/') or f.relative_to(r).as_posix().startswith('tests/') and f.suffix in {'.cs','.csproj'}) and old.get(f.relative_to(r).as_posix())!=f.read_bytes()}
 check('no other production or C# test changes outside exact R19 hashes',changed-{n for n in changed if approved_feature(r,n,'R10_R19')}=={test,app}-{n for n in (test,app) if approved_feature(r,n,'R10_R19')})
 check('every existing Check assertion retained',all(c in checks(s) for c in checks(before)))
 check('all layout invariants pass',not violations(s))
 # Strip only the reviewed helper, deterministic queued fixture and initialization comment.
 old_body=before[:before.index('    private static Size LayoutOutput(')]+before[before.index('    [STAThread]'):]
 new_body=s[:s.index('    private static void DrainLayoutWork(')]+s[s.index('    [STAThread]'):]
 begin=new_body.index('        // Deterministic queue regression:');end=new_body.index('        int built = 0;',begin)
 new_body=new_body[:begin]+new_body[end:]
 new_body=new_body.replace('using System.Windows.Threading;\n','').replace('// EndInit completes initialization; LayoutOutput also drains deferred native formatting.','// EndInit resolves the native theme before Measure; no synthetic template is supplied.')
 check('all unrelated fixture code retained exactly',new_body==old_body)
 build=(r/'build-dashboard.ps1').read_text(encoding='utf-8-sig');prior=[l for l in old['build-dashboard.ps1'].decode('utf-8-sig').splitlines() if l.startswith('Invoke-CheckedDotnet ')]
 check('all Windows stages retained in order with new source gate','check-copy-layout-r18r1.ps1' in build and all(l in build for l in prior) and [build.index(l) for l in prior]==sorted(build.index(l) for l in prior))
 evidence=json.loads((audit/'runtime-evidence.json').read_text())
 check('332 retained portable assertions have unchanged current inputs',sum(x['assertions'] for x in evidence['outcomes'])==332 and 'not rerun' in evidence['retained_from'] and all(sha((r/n).read_bytes())==h for n,h in evidence['source_hashes'].items()))
 typepath=r/'audit/R10_R19/wpf-typecheck-evidence.json' if approved_feature(r,'src/ProcessLens.App/MainWindow.xaml.cs','R10_R19') else audit/'wpf-typecheck-evidence.json'
 typed=json.loads(typepath.read_text())
 check('six current C# assemblies type-check with zero warnings',len(typed['assemblies'])==6 and all(x['status']=='PASS' and x['warning_count']==0 for x in typed['assemblies']) and all(sha((r/n).read_bytes())==h for n,h in typed['inputs'].items()))
 syntax=json.loads((audit/'syntax-evidence.json').read_text());check('edited test syntax matches its actual input',syntax['status']=='PASS' and all(sha((r/n).read_bytes())==h for n,h in syntax['source_hashes'].items()))
 log=(audit/'reported-windows-r17-excerpt.log').read_text()
 check('reported R17 Windows failure retained without claiming current execution','R17-pktmon-wifi-native-candidate' in log and 'desired=210.00x17.29' in log and 'Network UI fixtures complete' in log and 'System process WPF tests complete' in log)
 for label,first,second in [
 ('too early queue priority','DispatcherPriority.ContextIdle','DispatcherPriority.Send'),
 ('unbounded passes','passes < 16','passes < int.MaxValue'),
 ('oversized time budget','TimeSpan.FromSeconds(5)','TimeSpan.FromHours(5)'),
 ('missing timer cleanup','deadline.Stop();',''),
 ('missing sentinel cleanup','sentinel.Abort();',''),
 ('fake stable state','current == previous && lines == previousLines','true'),
 ('weakened height','text.DesiredSize.Height > 20','text.DesiredSize.Height > 0'),
 ('missing original line bound','text.LineCount >= 3','text.LineCount >= 1'),
 ('changed wrapping expectation','narrow.Height > wide.Height && text.LineCount > wideLines','true'),
 ('no Background fixture','deferred.Dispatcher.BeginInvoke(DispatcherPriority.Background','deferred.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle')]:
  assert first in s;check('reject '+label,bool(violations(s.replace(first,second))))
 check('old Measure-only helper is rejected',bool(violations(before)))
 for f in [*r.rglob('*.xaml'),*r.rglob('*.csproj')]:ET.parse(f)
 check('all XAML/project XML parses',True)
 a.output.write_text(json.dumps(rows,indent=2)+'\n');raise SystemExit(0 if all(x['status']=='PASS' for x in rows) else 1)
if __name__=='__main__':main()
