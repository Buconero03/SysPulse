using System.Buffers.Binary;
using System.Reflection;
using ProcessLens.App;

static void Check(bool ok, string label) { if (!ok) throw new Exception("FAIL icon: " + label); Console.WriteLine("PASS icon: " + label); }
var assembly = Assembly.GetExecutingAssembly();
byte[]? original = IconFile.ReadEmbedded(assembly);
Check(original is { Length: > 100 and <= IconFile.MaximumBytes }, "actual SDK embedded resource is available without a path");
Check(IconFile.TrySelectImage(original,32,out var native), "32-pixel native icon payload selected from actual asset");
Check(native.AsSpan().StartsWith(new byte[]{137,80,78,71,13,10,26,10}), "selected actual image has PNG resource signature");
Check(IconFile.TrySelectImage(original,16,out var small)&&!small.SequenceEqual(native), "small and tray sizes retain distinct image payloads");
Check(IconFile.TrySelectImage(original,256,out var large)&&large.Length>native.Length, "256-pixel entry retains its actual dimensions");
foreach(var invalid in new byte[]?[]{null,Array.Empty<byte>(),new byte[5],new byte[7],new byte[IconFile.MaximumBytes+1]})
 Check(!IconFile.TrySelectImage(invalid,32,out var missing)&&missing.Length==0,"invalid or missing data is unavailable");
byte[] Mutate(int offset,uint value) { var b=original!.ToArray();BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(offset),value);return b; }
Check(!IconFile.TrySelectImage(Mutate(18,uint.MaxValue),32,out _),"overflowing payload offset rejected");
Check(!IconFile.TrySelectImage(Mutate(14,uint.MaxValue),32,out _),"overflowing payload length rejected");
Check(!IconFile.TrySelectImage(Mutate(18,0),32,out _),"payload cannot overlap icon directory");
var cursor=original!.ToArray();cursor[2]=2;
Check(!IconFile.TrySelectImage(cursor,32,out _),"cursor does not masquerade as icon");
Check(!IconFile.TrySelectImage(original,0,out _)&&!IconFile.TrySelectImage(original,257,out _),"invalid requested dimensions rejected");
Check(IconFile.ReadEmbedded(typeof(object).Assembly) is null,"missing optional assembly resource is unavailable");
Check(original!.SequenceEqual(IconFile.ReadEmbedded(assembly)!),"reads preserve embedded bytes exactly");
if(args.Contains("--expect-single-file")) {
#pragma warning disable IL3000 // Deliberately assert the single-file compatibility condition.
 Check(assembly.Location.Length==0,"single-file assembly has no external location");
#pragma warning restore IL3000
 Check(IconFile.TrySelectImage(IconFile.ReadEmbedded(assembly),32,out _),"embedded icon survives real single-file publication without external assets");
}
Console.WriteLine("Icon resource tests complete; no WPF, native icon handles, filesystem icon lookup or user data.");

// R33: same embedded catalogue used by the app; exact names and display-only group overrides.
Check(ProcessSymbolCatalog.LoadError is null && ProcessSymbolCatalog.Entries.Count==61,"R33 actual SDK catalogue loads with all 61 entries");
Check(ProcessSymbolCatalog.Find("bash.exe")?.Id=="bash"&&ProcessSymbolCatalog.Find("git.exe")?.Id=="git","R33 Bash and Git have distinct symbols");
Check(ProcessSymbolCatalog.Find("System")?.Id=="system"&&ProcessSymbolCatalog.Find("System Idle Process")?.Id=="idle","R33 System and Idle have distinct symbols");
Check(ProcessSymbolCatalog.Find("RuntimeBroker.exe")?.Id=="broker"&&ProcessSymbolCatalog.Find("svchost.exe")?.Id=="svchost","R33 Windows components have explicit symbols");
foreach(string name in new[]{"git-helper.exe","not-git.exe","C:\\Tools\\git.exe","/tmp/git.exe","git.exe --anything","git.exe.bak"," ","git\n.exe"})
 Check(ProcessSymbolCatalog.Find(name) is null,"R33 exact-name boundary excludes "+name.Replace('\n',' '));
Check(ProcessSymbolCatalog.GroupOverride("Git","bash.exe")?.Id=="git","R33 Git group cannot inherit Bash image");
Check(ProcessSymbolCatalog.GroupOverride("Git","git.exe") is null,"R33 actual Git executable retains native icon priority");
Check(ProcessSymbolCatalog.GroupOverride("Google Chrome","chrome.exe") is null,"R33 Chrome group retains actual Chrome icon");
Check(ProcessSymbolCatalog.GroupOverride("Git helper","bash.exe") is null,"R33 group labels use exact aliases");
Check(ProcessSymbolCatalog.Find("BASH.EXE")?.Id==ProcessSymbolCatalog.Find("bash")?.Id,"R33 name matching is case insensitive");
Check(ProcessIconPlaceholder.Pattern("hello.exe")==ProcessIconPlaceholder.Pattern("HELLO.EXE"),"R33 generated pattern is stable across case");
Check(Enumerable.Range(0,1000).All(i=>ProcessIconPlaceholder.Pattern("unknown"+i+".exe") is >0 and <=0x7fff),"R33 every generated icon has visible bounded cells");
Check(Enumerable.Range(0,1000).Select(i=>(ProcessIconPlaceholder.Pattern("unknown"+i+".exe"),ProcessIconPlaceholder.ColorIndex("unknown"+i+".exe"))).Distinct().Count()>980,"R33 unknown files do not collapse to one generic pattern");
Check(ProcessIconPlaceholder.Pattern(null)>0&&ProcessIconPlaceholder.Pattern("你好.exe")>0,"R33 missing and Unicode names keep a visible pattern");
Check(ProcessSymbolCatalog.Entries.All(s=>s.Layers.Count>0&&s.Layers.Count<=12&&s.Color.Length==7),"R33 catalogue geometry remains bounded");
Check(ProcessSymbolCatalog.Entries.Select(s=>s.Id).Distinct().Count()==61,"R33 catalogue identifiers are distinct");
Console.WriteLine("R33 catalogue regressions complete; no native icon acquisition or network.");
