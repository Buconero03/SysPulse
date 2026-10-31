"""Runtime negative controls for Windows icon classification, snapshot fixtures only."""
import argparse,json,hashlib,subprocess
from pathlib import Path
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project',type=Path,required=True);p.add_argument('--sdk',type=Path,required=True);p.add_argument('--work',type=Path,required=True);a=p.parse_args()
r=a.project.resolve();sdk=a.sdk.resolve();work=a.work.resolve();root=work/'mutants-r31';root.mkdir(exist_ok=True);audit=r/'audit/R10_R31';sha=lambda b:hashlib.sha256(b).hexdigest()
refs=sorted((sdk/'packs/Microsoft.NETCore.App.Ref/8.0.32/ref/net8.0').glob('*.dll'));globals=work/'runtime/GlobalUsings.cs'
core_sources=sorted((r/'src/ProcessLens.Core').glob('*.cs'))
sources=[r/'tests/ProcessLens.ProcessGroupsTests/Program.cs']+[r/'src/ProcessLens.Windows'/n for n in ['NativeMethods.cs','OwnedNetworkTableReader.cs','WindowsDeepProcessInspector.cs']]+[r/'src/ProcessLens.App'/n for n in ['ProcessDisplayFormatter.cs','ProcessNetworkPresentation.cs','WindowLifetimePolicy.cs','ReadingPositionPolicy.cs','WindowsProcessIconPolicy.cs','IconPreferencePolicy.cs','DeferredDiagnosticWriter.cs','WindowDiagnosticJournal.cs','NativeIconPreference.cs','TrayCallbackRouter.cs','WindowsComponentContext.cs']]
mutations=[
 ('override-specific-native-icon','src/ProcessLens.App/IconPreferencePolicy.cs','windowsComponent && (original is null || generic)','windowsComponent','R28 Explorer or another Windows component keeps its distinct executable icon'),
 ('defer-ordinary-click','src/ProcessLens.App/WindowLifetimePolicy.cs','_pending = null; // A queued restore must not undo this explicit click.\n        minimize();','_pending = null; // A queued restore must not undo this explicit click.\n        Schedule(minimize);','R28 explicit minimize acts before the queued dispatcher turn'),
 ('stale-restore-after-minimize','src/ProcessLens.App/WindowLifetimePolicy.cs','_pending = null; // A queued restore must not undo this explicit click.','_pending = restore; // Deliberately retain a stale restore for this mutant.','R28 old queued restore cannot undo an immediate minimize'),
 ('synchronous-window-log','src/ProcessLens.App/DeferredDiagnosticWriter.cs','post(Drain); return true;','Drain(); return true;','R28 window logging never writes from the caller thread'),
 ('generic-icon-hides-resource','src/ProcessLens.App/NativeIconPreference.cs','shell is not null && !generic(shell)','shell is not null','R29 embedded executable icon replaces only a generic Shell icon'),
 ('unbounded-window-journal','src/ProcessLens.App/WindowDiagnosticJournal.cs','if (_lines.Count >= Math.Clamp(capacity, 1, 256)) _lines.Dequeue();','// Deliberately keep all lines.','R29 journal retains at most its fixed capacity'),
 ('culture-sensitive-window-clock','src/ProcessLens.App/WindowDiagnosticJournal.cs','string.Create(CultureInfo.InvariantCulture,','string.Create(CultureInfo.CurrentCulture,','R29 trace captures event time and invariant monotonic elapsed time'),
 ('ignored-v4-mouse-release','src/ProcessLens.App/TrayCallbackRouter.cs','0x0202 or 0x0203 or','0x0203 or','R30 ordinary left-button release activates the v4 tray icon'),
 ('other-tray-id-accepted','src/ProcessLens.App/TrayCallbackRouter.cs','packedId == expectedIconId','true','R30 another packed icon cannot trigger a window action'),
 ('packed-event-not-masked','src/ProcessLens.App/TrayCallbackRouter.cs','(int)(bits & 0xFFFF)','(int)bits','R30 v4 decodes event and icon ID separately from coordinates'),
 ('host-lookalike-accepted','src/ProcessLens.Core/WindowsHostReference.cs','x.Executable.Equals(name, StringComparison.OrdinalIgnoreCase)','name.Contains(x.Executable, StringComparison.OrdinalIgnoreCase)','R31 host dictionary rejects lookalike and path-as-name')]
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
 (out/'ProcessLens.Semantics.dll').write_bytes((work/'runtime/ProcessLens.Semantics.dll').read_bytes())
 dll=out/'ProcessLens.ProcessGroupsTests.dll';compile(dll,sources,[core,out/'ProcessLens.Semantics.dll']);dll.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':'8.0.32'}}}))
 x=subprocess.run([str(sdk/'dotnet'),str(dll)],capture_output=True,text=True,timeout=45);log=x.stdout+x.stderr;(audit/(name+'-expected-failure.log')).write_text(log)
 assert x.returncode!=0 and expected in log,log
 results.append({'mutation':name,'expected_failure':expected,'expected_runtime_failure_observed':True,'status':'PASS','production_file':file,'production_sha256':sha(original.read_bytes()),'test_sha256':sha(sources[0].read_bytes())})
(audit/'mutation-evidence.json').write_text(json.dumps(results,indent=2)+'\n');print('PASS',len(results),'runtime negative controls')
