using System.Globalization;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Net;
using System.Net.Sockets;
using ProcessLens.Core;
using ProcessLens.Windows;
using ProcessLens.App;

static void Check(bool ok, string label)
{ if (!ok) throw new Exception("FAIL R25 " + label); Console.WriteLine("PASS R25 " + label); }
var start = new DateTime(2026,10,23,12,0,0,DateTimeKind.Utc);
ProcessSnapshot Chrome(int pid, int parent, int second = 0) => new() { Pid = pid, ParentPid = parent, Name = "chrome.exe",
    ExecutablePath = @"C:\Google\Chrome\chrome.exe", FileDescription = "Google Chrome", StartTime = start.AddSeconds(second) };
var root = Chrome(10,1); var child = Chrome(11,10,1); var leaf = Chrome(12,11,2);
var otherRoot = Chrome(20,1); var otherChild = Chrome(21,20,1);
var cloud = new ProcessSnapshot { Pid=30, ParentPid=10, Name="claude.exe", FileDescription="Claude", ProductName="Google integration",
    ExecutablePath=@"C:\Cloud\claude.exe", StartTime=start.AddSeconds(1), CommandLine="--google integration", ParentName="Google Chrome" };
var list = new[] { root, child, leaf, otherRoot, otherChild, cloud };
var groups = ProcessGrouping.Build(list);
Check(groups.Count==3 && groups.Single(g=>g.Root.Pid==10).Members.Select(x=>x.Pid).SequenceEqual(new[]{10,11,12}), "same executable linked children form a group");
Check(groups.Single(g=>g.Root.Pid==20).Members.Count==2 && groups.Single(g=>g.Root.Pid==30).Members.Count==1, "independent browser instances and other executables remain separate");
Check(ProcessGrouping.Build(new[]{ root, child with { ExecutablePath=@"C:\Other\chrome.exe" } }).Count==2, "homonymous executable from another path is not the same app");
Check(ProcessGrouping.Build(new[]{ root with { StartTime=start.AddSeconds(3) }, child }).Count==2, "newer reused parent cannot adopt an older child");
Check(ProcessGrouping.Build(new[]{ root with { StartTime=null }, child }).Count==2 &&
    ProcessGrouping.Build(new[]{ root with { ExecutablePath=null }, child }).Count==2, "unknown creation or path never establishes grouping");
Check(ProcessGrouping.Build(new[]{ root with { ParentPid=11 }, child with { StartTime=start } }).Count==2, "cyclic parent graph cannot fabricate an application tree");
Check(ProcessGrouping.Build(new[]{ child }).Single().Root.Pid==11, "orphan stays a separate group");
Check(ProcessGrouping.Build(new[]{ root }).Single().Key != ProcessGrouping.Build(new[]{ root with { StartTime=start.AddDays(1) } }).Single().Key,
    "group expansion key cannot cross PID reuse");
Check(ProcessIdentitySearch.Matches(root,"google") && ProcessIdentitySearch.Matches(root,"CHROME") && ProcessIdentitySearch.Matches(root,"10"), "search accepts executable identity, display metadata and exact PID");
Check(!ProcessIdentitySearch.Matches(cloud,"google") && !ProcessIdentitySearch.Matches(cloud,"10") && !ProcessIdentitySearch.Matches(root,"1"),
    "parent, arguments and PID substrings cannot add unrelated search results");
Check(ProcessIdentitySearch.Matches(cloud,"  claude  ") && ProcessIdentitySearch.Matches(cloud,""), "trimmed and empty identity filters");
Check(!ProcessIdentitySearch.Matches(root,"google","Cloud"), "hidden product/description metadata cannot override the displayed name filter");
var visible=list.Where(x=>ProcessIdentitySearch.Matches(x,"google")).ToArray();
for(int i=0;i<20;i++) visible=list.Select(x=>x with { WorkingSetBytes=i*1048576L }).Where(x=>ProcessIdentitySearch.Matches(x,"google")).ToArray();
Check(visible.Length==5 && visible.All(x=>x.Name=="chrome.exe"), "repeated refreshed snapshots keep the active identity filter");
Check(ProcessGrouping.IdentityMatches(root, root with { CpuPercent=9 }) && !ProcessGrouping.IdentityMatches(root, root with { StartTime=start.AddSeconds(1) }) &&
    !ProcessGrouping.IdentityMatches(root, root with { ExecutablePath=@"C:\Other\chrome.exe" }), "network ownership gate requires exact known creation and executable path");
foreach (string culture in new[]{"it-IT","en-US"})
{
    CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo(culture);
    var a=root with { CpuSampleAvailable=true, CpuPercent=2.5, PrivateWorkingSetAvailable=true, PrivateWorkingSetBytes=10*1048576, WorkingSetAvailable=true, WorkingSetBytes=20*1048576 };
    var b=child with { CpuSampleAvailable=true, CpuPercent=3.5, PrivateWorkingSetAvailable=true, PrivateWorkingSetBytes=30*1048576, WorkingSetAvailable=true, WorkingSetBytes=50*1048576 };
    var usage=ProcessGroupUsage.From(new[]{a,b});
    Check(usage.CpuPercent==6 && usage.CpuKnown==2 && usage.MemoryMb==40 && usage.MemoryKnown==2 && usage.PrivateResident, "group sums private resident and valid CPU ["+culture+"]");
    usage=ProcessGroupUsage.From(new[]{a,b with { PrivateWorkingSetAvailable=false, CpuSampleAvailable=false }});
    Check(usage.MemoryMb==10 && usage.MemoryKnown==1 && usage.CpuPercent==2.5 && usage.CpuKnown==1 && usage.Count==2,
        "partial coverage cannot mix total WS or missing CPU into private totals ["+culture+"]");
    usage=ProcessGroupUsage.From(new[]{a with { PrivateWorkingSetAvailable=false },b with { PrivateWorkingSetAvailable=false }});
    Check(!usage.PrivateResident && usage.MemoryMb==70, "explicit total-resident fallback stays separate ["+culture+"]");
    usage=ProcessGroupUsage.From(new[]{root,child});
    Check(usage.MemoryKnown==0 && usage.CpuKnown==0, "missing group counters remain unavailable ["+culture+"]");
    var net=root with { IsDeepInspected=true, NetworkCapturedUtc=start, NetworkAcquisitionComplete=false,
        NetworkVerifiedProcessCount=2, NetworkFamilyProcessCount=2, DirectNetworkIdentityVerified=true,
        NetworkAcquisitionNote="TCP IPv6: AccessDenied", NetworkEndpoints=new[]{new NetworkEndpointSnapshot("TCP","LISTEN","127.0.0.1",9222,null,null,"SOLO LOCALE")},
        RelatedNetworkProcesses=new[]{new RelatedNetworkProcessSnapshot(11,"chrome.exe","Servizio di rete",0,1,0,1,
            new[]{new NetworkEndpointSnapshot("TCP","ESTABLISHED","192.168.1.2",51000,"1.1.1.1",443,"PUBBLICO")})} };
    string report=ProcessNetworkPresentation.DescribeEndpointOwners(net);
    Check(report.Contains("LETTURA PARZIALE") && report.Contains("AccessDenied") && report.Contains("PID 10 · TCP LISTEN") &&
        report.Contains("PID 11 · TCP ESTABLISHED") && report.Contains("locale [192.168.1.2]:51000 → remoto [1.1.1.1]:443"),
        "sample time, partial read and per-PID local/remote port ownership retained ["+culture+"]");
    Check(ProcessNetworkPresentation.Describe(net with { DirectNetworkIdentityVerified=false }).Contains("socket N/D"), "unverified selected instance cannot acquire measured zero sockets ["+culture+"]");
}
var staleNetwork=root with { CpuSampleAvailable=true,CpuPercent=42,WorkingSetAvailable=true,WorkingSetBytes=1234,
    TcpListeningCount=2,NetworkFamilyProcessCount=10,NetworkFamilyTcpEstablishedCount=9,
    NetworkEndpoints=new[]{new NetworkEndpointSnapshot("TCP","LISTEN","127.0.0.1",9222,null,null,"SOLO LOCALE")},
    RelatedNetworkProcesses=new[]{new RelatedNetworkProcessSnapshot(11,"chrome.exe","rete",1,2,0,2,Array.Empty<NetworkEndpointSnapshot>())} };
var invalidated=ProcessNetworkEvidence.Unavailable(staleNetwork,"PID riutilizzato");
Check(invalidated.DirectNetworkIdentityVerified==false && !invalidated.NetworkAcquisitionComplete && invalidated.NetworkVerifiedProcessCount==0 &&
    invalidated.NetworkEndpoints.Count==0 && invalidated.RelatedNetworkProcesses.Count==0 && invalidated.TcpListeningCount==0 && invalidated.NetworkFamilyTcpEstablishedCount==0,
    "invalid selected anchor cannot retain another instance's group sockets or counters");
Check(invalidated.CpuPercent==42 && invalidated.WorkingSetBytes==1234 && ProcessNetworkPresentation.Describe(invalidated).Contains("socket N/D"),
    "network invalidation preserves independent CPU/RAM while displaying unavailable socket identity");
var tray = new TrayActivationGate();
Check(tray.Activate(10,0.5,true,false)==TrayActivation.Minimize,"visible tray click requests minimize");
Check(tray.Activate(10.01,0.5,true,true) is null && tray.Activate(10.3,0.5,true,true) is null,
    "duplicate or double-click notification cannot immediately reopen minimized window");
Check(tray.Activate(11,0.5,true,true)==TrayActivation.Restore,"later tray click restores a minimized window");
Check(tray.Activate(11.2,0.5,true,false) is null,"second click of restore double-click cannot minimize again");
Check(tray.Activate(12,0.5,false,false)==TrayActivation.Restore && tray.Activate(double.NaN,0.5,true,false) is null,
    "hidden window is restored and invalid callback clock is ignored");
// Independent SDK fixtures: table header is DWORD, owner PID is at byte 20 in IPv4 TCP.
Check(Marshal.SizeOf<NativeMethods.MIB_TCPROW_OWNER_PID>()==24 && Marshal.SizeOf<NativeMethods.MIB_TCP6ROW_OWNER_PID>()==56 &&
    Marshal.SizeOf<NativeMethods.MIB_UDPROW_OWNER_PID>()==12 && Marshal.SizeOf<NativeMethods.MIB_UDP6ROW_OWNER_PID>()==28,
    "IPv4/IPv6 TCP/UDP row stride matches SDK fixture");
var decodePort=typeof(WindowsDeepProcessInspector).GetMethod("PortFromNetworkOrder",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)!;
Check((int)decodePort.Invoke(null,new object[]{0x50c3u})! == 50000 && (int)decodePort.Invoke(null,new object[]{0xbb01u})! == 443,
    "native port conversion distinguishes ephemeral local 50000 and remote 443");
uint IPv6Fixture(IntPtr buffer,ref int size)
{
    size=60;if(buffer==IntPtr.Zero)return 122;
    Marshal.Copy(new byte[60],0,buffer,60);Marshal.WriteInt32(buffer,1);
    Marshal.WriteByte(buffer,4+15,1);Marshal.WriteInt32(buffer,4+16,7);Marshal.WriteInt32(buffer,4+20,0xbb01);
    Marshal.WriteInt32(buffer,4+48,5);Marshal.WriteInt32(buffer,4+52,43);return 0;
}
OwnedNetworkTableReader.Read(56,IPv6Fixture,(rows,count)=>
{
    var row=Marshal.PtrToStructure<NativeMethods.MIB_TCP6ROW_OWNER_PID>(rows);
    Check(count==1 && row.dwOwningPid==43 && row.dwLocalScopeId==7 && row.ucLocalAddr![15]==1 && row.dwLocalPort==0xbb01,
        "independent IPv6 fixture preserves scope, owner and network-order port");
});
int calls=0; bool decoded=false;
uint Table(IntPtr buffer,ref int size)
{
    calls++; if(buffer==IntPtr.Zero) { size=28;return 122; }
    Marshal.WriteInt32(buffer,1); for(int i=4;i<28;i+=4) Marshal.WriteInt32(buffer,i,0);
    Marshal.WriteInt32(buffer,4,5); Marshal.WriteInt32(buffer,4+8,0x50c3); Marshal.WriteInt32(buffer,4+20,42);size=28;return 0;
}
OwnedNetworkTableReader.Read(24,Table,(rows,count)=> { var row=Marshal.PtrToStructure<NativeMethods.MIB_TCPROW_OWNER_PID>(rows);
    decoded=count==1 && row.dwOwningPid==42 && row.dwState==5 && row.dwLocalPort==0x50c3; });
Check(calls==2 && decoded,"independent native table fixture decodes owner, state and network-order port");
calls=0;
uint Growing(IntPtr buffer,ref int size)
{ calls++; if(calls==1){size=4;return 122;} if(calls==2){size=28;return 122;} return Table(buffer,ref size); }
OwnedNetworkTableReader.Read(24,Growing,(_,count)=>Check(count==1,"growing socket table retried with new capacity"));
uint Denied(IntPtr buffer,ref int size)=>5;
try { OwnedNetworkTableReader.Read(24,Denied,(_,_)=>{});throw new Exception("expected denial"); }
catch(Win32Exception ex) { Check(ex.NativeErrorCode==5,"native access error cannot become a valid empty table"); }
uint Truncated(IntPtr buffer,ref int size)
{ size=4;if(buffer==IntPtr.Zero)return 122;Marshal.WriteInt32(buffer,100);return 0; }
try { OwnedNetworkTableReader.Read(24,Truncated,(_,_)=>{});throw new Exception("expected truncation"); }
catch(InvalidDataException) { Check(true,"table count exceeding returned bytes is rejected"); }
uint Huge(IntPtr buffer,ref int size) {size=17*1024*1024;return 122;}
try { OwnedNetworkTableReader.Read(24,Huge,(_,_)=>{});throw new Exception("expected cap"); }
catch(InvalidDataException) { Check(true,"oversized table rejected before allocation"); }
if(OperatingSystem.IsWindows())
{
    using var listener=new TcpListener(IPAddress.Loopback,0); listener.Start();int port=((IPEndPoint)listener.LocalEndpoint).Port;
    using var own=System.Diagnostics.Process.GetCurrentProcess();
    var snapshot=new ProcessSnapshot{Pid=own.Id,ParentPid=0,Name=own.ProcessName+".exe",ExecutablePath=own.MainModule!.FileName,StartTime=own.StartTime};
    var observed=new WindowsDeepProcessInspector().RefreshNetwork(snapshot);
    Check(observed.DirectNetworkIdentityVerified==true && observed.NetworkEndpoints.Any(e=>e.Protocol=="TCP" && e.State=="LISTEN" && e.LocalPort==port),
        "WINDOWS native reader finds only its owned loopback listener with exact selected identity");
}
else Console.WriteLine("NOT RUN: Windows native tables and owned loopback listener (Linux host).");
Console.WriteLine("R25 grouping, identity filter and network table regressions complete.");

// R26: exercise the actual reconciliation and cancellation policy used by WPF.
var position26 = new ReadingPositionPolicy();
long ticket26 = position26.Capture(); position26.UserInput();
Check(!position26.IsCurrent(ticket26), "R26 user input cancels an older queued position");
long second26 = position26.Capture(); long third26 = position26.Capture();
Check(!position26.IsCurrent(second26) && position26.IsCurrent(third26), "R26 only the latest ticket may restore a viewport");
Check(ReadingPositionPolicy.HoldOrder(250,false,false), "R26 middle-page reading freezes rank");
Check(ReadingPositionPolicy.HoldOrder(0,true,false) && ReadingPositionPolicy.HoldOrder(0,false,true), "R26 pointer and keyboard targets do not move");
Check(!ReadingPositionPolicy.HoldOrder(0,false,false), "R26 automatic ranking resumes at top outside interaction");
Check(ReadingPositionPolicy.AnchoredOffset(300,-10,-60)==250, "R26 removal above retains the anchor screen position");
Check(ReadingPositionPolicy.AnchoredOffset(300,-10,40)==350, "R26 growth above retains the anchor screen position");
Check(ReadingPositionPolicy.AnchoredOffset(0,10,0)==0, "R26 anchor offset cannot be negative");
var a26=new object();var b26=new object();var c26=new object();var new26=new object();
var reading26=new System.Collections.ObjectModel.ObservableCollection<object>(new[]{a26,b26,c26});
var events26=new List<System.Collections.Specialized.NotifyCollectionChangedAction>();
reading26.CollectionChanged+=(_,e)=>events26.Add(e.Action);
ReadingPositionPolicy.Synchronize(reading26,new[]{c26,new26,a26,b26},true);
Check(reading26.SequenceEqual(new[]{a26,b26,c26,new26}), "R26 changed rank and a new PID retain reader order");
ReadingPositionPolicy.Synchronize(reading26,new[]{new26,c26,b26},true);
Check(reading26.SequenceEqual(new[]{b26,c26,new26}), "R26 exited PID disappears without reordering survivors");
ReadingPositionPolicy.Synchronize(reading26,new[]{new26,c26,b26},false);
Check(reading26.SequenceEqual(new[]{new26,c26,b26}), "R26 explicit sorting applies current ranking in place");
Check(!events26.Contains(System.Collections.Specialized.NotifyCollectionChangedAction.Reset), "R26 reconciliation never clears or resets the collection");
events26.Clear();ReadingPositionPolicy.Synchronize(reading26,reading26.ToArray(),false);
Check(events26.Count==0, "R26 unchanged inventory produces no collection events");
ReadingPositionPolicy.Synchronize(reading26,Array.Empty<object>(),true);
Check(reading26.Count==0, "R26 empty snapshot removes exited instances");
Check(WindowLifetimePolicy.DeferMinimize(true,false) && WindowLifetimePolicy.DeferMinimize(false,true), "R28 Closing and native callbacks defer window changes");
Check(!WindowLifetimePolicy.DeferMinimize(false,false), "R28 ordinary header click does not defer minimize");
Console.WriteLine("R26 reading and native-command policy regressions complete.");

// R27: snapshot-only icon classification; no Shell/native calls.
ProcessSnapshot IconProcess(string name,string? path,string? company=null,string? product=null,int pid=7000)=>new(){
    Pid=pid,ParentPid=1,Name=name,ExecutablePath=path,CompanyName=company,ProductName=product};
const string win27=@"C:\Windows";
foreach(var d in WindowsComponentCatalog.Definitions.Where(d=>d.RelativePath is not null))
{
    var p27=IconProcess(d.Name,win27+@"\"+d.RelativePath);
    Check(WindowsProcessIconPolicy.UseWindowsIcon(p27,win27),"R27 catalog component uses Windows icon at expected path: "+d.Name);
}
var icons27=new (string Label,ProcessSnapshot Process,string Directory,bool Expected)[]{
    ("reserved System PID",IconProcess("System",null,pid:4),"",true),
    ("reserved idle PID",IconProcess("Idle",null,pid:0),"",true),
    ("reserved idle display name",IconProcess("System Idle Process",null,pid:0),"",true),
    ("non-reserved System name",IconProcess("System",null),win27,false),
    ("wrong reserved PID name",IconProcess("unrelated.exe",null,pid:4),win27,false),
    ("wrong idle PID name",IconProcess("unrelated.exe",null,pid:0),win27,false),
    ("unknown path does not promote a name",IconProcess("svchost.exe",null),win27,false),
    ("unexpected executable path",IconProcess("svchost.exe",@"C:\Users\fixture\svchost.exe"),win27,false),
    ("Windows prefix collision",IconProcess("svchost.exe",@"C:\Windows.old\System32\svchost.exe"),win27,false),
    ("path traversal",IconProcess("svchost.exe",@"C:\Windows\System32\..\Temp\svchost.exe"),win27,false),
    ("alternate stream",IconProcess("svchost.exe",@"C:\Windows\System32\svchost.exe:payload"),win27,false),
    ("control character in path",IconProcess("svchost.exe","C:\\Windows\\System32\\svchost.exe\0"),win27,false),
    ("snapshot name differs from file",IconProcess("svchost.exe",@"C:\Windows\System32\other.exe"),win27,false),
    ("case and path separators",IconProcess("SVCHOST.EXE","c:/windows/system32/SVCHOST.EXE"),"C:/WINDOWS/",true),
    ("executable extension omitted in snapshot",IconProcess("svchost",@"C:\Windows\System32\svchost.exe"),win27,true),
    ("actual alternate Windows drive",IconProcess("svchost.exe",@"D:\OS\System32\svchost.exe"),@"D:\OS",true),
    ("empty Windows directory",IconProcess("svchost.exe",@"C:\Windows\System32\svchost.exe"),"",false),
    ("malformed Windows directory",IconProcess("svchost.exe",@"C:\Windows\System32\svchost.exe"),@"C:\OS\..\Windows",false),
    ("ShellHost OS product metadata",IconProcess("ShellHost.exe",@"C:\Windows\System32\ShellHost.exe","Microsoft Corporation","Microsoft® Windows® Operating System"),win27,true),
    ("SystemApps Italian OS metadata",IconProcess("TextInputHost.exe",@"C:\Windows\SystemApps\fixture\TextInputHost.exe","Microsoft Corporation","Sistema operativo Microsoft® Windows®"),win27,true),
    ("Microsoft app metadata is not an OS component",IconProcess("app.exe",@"C:\Windows\System32\app.exe","Microsoft Corporation","Microsoft Office"),win27,false),
    ("publisher alone is insufficient",IconProcess("app.exe",@"C:\Windows\System32\app.exe","Microsoft Corporation",null),win27,false),
    ("OS product alone is insufficient",IconProcess("app.exe",@"C:\Windows\System32\app.exe",null,"Microsoft Windows Operating System"),win27,false),
    ("lookalike publisher",IconProcess("app.exe",@"C:\Windows\System32\app.exe","Not Microsoft Corporation","Microsoft Windows Operating System"),win27,false),
    ("lookalike product",IconProcess("app.exe",@"C:\Windows\System32\app.exe","Microsoft Corporation","Microsoft Windows Operating System Helper"),win27,false),
    ("OS metadata outside Windows",IconProcess("app.exe",@"C:\Apps\app.exe","Microsoft Corporation","Microsoft Windows Operating System"),win27,false),
    ("OS metadata in Windows Temp",IconProcess("app.exe",@"C:\Windows\Temp\app.exe","Microsoft Corporation","Microsoft Windows Operating System"),win27,false),
    ("ordinary app in system32",IconProcess("thirdparty.exe",@"C:\Windows\System32\thirdparty.exe"),win27,false),
    ("ordinary app keeps original icon",IconProcess("chrome.exe",@"C:\Apps\Chrome\chrome.exe","Google LLC","Google Chrome"),win27,false)
};
foreach(var c27 in icons27)
{
    Check(WindowsProcessIconPolicy.UseWindowsIcon(c27.Process,c27.Directory)==c27.Expected,"R27 icon classification: "+c27.Label);
}
var before27=icons27[18].Process with{};
WindowsProcessIconPolicy.UseWindowsIcon(icons27[18].Process,win27);
Check(icons27[18].Process==before27,"R27 visual classification never mutates the snapshot");
Check(WindowsProcessIconPolicy.WindowsDescription.Contains("Firma digitale non verificata") &&
    !WindowsProcessIconPolicy.WindowsDescription.Contains("sicuro"),"R27 Windows icon is a category marker rather than a safety verdict");
Console.WriteLine("R27 Windows icon policy regressions complete; synthetic snapshots only.");

// R28 native icons take priority over a Windows classification.
var native28=new object();var generic28=new object();var windows28=new object();
Check(ReferenceEquals(IconPreferencePolicy.Select(native28,false,true,windows28),native28),"R28 Explorer or another Windows component keeps its distinct executable icon");
Check(ReferenceEquals(IconPreferencePolicy.Select(native28,false,false,windows28),native28),"R28 ordinary application keeps its original icon");
Check(ReferenceEquals(IconPreferencePolicy.Select(generic28,true,true,windows28),windows28),"R28 generic Windows host gets shared Windows fallback");
Check(ReferenceEquals(IconPreferencePolicy.Select(generic28,true,false,windows28),generic28),"R28 generic third-party service is not promoted to Windows");
Check(ReferenceEquals(IconPreferencePolicy.Select<object>(null,false,true,windows28),windows28),"R28 missing icon for classified OS component uses Windows fallback");
Check(IconPreferencePolicy.Select<object>(null,false,false,windows28) is null,"R28 unavailable unclassified icon stays unavailable");
Check(IconPreferencePolicy.SamePixels(new byte[]{1,2,3,255},new byte[]{1,2,3,255}),"R28 independently created identical pixel content matches the generic icon");
Check(!IconPreferencePolicy.SamePixels(new byte[]{1,2,4,255},new byte[]{1,2,3,255}),"R28 one different color channel retains a distinct icon");
Check(!IconPreferencePolicy.SamePixels(new byte[]{1,2,3,0},new byte[]{1,2,3,255}),"R28 alpha differences retain a distinct icon");
Check(!IconPreferencePolicy.SamePixels(Array.Empty<byte>(),Array.Empty<byte>()),"R28 empty data cannot establish a generic icon match");
Check(!IconPreferencePolicy.SamePixels(new byte[IconPreferencePolicy.MaximumPixelBytes+1],new byte[IconPreferencePolicy.MaximumPixelBytes+1]),"R28 pixel comparison has a fixed byte budget");
var queued28=new Queue<Action>();int min28=0,restore28=0;
using(var controls28=new DeferredWindowActions(a=>queued28.Enqueue(a),()=>min28++,()=>restore28++))
{
    controls28.Restore();controls28.MinimizeImmediately();
    Check(min28==1 && restore28==0,"R28 explicit minimize acts before the queued dispatcher turn");
    queued28.Dequeue()();Check(min28==1 && restore28==0,"R28 old queued restore cannot undo an immediate minimize");
    controls28.MinimizeImmediately();controls28.Restore();queued28.Dequeue()();
    Check(min28==2 && restore28==1,"R28 later explicit restore remains available");
    controls28.Dispose();controls28.MinimizeImmediately();
    Check(min28==2,"R28 explicit exit prevents later immediate window changes");
}
var jobs28=new Queue<Action>();var messages28=new List<string>();
var writer28=new DeferredDiagnosticWriter(a=>jobs28.Enqueue(a),x=>messages28.Add(x),2);
Check(writer28.Enqueue("click") && writer28.Enqueue("native-state") && messages28.Count==0,"R28 window logging never writes from the caller thread");
Check(!writer28.Enqueue("overflow") && jobs28.Count==1,"R28 diagnostics retain at most the configured queue with one worker");
jobs28.Dequeue()();Check(messages28.SequenceEqual(new[]{"click","native-state"}),"R28 accepted window diagnostic events retain their order");
Check(writer28.Enqueue("restore") && jobs28.Count==1,"R28 worker can be scheduled after a completed drain");jobs28.Dequeue()();
var errors28=new Queue<Action>();int writes28=0;
var failingWriter28=new DeferredDiagnosticWriter(a=>errors28.Enqueue(a),_=>{writes28++;throw new IOException("fixture");});
failingWriter28.Enqueue("one");failingWriter28.Enqueue("two");errors28.Dequeue()();
Check(writes28==2,"R28 a failing diagnostic sink cannot stop later window events");
var unavailableWriter28=new DeferredDiagnosticWriter(_=>throw new InvalidOperationException("fixture"),_=>throw new Exception());
Check(!unavailableWriter28.Enqueue("click"),"R28 failed worker scheduling cannot throw through a window command");
Console.WriteLine("R28 icon preference and window dispatch regressions complete.");

// R29: no Shell/file-system access. A real icon wins and the resource fallback stays lazy.
var shell29=new object();var resource29=new object();var generic29=new object();int reads29=0;
var nativeChoice29=NativeIconPreference.Resolve(shell29,x=>ReferenceEquals(x,generic29),()=>{reads29++;return resource29;});
Check(ReferenceEquals(nativeChoice29.Image,shell29)&&reads29==0&&nativeChoice29.Source=="Shell","R29 a distinct Shell icon skips all embedded-resource reads");
nativeChoice29=NativeIconPreference.Resolve(generic29,x=>ReferenceEquals(x,generic29),()=>{reads29++;return resource29;});
Check(ReferenceEquals(nativeChoice29.Image,resource29)&&reads29==1&&nativeChoice29.Source=="Risorsa eseguibile","R29 embedded executable icon replaces only a generic Shell icon");
nativeChoice29=NativeIconPreference.Resolve<object>(null,_=>false,()=>resource29);
Check(ReferenceEquals(nativeChoice29.Image,resource29),"R29 missing Shell icon can recover an embedded icon");
nativeChoice29=NativeIconPreference.Resolve(generic29,x=>ReferenceEquals(x,generic29),()=>generic29);
Check(ReferenceEquals(nativeChoice29.Image,generic29)&&nativeChoice29.Source=="Generica","R29 a generic embedded resource is never called a recovered original");
nativeChoice29=NativeIconPreference.Resolve<object>(null,_=>false,()=>throw new IOException("fixture"));
Check(nativeChoice29.Image is null&&nativeChoice29.Source=="Non disponibile","R29 failed extraction remains explicitly unavailable");
nativeChoice29=NativeIconPreference.Resolve(generic29,x=>ReferenceEquals(x,generic29),()=>throw new IOException("fixture"));
Check(ReferenceEquals(nativeChoice29.Image,generic29),"R29 resource failure retains the existing generic image");
foreach(string culture29 in new[]{"it-IT","en-US"})
{
    System.Globalization.CultureInfo.CurrentCulture=System.Globalization.CultureInfo.GetCultureInfo(culture29);
    var stamp29=new DateTimeOffset(2026,10,25,12,34,56,TimeSpan.Zero);double clock29=100;
    var delivered29=new List<string>();var journal29=new WindowDiagnosticJournal(x=>delivered29.Add(x),()=>stamp29,()=>clock29,capacity:3);
    journal29.Record(9,"INPUT","click\r\nforged\ttext");clock29=102.125;
    journal29.Record(9,"CLICK","registered");clock29=105.5;journal29.Record(9,"STATE","Minimized");
    string report29=journal29.Snapshot();
    Check(delivered29.Count==3&&report29.Split(Environment.NewLine).Length==3,"R29 trace events are one line and retain input click state order ["+culture29+"]");
    Check(report29.Contains("2026-10-25T12:34:56.0000000+00:00")&&report29.Contains("+2.125s"),"R29 trace captures event time and invariant monotonic elapsed time ["+culture29+"]");
    Check(report29.Contains("tentativo=9")&&report29.Contains("n=1")&&report29.Contains("n=3"),"R29 one attempt has correlated events with sequence numbers ["+culture29+"]");
    journal29.Record(10,"RIPRISTINO","later");
    Check(!journal29.Snapshot().Contains("INPUT")&&journal29.Snapshot().Contains("tentativo=10"),"R29 journal retains at most its fixed capacity without losing the later restore ["+culture29+"]");
    journal29.Record(10,"VALUE",new string('x',5000));
    Check(journal29.Snapshot().Split(Environment.NewLine).Last().Length<1700,"R29 individual trace lines have a fixed budget ["+culture29+"]");
}
var failedJournal29=new WindowDiagnosticJournal(_=>throw new IOException("fixture"));
failedJournal29.Record(1,"CLICK","retained");
Check(failedJournal29.Snapshot().Contains("retained"),"R29 memory trace survives a failed diagnostic sink");
var parallel29=new WindowDiagnosticJournal(capacity:256);
Parallel.For(0,1000,i=>parallel29.Record(i,"WORKER","probe"));
var lines29=parallel29.Snapshot().Split(Environment.NewLine);
Check(lines29.Length==256&&lines29.Select(x=>System.Text.RegularExpressions.Regex.Match(x,@" n=(\d+) ").Groups[1].Value).Distinct().Count()==256,"R29 UI and watchdog writes share a bounded correctly sequenced journal");
Console.WriteLine("R29 window trace and executable resource preference regressions complete.");

// R30: documented Shell callback layouts; no actual Shell/window/input interaction.
static long PackedTray30(int notification,int iconId=1)=>((long)iconId<<16)|(uint)notification;
var packet30=TrayCallbackRouter.Decode(unchecked((int)0xFFF0FF80),PackedTray30(0x202),1);
Check(packet30.Identified&&packet30.Notification==0x202&&packet30.IconId==1&&packet30.Format=="v4","R30 v4 decodes event and icon ID separately from coordinates");
Check(TrayCallbackRouter.IsActivation(packet30),"R30 ordinary left-button release activates the v4 tray icon");
foreach(int event30 in new[]{0x202,0x203,0x400,0x401})
{
    Check(TrayCallbackRouter.IsActivation(TrayCallbackRouter.Decode(0,PackedTray30(event30),1)),"R30 packed activation accepted: "+event30.ToString("X"));
    Check(TrayCallbackRouter.IsActivation(TrayCallbackRouter.Decode(1,event30,1)),"R30 legacy-layout activation accepted independently of negotiated version: "+event30.ToString("X"));
}
foreach(int event30 in new[]{0x200,0x201,0x204,0x205,0x7B,0x404,0x406,0x407,0x207,0xFFFF})
{
    Check(!TrayCallbackRouter.IsActivation(TrayCallbackRouter.Decode(0,PackedTray30(event30),1)),"R30 hover/right/down/balloon/unknown cannot toggle: "+event30.ToString("X"));
}
foreach(int event30 in new[]{0x205,0x7B})
    Check(TrayCallbackRouter.IsContextMenu(TrayCallbackRouter.Decode(0,PackedTray30(event30),1))&&TrayCallbackRouter.IsContextMenu(TrayCallbackRouter.Decode(1,event30,1)),"R30 context menu accepted in both layouts: "+event30.ToString("X"));
var other30=TrayCallbackRouter.Decode(0,PackedTray30(0x202,2),1);
Check(!other30.Identified&&!TrayCallbackRouter.IsActivation(other30),"R30 another packed icon cannot trigger a window action");
Check(!TrayCallbackRouter.Decode(2,0x202,1).Identified,"R30 another legacy icon cannot trigger a window action");
Check(!TrayCallbackRouter.Decode(1,(1L<<40)|PackedTray30(0x202),1).Identified,"R30 malformed high callback bits cannot identify this icon");
Check(!TrayCallbackRouter.Decode(-1,0x202,1).Identified,"R30 invalid legacy identity is rejected");
var queue30=new Queue<Action>();var gate30=new TrayActivationGate();int minimizes30=0,restores30=0;bool reduced30=false;
using(var window30=new DeferredWindowActions(a=>queue30.Enqueue(a),()=>{minimizes30++;reduced30=true;},()=>{restores30++;reduced30=false;}))
{
    void Callback30(long w,long l,double now)
    {
        var packet=TrayCallbackRouter.Decode(w,l,1);
        if(!TrayCallbackRouter.IsActivation(packet))return;
        var action=gate30.Activate(now,0.5,true,reduced30);
        if(action==TrayActivation.Minimize)window30.Minimize();
        else if(action==TrayActivation.Restore)window30.Restore();
    }
    Callback30(0,PackedTray30(0x202),10);Callback30(0,PackedTray30(0x400),10.01);
    Check(queue30.Count==1&&minimizes30==0,"R30 a v4 click defers one owned-window action until the native callback returns");
    queue30.Dequeue()();Callback30(0,PackedTray30(0x203),10.1);Callback30(0,PackedTray30(0x202),10.2);
    Check(reduced30&&minimizes30==1&&queue30.Count==0,"R30 select and double-click duplicates cannot restore immediately after minimizing");
    Callback30(0,PackedTray30(0x400),11);Callback30(0,PackedTray30(0x202),11.01);queue30.Dequeue()();
    Check(!reduced30&&restores30==1,"R30 reversed select/up order restores exactly once on a later click");
    Callback30(2,0x202,12);Callback30(0,PackedTray30(0x406),12.1);
    Check(queue30.Count==0,"R30 wrong icon and hover cannot consume the next legitimate click");
    Callback30(1,0x202,12.2);queue30.Dequeue()();
    Check(reduced30&&minimizes30==2,"R30 legacy callback layout still toggles the same window after packed callbacks");
    window30.Dispose();Callback30(1,0x202,14);
    Check(queue30.Count==0,"R30 exit/disposal stops pending tray window work");
}
Console.WriteLine("R30 notification-area routing and duplicate-callback regressions complete.");

// R31 local host references are not executable identity checks.
foreach(var entry31 in WindowsHostReference.Entries)
{
    Check(ReferenceEquals(WindowsHostReference.Find(entry31.Executable.ToUpperInvariant()),entry31),"R31 host dictionary exact name lookup: "+entry31.Executable);
    Check(WindowsHostReference.Find("fake-"+entry31.Executable) is null&&WindowsHostReference.Find(@"C:\fake\"+entry31.Executable) is null,"R31 host dictionary rejects lookalike and path-as-name: "+entry31.Executable);
    var p31=new ProcessSnapshot{Pid=9131,ParentPid=0,Name=entry31.Executable,CommandLine="SECRET_NOT_IN_REFERENCE"};
    Check(WindowsHostReference.Matches(p31,entry31.Title),"R31 displayed host reference title is searchable: "+entry31.Executable);
    Check(!WindowsHostReference.Matches(p31,"913")&&!WindowsHostReference.Matches(p31,"SECRET_NOT_IN_REFERENCE"),"R31 host alias never broadens PID or argument search: "+entry31.Executable);
    var context31=WindowsComponentContext.Describe(p31);
    Check(context31.IsApplicable&&context31.Report.Contains(entry31.Source)&&!context31.Report.Contains("SECRET_NOT_IN_REFERENCE"),"R31 context keeps the Microsoft source and omits secret arguments: "+entry31.Executable);
}
var svc31=new ProcessSnapshot{Pid=9132,ParentPid=0,Name="svchost.exe",ServiceNames=new[]{"One","Two","Three"},ServiceDisplayNames=new[]{"Primo","Secondo","Terzo"}};
Check(WindowsHostReference.Services(svc31)=="Servizi: Primo, Secondo…","R31 SCM display names are bounded and remain actual snapshot data");
Check(WindowsHostReference.Services(svc31 with {ServiceDisplayNames=Array.Empty<string>()})=="Servizi: One, Two…","R31 SCM technical names are retained when display names are missing");
Check(WindowsHostReference.Services(svc31 with {ServiceDisplayNames=Array.Empty<string>(),ServiceNames=Array.Empty<string>()}).Contains("non disponibile"),"R31 absent SCM acquisition does not invent hosted services");
var analyzer31=new ProcessLens.Semantics.ProcessIntelligenceAnalyzer();
foreach(string name31 in new[]{"prevhost.exe","smartscreen.exe"})
{
    var i31=analyzer31.Analyze(new ProcessSnapshot{Pid=9133,ParentPid=0,Name=name31});
    Check(i31.Role.Contains("dal nome")&&i31.WhatIsIt.Contains("attività corrente non verificate")&&i31.WhyRunning.Contains("sconosciuto"),"R31 name reference cannot prove identity current work or launch cause: "+name31);
}
Console.WriteLine("R31 host references and search boundaries complete.");

Check(WindowsHostReference.ListTitle(svc31,"generic")=="Host servizi · Primo, Secondo…","R31 collapsed service host group exposes actual bounded SCM labels");
Check(WindowsHostReference.Matches(svc31,"Secondo")&&!WindowsHostReference.Matches(svc31,"913"),"R31 search matches actual hosted SCM labels while retaining exact PID boundary");
Check(WindowsHostReference.ListTitle(svc31 with {Name="thirdparty.exe"},"Original app")=="Original app","R31 reference dictionary cannot rename an unrelated application group");
Check(WindowsHostReference.ListTitle(svc31 with {Name="prevhost.exe",ServiceDisplayNames=Array.Empty<string>(),ServiceNames=Array.Empty<string>()},"unknown")=="Host delle anteprime","R31 collapsed preview host has a useful reference title");

// R32: resource-first icon selection and honest per-file placeholders; no Shell calls.
int shellReads32=0;
var original32=new object(); var generic32=new object();
var icon32=NativeIconPreference.ResolveExecutable(()=>original32,()=>{shellReads32++;return generic32;},x=>ReferenceEquals(x,generic32));
Check(ReferenceEquals(icon32.Image,original32)&&shellReads32==0&&icon32.Source=="Risorsa eseguibile","R32 executable resources precede Shell without extra reads");
icon32=NativeIconPreference.ResolveExecutable<object>(()=>throw new IOException("fixture"),()=>{shellReads32++;return original32;},x=>ReferenceEquals(x,generic32));
Check(ReferenceEquals(icon32.Image,original32)&&shellReads32==1&&icon32.Source=="Shell","R32 unavailable resources retain Shell recovery");
icon32=NativeIconPreference.ResolveExecutable<object>(()=>generic32,()=>null,x=>ReferenceEquals(x,generic32));
Check(icon32.Image is null,"R32 absent originals stay unavailable for placeholder selection");
Check(ProcessSymbolCatalog.Find("RuntimeBroker.exe")?.Id=="broker"&&ProcessSymbolCatalog.Find("chrome.exe")?.Id=="chrome","R33 exact executable names select meaningful symbols");
Check(ProcessIconPlaceholder.Pattern(null)>0&&ProcessIconPlaceholder.Pattern("你好.exe")>0,"R33 missing or Unicode names retain visible generated patterns");
Check(ProcessIconPlaceholder.Key(" CHROME.exe ")==ProcessIconPlaceholder.Key("chrome.exe")&&ProcessIconPlaceholder.ColorIndex("chrome.exe")==ProcessIconPlaceholder.ColorIndex("CHROME.EXE"),"R32 placeholder identity and color are case invariant");
Check(Enumerable.Range(0,1000).All(i=>ProcessIconPlaceholder.ColorIndex("fixture"+i+".exe") is >=0 and <8),"R32 palette index remains bounded");
Check(!WindowsProcessIconPolicy.UseSharedWindowsSymbol(IconProcess("explorer.exe",@"C:\Windows\explorer.exe"),win27),"R32 Explorer never receives the shared Windows fallback");
Check(WindowsProcessIconPolicy.UseSharedWindowsSymbol(IconProcess("svchost.exe",@"C:\Windows\System32\svchost.exe"),win27),"R32 generic Windows symbol limited to core components");
var queue32=new List<Action>();var events32=new List<string>();
using(var actions32=new DeferredWindowActions(a=>queue32.Add(a),()=>events32.Add("minimize"),()=>events32.Add("restore"),()=>events32.Add("hide"))) {
 actions32.Restore();actions32.HideToTray();queue32[0]();
 Check(events32.SequenceEqual(new[]{"hide"}),"R32 explicit notification-area hide supersedes queued restore");
 queue32.Clear();actions32.HideToTray();actions32.Restore();queue32[0]();
 Check(events32.SequenceEqual(new[]{"hide","restore"}),"R32 reopen supersedes queued notification-area hide");
}
Console.WriteLine("R32 notification-area and icon policy regressions complete.");

icon32=NativeIconPreference.ResolveExecutable<object>(()=>null,()=>throw new IOException("Shell fixture"),_=>false);
Check(icon32.Image is null,"R32 Shell failure permits a placeholder instead of breaking the process list");
