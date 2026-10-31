"""Type-check real C# against WindowsDesktop reference assemblies with explicit generated-field stubs.
This is NOT WPF markup compilation, BAML, MSBuild, or runtime validation. Stubs live outside the deliverable.
"""
import argparse,hashlib,json,subprocess,xml.etree.ElementTree as ET
from pathlib import Path
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--candidate',type=Path,required=True);p.add_argument('--sdk',type=Path,required=True);p.add_argument('--work',type=Path,required=True);a=p.parse_args()
r=a.candidate.resolve();sdk=a.sdk.resolve();work=a.work.resolve();out=work/'typecheck';out.mkdir(exist_ok=True);audit=r/'audit/R10_R17';sha=lambda b:hashlib.sha256(b).hexdigest()
base={p.name:p for p in (sdk/'packs/Microsoft.NETCore.App.Ref/8.0.32/ref/net8.0').glob('*.dll')};desktop=list((work/'wpf-ref').glob('*.dll'))
assert len(desktop)>20
for f in desktop:base[f.name]=f
refs=list(base.values());event=sdk/'packs/Microsoft.AspNetCore.App.Ref/8.0.32/ref/net8.0/System.Diagnostics.EventLog.dll';sqlite=work/'Microsoft.Data.Sqlite.dll'
globals=out/'GlobalUsings.cs';globals.write_text(''.join('global using '+s+';\n' for s in ['System','System.Collections.Generic','System.IO','System.Linq','System.Net.Http','System.Threading','System.Threading.Tasks']))
x='{http://schemas.microsoft.com/winfx/2006/xaml}';stubs=[];inputs={}
for path in (r/'src/ProcessLens.App').glob('*.xaml'):
 tree=ET.parse(path).getroot();cls=tree.get(x+'Class');assert cls
 fields=[]
 for e in tree.iter():
  name=e.get(x+'Name')
  if not name:continue
  tag=e.tag.split('}',1)[1]
  if e.tag.startswith('{clr-namespace:ProcessLens.App}'):typ='ProcessLens.App.'+tag
  elif tag=='Polyline':typ='System.Windows.Shapes.Polyline'
  else:typ='System.Windows.Controls.'+tag
  fields.append('internal '+typ+' '+name+' = null!;')
 ns,short=cls.rsplit('.',1);stubs.append('namespace '+ns+' { public partial class '+short+' { '+ ' '.join(fields)+' private void InitializeComponent() {} } }')
 inputs[path.relative_to(r).as_posix()]=sha(path.read_bytes())
stub=out/'MarkupFieldStubs.cs';stub.write_text('\n'.join(stubs));results=[]
def compile(name,sources,extra=(),library=True):
 for f in sources:
  if f.is_relative_to(r):inputs[f.relative_to(r).as_posix()]=sha(f.read_bytes())
 args=[str(sdk/'dotnet'),str(sdk/'sdk/8.0.426/Roslyn/bincore/csc.dll'),'-nologo','-nullable:enable','-langversion:12','-deterministic+','-target:'+('library' if library else 'exe'),'-out:'+str(out/(name+'.dll'))]
 args+=['-analyzer:'+str(f) for f in (sdk/'packs/Microsoft.NETCore.App.Ref/8.0.32/analyzers/dotnet/cs').glob('*.dll')]
 args+=['-r:'+str(f) for f in [*refs,event,*extra]]+[str(globals)]+[str(f) for f in sources]
 result=subprocess.run(args,capture_output=True,text=True);log=result.stdout+result.stderr;(audit/(name+'-typecheck.log')).write_text(log)
 print(name, 'PASS' if result.returncode==0 else 'FAIL');results.append({'assembly':name,'status':'PASS' if result.returncode==0 else 'FAIL','warning_count':log.count('warning CS')})
 if result.returncode:raise SystemExit(log)
core=work/'runtime/ProcessLens.Core.dll';assert core.is_file()
compile('ProcessLens.Windows',sorted((r/'src/ProcessLens.Windows').glob('*.cs')),[core])
compile('ProcessLens.Semantics',sorted((r/'src/ProcessLens.Semantics').glob('*.cs')),[core])
extra=[core,out/'ProcessLens.Windows.dll',out/'ProcessLens.Semantics.dll',sqlite]
compile('ProcessLens',sorted((r/'src/ProcessLens.App').rglob('*.cs'))+[stub],extra)
for name in ['NetworkUiTests','CopyUiTests','SystemProcessUiTests']:
 compile('ProcessLens.'+name,[r/'tests'/('ProcessLens.'+name)/'Program.cs'],extra+[out/'ProcessLens.dll'],False)
(audit/'wpf-typecheck-evidence.json').write_text(json.dumps({'qualification':'C# type checking with generated-field stubs only; NOT actual WPF markup compilation or runtime','assemblies':results,'inputs':inputs,'stub_sha256':sha(stub.read_bytes()),'reference_hashes':{f.name:sha(f.read_bytes()) for f in [*desktop,event,sqlite]}},indent=2)+'\n')
