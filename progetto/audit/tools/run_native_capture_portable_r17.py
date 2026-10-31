"""Direct Roslyn/.NET portable qualification for R17 on this host; no MSBuild or WPF."""
import argparse,hashlib,json,subprocess
from pathlib import Path
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--candidate',type=Path,required=True);p.add_argument('--sdk',type=Path,required=True);p.add_argument('--work',type=Path,required=True);a=p.parse_args()
r=a.candidate.resolve();sdk=a.sdk.resolve();work=a.work.resolve();work.mkdir(parents=True,exist_ok=True);audit=r/'audit/R10_R17';audit.mkdir(exist_ok=True)
refs=sorted((sdk/'packs/Microsoft.NETCore.App.Ref/8.0.32/ref/net8.0').glob('*.dll'))
globals=work/'GlobalUsings.cs';globals.write_text(''.join('global using '+s+';\n' for s in ['System','System.Collections.Generic','System.IO','System.Linq','System.Net.Http','System.Threading','System.Threading.Tasks']))
source_hashes={};outcomes=[]
def compile(name,sources,library=False,extra=()):
 for f in sources:source_hashes[f.relative_to(r).as_posix()]=hashlib.sha256(f.read_bytes()).hexdigest()
 args=[str(sdk/'dotnet'),str(sdk/'sdk/8.0.426/Roslyn/bincore/csc.dll'),'-nologo','-nullable:enable','-langversion:12','-deterministic+','-target:'+('library' if library else 'exe'),'-out:'+str(work/(name+'.dll'))]
 args+=['-r:'+str(f) for f in [*refs,*extra]]+[str(globals)]+[str(f) for f in sources]
 result=subprocess.run(args,capture_output=True,text=True);(audit/(name+'-build.log')).write_text(result.stdout+result.stderr)
 if result.returncode:raise SystemExit(result.stdout+result.stderr)
 if not library:(work/(name+'.runtimeconfig.json')).write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':'8.0.32'}}}))
def run(name):
 result=subprocess.run([str(sdk/'dotnet'),str(work/(name+'.dll'))],capture_output=True,text=True,timeout=45)
 log=result.stdout+result.stderr;(audit/(name+'-run.log')).write_text(log)
 if result.returncode:raise SystemExit(log)
 outcome={'suite':name,'status':'PASS','assertions':sum(l.startswith('PASS ') for l in log.splitlines()),'skipped':[l for l in log.splitlines() if l.startswith('SKIP ')]};outcomes.append(outcome);print(json.dumps(outcome))
compile('ProcessLens.Core',sorted((r/'src/ProcessLens.Core').glob('*.cs')),True)
for suite,extra_sources in [('NativeCaptureTests',[r/'src/ProcessLens.Windows/WindowsNativeCapture.cs']),('NativeNetworkTests',[r/'src/ProcessLens.Windows/WindowsNativeNetworkEvidence.cs']),('WindowsComponentTests',[]),('SystemProcessTests',[r/'src/ProcessLens.Windows/WindowsSystemProcessSource.cs']),('NetworkDeviceTests',[r/'src/ProcessLens.Windows/WindowsNetworkDevices.cs'])]:
 name='ProcessLens.'+suite
 compile(name,[r/'tests'/name/'Program.cs',*extra_sources],extra=[work/'ProcessLens.Core.dll']);run(name)
evidence={'environment':'Linux x64; Roslyn SDK 8.0.426 and .NET 8.0.32 direct compilation; not MSBuild/WPF','outcomes':outcomes,'source_hashes':source_hashes,'Windows_WPF_PowerShell_DPI_and_live_RAM':'NOT EXECUTED'}
(audit/'runtime-evidence.json').write_text(json.dumps(evidence,indent=2)+'\n')
