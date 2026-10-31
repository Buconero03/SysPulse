"""Compile and run real RAM/header presentation tests using Roslyn/.NET; no WPF runtime or native calls."""
import argparse,hashlib,json,subprocess
from pathlib import Path
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--candidate',type=Path,required=True);p.add_argument('--sdk',type=Path,required=True);p.add_argument('--work',type=Path,required=True);a=p.parse_args()
r=a.candidate.resolve();sdk=a.sdk.resolve();work=a.work.resolve();work.mkdir(parents=True,exist_ok=True);audit=r/'audit/R10_R19'
refs=sorted((sdk/'packs/Microsoft.NETCore.App.Ref/8.0.32/ref/net8.0').glob('*.dll'));sha=lambda b:hashlib.sha256(b).hexdigest()
globals=work/'GlobalUsings.cs';globals.write_text(''.join('global using '+s+';\n' for s in ['System','System.Collections.Generic','System.IO','System.Linq','System.Net.Http','System.Threading','System.Threading.Tasks']))
hashes={}
def compile(name,sources,extra=(),library=False):
 hashes.update({f.relative_to(r).as_posix():sha(f.read_bytes()) for f in sources})
 args=[str(sdk/'dotnet'),str(sdk/'sdk/8.0.426/Roslyn/bincore/csc.dll'),'-nologo','-nullable:enable','-langversion:12','-deterministic+','-target:'+('library' if library else 'exe'),'-out:'+str(work/(name+'.dll'))]
 args+=['-r:'+str(f) for f in [*refs,*extra]]+[str(globals)]+[str(f) for f in sources]
 x=subprocess.run(args,capture_output=True,text=True,timeout=60);(audit/(name+'-build.log')).write_text(x.stdout+x.stderr)
 if x.returncode:raise SystemExit(x.stdout+x.stderr)
compile('ProcessLens.Core',sorted((r/'src/ProcessLens.Core').glob('*.cs')),library=True)
name='ProcessLens.PresentationTests'
compile(name,[r/'tests'/name/'Program.cs',r/'src/ProcessLens.App/ProcessDisplayFormatter.cs',r/'src/ProcessLens.App/ProcessNetworkPresentation.cs'],extra=[work/'ProcessLens.Core.dll'])
(work/(name+'.runtimeconfig.json')).write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':'8.0.32'}}}))
x=subprocess.run([str(sdk/'dotnet'),str(work/(name+'.dll'))],capture_output=True,text=True,timeout=45);log=x.stdout+x.stderr;(audit/(name+'-run.log')).write_text(log)
if x.returncode:raise SystemExit(log)
e={'environment':'Linux x64; direct Roslyn SDK 8.0.426 / .NET 8.0.32; no MSBuild or WPF','status':'PASS','assertions':sum(l.startswith('PASS ') for l in log.splitlines()),'R19_assertions':sum('R19 ' in l and l.startswith('PASS ') for l in log.splitlines()),'source_hashes':hashes}
(audit/'presentation-evidence.json').write_text(json.dumps(e,indent=2)+'\n');print(json.dumps({k:v for k,v in e.items() if k!='source_hashes'},indent=2))
