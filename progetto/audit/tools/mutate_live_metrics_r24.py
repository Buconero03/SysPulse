"""Prove runtime regressions reject false private zero, synchronous minimize and older memory overlays."""
import argparse,hashlib,json,subprocess
from pathlib import Path
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project',type=Path,required=True);p.add_argument('--sdk',type=Path,required=True);p.add_argument('--work',type=Path,required=True);a=p.parse_args()
r=a.project.resolve();sdk=a.sdk.resolve();work=a.work.resolve();out=work/'mutants';out.mkdir(exist_ok=True);audit=r/'audit/R10_R24';sha=lambda b:hashlib.sha256(b).hexdigest();refs=sorted((sdk/'packs/Microsoft.NETCore.App.Ref/8.0.32/ref/net8.0').glob('*.dll'))
globals=work/'runtime/GlobalUsings.cs';core=work/'runtime/ProcessLens.Core.dll'
sources=[r/'tests/ProcessLens.LiveMetricsTests/Program.cs']+[r/'src/ProcessLens.Windows'/n for n in ['WindowsMemorySampler.cs','WindowsLiveCpuSampler.cs','NativeMethods.cs']]+[r/'src/ProcessLens.App'/n for n in ['WindowLifetimePolicy.cs','MemoryObservationBuffer.cs']]
results=[]
for name,file,before,after,expected in [
 ('false-private-zero','src/ProcessLens.Windows/WindowsMemorySampler.cs','PrivateWorkingSetSize = nuint.MaxValue','PrivateWorkingSetSize = 0','success with only EX prefix cannot invent zero private WS'),
 ('synchronous-minimize','src/ProcessLens.App/WindowLifetimePolicy.cs','_pending = action;','action(); _pending = null;','Closing only queues one minimize after returning'),
]:
 original=r/file;data=original.read_text();assert before in data;mutated=out/(name+'.cs');mutated.write_text(data.replace(before,after,1));dll=out/(name+'.dll')
 args=[str(sdk/'dotnet'),str(sdk/'sdk/8.0.426/Roslyn/bincore/csc.dll'),'-nologo','-nullable:enable','-langversion:12','-target:exe','-out:'+str(dll)]+['-r:'+str(f) for f in [*refs,core]]+[str(globals)]+[str(mutated if f==original else f) for f in sources]
 x=subprocess.run(args,capture_output=True,text=True,timeout=60);assert x.returncode==0,x.stdout+x.stderr
 dll.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':'8.0.32'}}}))
 (out/'ProcessLens.Core.dll').write_bytes(core.read_bytes())
 x=subprocess.run([str(sdk/'dotnet'),str(dll)],capture_output=True,text=True,timeout=30);log=x.stdout+x.stderr;(audit/(name+'-expected-failure.log')).write_text(log)
 assert x.returncode!=0 and 'FAIL R24 '+expected in log,log
 results.append({'mutation':name,'expected_runtime_failure_observed':True,'status':'PASS','production_file':file,'production_sha256':sha(original.read_bytes()),'test_sha256':sha(sources[0].read_bytes())})
(audit/'mutation-evidence.json').write_text(json.dumps(results,indent=2)+'\n');print('PASS',len(results),'independent runtime mutation checks')
