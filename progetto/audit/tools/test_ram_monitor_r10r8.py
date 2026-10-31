from pathlib import Path
import argparse,json
from ram_monitor_rules import violations
p=argparse.ArgumentParser(description='Source-guard mutations only; not C# runtime');p.add_argument('--candidate',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args()
files={p.relative_to(a.candidate).as_posix():p.read_text(encoding='utf-8-sig') for p in a.candidate.rglob('*') if p.suffix in ['.cs','.ps1','.xaml']}
r=[]
def check(name,ok):r.append(dict(test=name,status='PASS' if ok else 'FAIL',scope='Source only, no C# execution'));print(r[-1]['status'],name)
check('candidate satisfies monitor and navigation source guards',not violations(files))
mutations=[
 ('partial becomes total','src/ProcessLens.RamMonitor/Measurement.cs','complete ? observed : null','observed'),
 ('snapshot parent accepted alone','src/ProcessLens.RamMonitor/Measurement.cs','child.HandleParentPid == parent.Pid','true'),
 ('PID reuse allowed','src/ProcessLens.RamMonitor/NativeSampler.cs','previous.StartUtcTicks == reading.StartUtcTicks','true'),
 ('root name substitutes path','src/ProcessLens.RamMonitor/NativeSampler.cs','_scope.Contains(reading.Path)','true'),
 ('full process access','src/ProcessLens.RamMonitor/NativeSampler.cs','OpenProcess(0x1000','OpenProcess(0x1fffff'),
 ('EX prefix becomes private RAM','src/ProcessLens.RamMonitor/NativeSampler.cs','memory && ex2','memory'),
 ('sampling history accumulates','src/ProcessLens.RamMonitor/LogSession.cs','Flush(); SaveSummary','NoFlush(); SaveSummary'),
 ('RAM category truncated before filter','src/ProcessLens.App/MemoryDashboardView.xaml.cs','ReadFullAnalysis(int.MaxValue)','ReadFullAnalysis(80)'),
 ('substring PID service scope','src/ProcessLens.App/ServiceNavigation.cs','scope.ContainsKey(pid)','true'),
 ('new PID born after RAM frame accepted','src/ProcessLens.App/ServiceNavigation.cs','host.CurrentStartUtc!.Value <= target.FrameStartedUtc.Value','true'),
 ('stale navigation result accepted','src/ProcessLens.App/MainWindow.xaml.cs','request != _workspaceNavigation','false'),
 ('service details could stop service','src/ProcessLens.App/ServiceDetailsWindow.cs','Title =','// StopService()\n        Title ='),
]
for name,path,before,after in mutations:
 assert before in files[path],(name,before)
 changed=dict(files);changed[path]=changed[path].replace(before,after)
 check('reject '+name,bool(violations(changed)))
a.output.parent.mkdir(exist_ok=True,parents=True);a.output.write_text(json.dumps(r,indent=2)+'\n')
raise SystemExit(0 if all(x['status']=='PASS' for x in r) else 1)
