"""Runtime negative controls on production copies: search scope, executable identity, table errors and tray duplicate clicks."""
import argparse,json,hashlib,subprocess
from pathlib import Path
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project',type=Path,required=True);p.add_argument('--sdk',type=Path,required=True);p.add_argument('--work',type=Path,required=True);a=p.parse_args()
r=a.project.resolve();sdk=a.sdk.resolve();work=a.work.resolve();root=work/'mutants-r25';root.mkdir(exist_ok=True);audit=r/'audit/R10_R25';sha=lambda b:hashlib.sha256(b).hexdigest()
refs=sorted((sdk/'packs/Microsoft.NETCore.App.Ref/8.0.32/ref/net8.0').glob('*.dll'));globals=work/'runtime/GlobalUsings.cs'
core_sources=sorted((r/'src/ProcessLens.Core').glob('*.cs'))
sources=[r/'tests/ProcessLens.ProcessGroupsTests/Program.cs']+[r/'src/ProcessLens.Windows'/n for n in ['NativeMethods.cs','OwnedNetworkTableReader.cs','WindowsDeepProcessInspector.cs']]+[r/'src/ProcessLens.App'/n for n in ['ProcessDisplayFormatter.cs','ProcessNetworkPresentation.cs','WindowLifetimePolicy.cs']]
mutations=[
 ('search-arguments','src/ProcessLens.Core/ProcessGrouping.cs','return p.Name.Contains(text, StringComparison.OrdinalIgnoreCase) ||','return p.CommandLine?.Contains(text, StringComparison.OrdinalIgnoreCase) == true || p.Name.Contains(text, StringComparison.OrdinalIgnoreCase) ||','parent, arguments and PID substrings cannot add unrelated search results'),
 ('name-only-group','src/ProcessLens.Core/ProcessGrouping.cs','string.Equals(a.ExecutablePath, b.ExecutablePath, StringComparison.OrdinalIgnoreCase) &&','true &&','homonymous executable from another path is not the same app'),
 ('table-error-as-empty','src/ProcessLens.Windows/OwnedNetworkTableReader.cs','if (result != 0 && result != 122 && result != 234) throw new Win32Exception((int)result);','if (result != 0 && result != 122 && result != 234) return;','expected denial'),
 ('duplicate-tray-click','src/ProcessLens.App/WindowLifetimePolicy.cs','if (_lastActivation.HasValue && now >= _lastActivation.Value && now - _lastActivation.Value <= doubleClickSeconds) return null;','','duplicate or double-click notification cannot immediately reopen minimized window')]
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
