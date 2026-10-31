"""Source guard mutation tests only. Never compiles or runs the C# algorithms."""
from pathlib import Path
import argparse,json
from live_resource_rules import violations
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--candidate',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args()
files={f.relative_to(a.candidate).as_posix():f.read_text(encoding='utf-8-sig') for f in a.candidate.rglob('*') if f.suffix in ['.cs','.xaml','.ps1']}
results=[]
def check(name, ok):
 results.append(dict(test=name,status='PASS' if ok else 'FAIL',scope='Python source guard, not C# execution'));print(results[-1]['status'],name)
check('candidate satisfies all resource guards',not violations(files))
mutations=[
 ('hidden only restore route','src/ProcessLens.App/WindowLifetimePolicy.cs','exitRequested ? WindowCloseAction.Exit : WindowCloseAction.MinimizeToTaskbar','exitRequested ? WindowCloseAction.Exit : WindowCloseAction.HideToTray'),
 ('lost icon detection removed','src/ProcessLens.App/TrayWindowController.cs','Shell_NotifyIconW(1, ref existing)','NoIconCheck()'),
 ('CPU tick becomes busy loop','src/ProcessLens.Core/MonitorRefreshPolicy.cs','LiveMetricsInterval = TimeSpan.FromSeconds(1)','LiveMetricsInterval = TimeSpan.FromMilliseconds(1)'),
 ('stale generation accepted','src/ProcessLens.App/MainWindow.xaml.cs','generation != _cpuGeneration','false'),
 ('handle not released','src/ProcessLens.Windows/WindowsLiveCpuSampler.cs','NativeMethods.CloseHandle(handle);','KeepHandle(handle);'),
 ('partial processor group labelled total','src/ProcessLens.Windows/WindowsLiveCpuSampler.cs','cpus is > 0 and <= 64','cpus > 0'),
 ('creation verification removed','src/ProcessLens.Core/LiveCpuSample.cs','old.Value.Start == startUtc','true'),
 ('long pause accepted','src/ProcessLens.Core/LiveCpuSample.cs','MaximumGapSeconds = 6','MaximumGapSeconds = 600'),
 ('unbounded metadata retained','src/ProcessLens.Core/BoundedMetadataCache.cs','_entries.Count == _capacity','false'),
 ('EX2 prefix sentinel removed','src/ProcessLens.Windows/WindowsSelfResources.cs','PrivateWorkingSet = nuint.MaxValue','PrivateWorkingSet = 0'),
 ('passive report opens all access','measure-processlens-resources.ps1','OpenProcess(0x1000','OpenProcess(0x1fffff'),
 ('forced garbage collection introduced','src/ProcessLens.Windows/WindowsSelfResources.cs','using System.Diagnostics;','using System.Diagnostics; // GC.Collect()'),
 ('CPU tests skipped','build-dashboard.ps1','Tests.ProcessLens.CpuTests','Tests.SkippedCpuTests'),
]
for name,path,before,after in mutations:
 assert before in files[path],(name,before)
 changed=dict(files);changed[path]=changed[path].replace(before,after)
 check('reject '+name,bool(violations(changed)))
a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(results,indent=2)+'\n')
raise SystemExit(0 if all(x['status']=='PASS' for x in results) else 1)
