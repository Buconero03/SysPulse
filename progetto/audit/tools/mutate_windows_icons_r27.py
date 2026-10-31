"""Runtime negative controls for Windows icon classification, snapshot fixtures only."""
import argparse,json,hashlib,subprocess
from pathlib import Path
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project',type=Path,required=True);p.add_argument('--sdk',type=Path,required=True);p.add_argument('--work',type=Path,required=True);a=p.parse_args()
r=a.project.resolve();sdk=a.sdk.resolve();work=a.work.resolve();root=work/'mutants-r27';root.mkdir(exist_ok=True);audit=r/'audit/R10_R27';sha=lambda b:hashlib.sha256(b).hexdigest()
refs=sorted((sdk/'packs/Microsoft.NETCore.App.Ref/8.0.32/ref/net8.0').glob('*.dll'));globals=work/'runtime/GlobalUsings.cs'
core_sources=sorted((r/'src/ProcessLens.Core').glob('*.cs'))
sources=[r/'tests/ProcessLens.ProcessGroupsTests/Program.cs']+[r/'src/ProcessLens.Windows'/n for n in ['NativeMethods.cs','OwnedNetworkTableReader.cs','WindowsDeepProcessInspector.cs']]+[r/'src/ProcessLens.App'/n for n in ['ProcessDisplayFormatter.cs','ProcessNetworkPresentation.cs','WindowLifetimePolicy.cs','ReadingPositionPolicy.cs','WindowsProcessIconPolicy.cs']]
mutations=[
 ('name-only-windows-icon','src/ProcessLens.App/WindowsProcessIconPolicy.cs','if (!SafeWindowsPath(p.ExecutablePath, windowsDirectory)) return false;', 'if (p.Name.Equals("svchost.exe", StringComparison.OrdinalIgnoreCase)) return true; if (!SafeWindowsPath(p.ExecutablePath, windowsDirectory)) return false;', 'R27 icon classification: unknown path does not promote a name'),
 ('all-microsoft-apps-are-os','src/ProcessLens.App/WindowsProcessIconPolicy.cs','return product.Equals(', 'return true || product.Equals(', 'R27 icon classification: Microsoft app metadata is not an OS component')]
results=[]
for name,file,before,after,expected in mutations:
 out=root/name;out.mkdir(exist_ok=True);original=r/file;data=original.read_text();assert before in data;mutated=out/'mutated.cs';mutated.write_text(data.replace(before,after,1));core=out/'ProcessLens.Core.dll'
 def compile(target,inputs,extra=(),library=False):
  args=[str(sdk/'dotnet'),str(sdk/'sdk/8.0.426/Roslyn/bincore/csc.dll'),'-nologo','-nullable:enable','-langversion:12','-target:'+('library' if library else 'exe'),'-out:'+str(target)]
  args+=['-analyzer:'+str(f) for f in (sdk/'packs/Microsoft.NETCore.App.Ref/8.0.32/analyzers/dotnet/cs').glob('*.dll')]
  args+=['-r:'+str(f) for f in [*refs,*extra]]+[str(globals)]+[str(mutated if f==original else f) for f in inputs]
  x=subprocess.run(args,capture_output=True,text=True,timeout=60);assert x.returncode==0,x.stdout+x.stderr
 if file.startswith('src/ProcessLens.Core/'):compile(core,core_sources,library=True)
 else:core.write_bytes((work/'runtime/ProcessLens.Core.dll').read_bytes())
 dll=out/'ProcessLens.ProcessGroupsTests.dll';compile(dll,sources,[core]);dll.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':'8.0.32'}}}))
 x=subprocess.run([str(sdk/'dotnet'),str(dll)],capture_output=True,text=True,timeout=45);log=x.stdout+x.stderr;(audit/(name+'-expected-failure.log')).write_text(log)
 assert x.returncode!=0 and expected in log,log
 results.append({'mutation':name,'expected_failure':expected,'expected_runtime_failure_observed':True,'status':'PASS','production_file':file,'production_sha256':sha(original.read_bytes()),'test_sha256':sha(sources[0].read_bytes())})
(audit/'mutation-evidence.json').write_text(json.dumps(results,indent=2)+'\n');print('PASS',len(results),'runtime negative controls')
