using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using ProcessLens.Core;
using ProcessLens.Windows;

static void Check(bool ok, string label)
{ if (!ok) throw new Exception("FAIL system: " + label); Console.WriteLine("PASS system: " + label); }
static void Reject(Action action, string label)
{
    try { action(); } catch (InvalidDataException) { Check(true, label); return; }
    throw new Exception("FAIL system: " + label);
}
// Constants from the published Windows SDK ABI, independent of production Marshal.OffsetOf.
bool wide = IntPtr.Size == 8;
int header = wide ? 256 : 184, namePointer = wide ? 64 : 60,
    pidOffset = wide ? 80 : 68, parentOffset = wide ? 88 : 72,
    handlesOffset = wide ? 96 : 76, sessionOffset = wide ? 100 : 80,
    residentOffset = wide ? 144 : 104, stride = 320;
Check(Marshal.SizeOf<WindowsSystemProcessSource.ProcessInfo>() == header, "published process header size");
Check(Marshal.OffsetOf<WindowsSystemProcessSource.ProcessInfo>("ImageName").ToInt32() == 56 &&
    Marshal.OffsetOf<WindowsSystemProcessSource.ProcessInfo>("UniqueProcessId").ToInt32() == pidOffset &&
    Marshal.OffsetOf<WindowsSystemProcessSource.ProcessInfo>("WorkingSetSize").ToInt32() == residentOffset,
    "published Unicode PID and Working Set offsets");
Check(Marshal.SizeOf<WindowsSystemProcessSource.PerformanceInfo>() == (wide ? 104 : 56), "PSAPI performance ABI size");
Check(Marshal.OffsetOf<WindowsSystemProcessSource.PerformanceInfo>("PageSize").ToInt32() == (wide ? 80 : 40), "PSAPI page-size offset");
IntPtr buffer = Marshal.AllocHGlobal(stride * 2);
try
{
    void Reset()
    {
        Marshal.Copy(new byte[stride * 2], 0, buffer, stride * 2);
        Marshal.WriteInt32(buffer, 0, stride); // PID 0, genuine zero placeholder for idle.
        IntPtr row = IntPtr.Add(buffer, stride);
        Marshal.WriteInt32(row, 4, 7);
        string name = "Memory Compression"; byte[] text = Encoding.Unicode.GetBytes(name);
        Marshal.WriteInt16(row, 56, (short)text.Length); Marshal.WriteInt16(row, 58, (short)text.Length);
        Marshal.WriteIntPtr(row, namePointer, IntPtr.Add(row, header));
        Marshal.Copy(text, 0, IntPtr.Add(row, header), text.Length);
        Marshal.WriteIntPtr(row, pidOffset, new IntPtr(44)); Marshal.WriteIntPtr(row, parentOffset, new IntPtr(4));
        Marshal.WriteInt32(row, handlesOffset, 9); Marshal.WriteInt32(row, sessionOffset, 0);
        Marshal.WriteIntPtr(row, residentOffset, new IntPtr(12 * 1048576));
    }
    Reset();
    var rows = WindowsSystemProcessSource.ParseNative(buffer, stride * 2, default);
    Check(rows.Count == 2 && rows[0].Pid == 0 && rows[0].WorkingSetBytes is null,
        "idle retained and placeholder RAM does not become measured zero");
    Check(rows[1].Name == "Memory Compression" && rows[1].Pid == 44 && rows[1].ParentPid == 4 &&
        rows[1].Threads == 7 && rows[1].Handles == 9 && rows[1].WorkingSetBytes == 12 * 1048576,
        "independent raw fixture retains internal process name PID counters and physical total Working Set");
    Reset(); Marshal.WriteInt32(buffer, 0, 8);
    Reject(() => WindowsSystemProcessSource.ParseNative(buffer, stride * 2, default), "reject header overlap");
    Reset(); Marshal.WriteInt32(buffer, 0, int.MaxValue);
    Reject(() => WindowsSystemProcessSource.ParseNative(buffer, stride * 2, default), "reject out-of-range next offset");
    Reset(); Marshal.WriteIntPtr(IntPtr.Add(buffer, stride), namePointer, IntPtr.Add(buffer, stride * 2));
    Reject(() => WindowsSystemProcessSource.ParseNative(buffer, stride * 2, default), "reject name outside record");
    Reset(); Marshal.WriteInt16(IntPtr.Add(buffer, stride), 56, 3);
    Reject(() => WindowsSystemProcessSource.ParseNative(buffer, stride * 2, default), "reject odd Unicode byte length");
    Reset(); Marshal.WriteIntPtr(IntPtr.Add(buffer, stride), pidOffset, IntPtr.Zero);
    Reject(() => WindowsSystemProcessSource.ParseNative(buffer, stride * 2, default), "reject duplicate PID");
    Reset(); Reject(() => WindowsSystemProcessSource.ParseNative(buffer, header - 1, default), "reject truncated header");
    using var cancel = new CancellationTokenSource(); cancel.Cancel();
    try { WindowsSystemProcessSource.ParseNative(buffer, stride * 2, cancel.Token); throw new Exception("cancellation ignored"); }
    catch (OperationCanceledException) { Check(true, "cancellation stops inventory parsing"); }
}
finally { Marshal.FreeHGlobal(buffer); }
Check(WindowsSystemProcessSource.PageBytes(12, 4096) == 49152 &&
    WindowsSystemProcessSource.PageBytes(0, 4096) == 0 &&
    WindowsSystemProcessSource.PageBytes(ulong.MaxValue, 4096) is null &&
    WindowsSystemProcessSource.PageBytes(5, 0) is null, "page units preserve zero and reject overflow or unknown page size");
var unknown = new SystemProcessEntry(512, 4, "example.exe", 2, null, null, null);
Check(SystemProcessPresentation.WindowsGroup(unknown with { Name = "Memory Compression" }, @"C:\Windows") &&
    !SystemProcessPresentation.WindowsGroup(unknown with { Name = "Memory Compression.exe" }, @"C:\Windows"),
    "internal names exact and suffix lookalike not classified as compression");
Check(SystemProcessPresentation.UnderWindows(@"D:\Windows\System32\csrss.exe", @"D:\Windows") &&
    !SystemProcessPresentation.UnderWindows(@"D:\Windows-old\csrss.exe", @"D:\Windows") &&
    !SystemProcessPresentation.UnderWindows(@"D:\Windows\..\Users\fake.exe", @"D:\Windows"),
    "Windows directory boundaries and traversal rejected");
Check(SystemProcessPresentation.Origin(unknown with { Name = "lsass.exe", Path = @"C:\Users\test\lsass.exe" }, @"C:\Windows")
    .Contains("non verificata"), "familiar name in user directory never certifies Windows ownership");
DateTime start = new(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc);
var observed = unknown with { StartUtc = start };
var snapshot = new ProcessSnapshot { Pid = 512, ParentPid = 4, Name = "example.exe", StartTime = start.ToLocalTime() };
Check(SystemProcessPresentation.SameInstance(observed, snapshot) &&
    !SystemProcessPresentation.SameInstance(observed, snapshot with { StartTime = start.AddSeconds(1) }) &&
    !SystemProcessPresentation.SameInstance(observed, snapshot with { Name = "other.exe" }) &&
    !SystemProcessPresentation.SameInstance(observed with { StartUtc = null }, snapshot),
    "analysis navigation requires exact PID executable and known creation time across UTC and local time");
foreach (string culture in new[] { "it-IT", "en-US" })
{
    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
    string text = SystemProcessPresentation.MiB(26L * 1048576);
    Check(text == (culture == "it-IT" ? "26,0 MiB" : "26.0 MiB") &&
        SystemProcessPresentation.MiB(null) == "N/D", "culture and unavailable memory " + culture);
    string memory = SystemProcessPresentation.MemoryReport(new(null, null, null, null, 0, null, "errore originale"));
    Check(memory.Contains("non sommarli") && memory.Contains("errore originale") && memory.Contains("N/D"),
        "global kernel counters retain source errors and cannot become process totals " + culture);
}
if (OperatingSystem.IsWindows())
{
    // Read-only smoke check against a second API; no process creation, termination, driver or privilege change.
    using var self = Process.GetCurrentProcess();
    var live = new WindowsSystemProcessSource().Capture(default);
    var mine = live.Processes.Single(p => p.Pid == self.Id);
    Check(live.Available && mine.Name.Equals(self.ProcessName + ".exe", StringComparison.OrdinalIgnoreCase) &&
        mine.StartUtc == self.StartTime.ToUniversalTime(), "live Windows inventory agrees with independent self PID name and creation time");
    Check(live.Processes.Any(p => p.Pid == 0) && live.Processes.Any(p => p.Pid == 4), "live Windows idle and System entries remain visible");
    Check(live.Memory.PhysicalTotal > 0 && live.Memory.KernelNonpaged >= 0, "live PSAPI memory counters available");
}
else Console.WriteLine("SKIP live Windows read: non-Windows host.");
Console.WriteLine("System process tests complete.");
