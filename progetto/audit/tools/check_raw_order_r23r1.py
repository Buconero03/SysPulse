"""Verify that regression tests reject the old ordering under a simulated Win32 NUL-truncating backend.
Not a Windows execution. The mutant exists only in the private test directory, never in the candidate.
"""
import argparse,hashlib,json,subprocess
from pathlib import Path
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--candidate',type=Path,required=True);p.add_argument('--sdk',type=Path,required=True);p.add_argument('--work',type=Path,required=True);a=p.parse_args()
r=a.candidate.resolve();sdk=a.sdk.resolve();work=a.work.resolve()/'order-mutant';work.mkdir(parents=True,exist_ok=True);audit=r/'audit/R10_R23_R1';sha=lambda b:hashlib.sha256(b).hexdigest()
path=r/'src/ProcessLens.Core/NewTaskLaunch.cs';fixed=path.read_text()
block='''        // Win32 expansion consumes null-terminated strings. Reject raw controls before
        // trimming or expansion, otherwise a NUL suffix can disappear on Windows.
        if (InvalidControls(program, arguments, directory))
        { error = "I campi contengono caratteri non validi o interruzioni di riga."; return false; }
'''
assert fixed.count(block)==1
mutant=work/'NewTaskLaunch.cs';mutant.write_text(fixed.replace(block,'',1))
refs=sorted((sdk/'packs/Microsoft.NETCore.App.Ref/8.0.32/ref/net8.0').glob('*.dll'))
globals=work/'GlobalUsings.cs';globals.write_text(''.join('global using '+s+';\n' for s in ['System','System.Collections.Generic','System.IO','System.Linq','System.Net.Http','System.Threading','System.Threading.Tasks']))
def compile(name,sources,extra=(),library=False):
 cmd=[str(sdk/'dotnet'),str(sdk/'sdk/8.0.426/Roslyn/bincore/csc.dll'),'-nologo','-nullable:enable','-langversion:12','-target:'+('library' if library else 'exe'),'-out:'+str(work/(name+'.dll'))]
 cmd+=['-r:'+str(f) for f in [*refs,*extra]]+[str(globals)]+[str(f) for f in sources]
 x=subprocess.run(cmd,capture_output=True,text=True,timeout=60);(audit/(name+'-mutant-build.log')).write_text(x.stdout+x.stderr)
 assert x.returncode==0,x.stdout+x.stderr
compile('ProcessLens.Core',[mutant if f==path else f for f in sorted((r/'src/ProcessLens.Core').glob('*.cs'))],library=True)
name='ProcessLens.NewTaskTests'
sources=[r/'tests/ProcessLens.NewTaskTests/Program.cs',r/'src/ProcessLens.Windows/WindowsNewTaskLauncher.cs']
compile(name,sources,[work/'ProcessLens.Core.dll'])
(work/(name+'.runtimeconfig.json')).write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':'8.0.32'}}}))
x=subprocess.run([str(sdk/'dotnet'),str(work/(name+'.dll'))],capture_output=True,text=True,timeout=30);log=x.stdout+x.stderr;(audit/'old-order-reproduction.log').write_text(log)
assert x.returncode!=0 and 'FAIL new task: R23 R1 raw input rejected before native expansion: program NUL before suffix' in log,log
(audit/'old-order-evidence.json').write_text(json.dumps({'status':'PASS','kind':'LAB / SIMULATED WIN32; NOT native Windows','old_order_expected_failure_reproduced':True,'mutant_exit_code':x.returncode,'production_source_sha256':sha(path.read_bytes()),'test_sha256':sha(sources[0].read_bytes()),'mutant_source_sha256':sha(mutant.read_bytes()),'mutant_is_delivered':False},indent=2)+'\n')
print('PASS: removing the pre-expansion guard reproduces the failure with the Win32 truncation fixture; production code unchanged.')
