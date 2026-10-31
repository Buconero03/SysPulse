using System.Globalization;
using System.Runtime.InteropServices;
using ProcessLens.Core;
using ProcessLens.App;
using ProcessLens.Windows;

static void Check(bool ok, string label)
{ if (!ok) throw new Exception("FAIL R24 " + label); Console.WriteLine("PASS R24 " + label); }
var start = new DateTime(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc);
var original = new ProcessSnapshot { Pid = 42, ParentPid = 1, Name = "fixture.exe", StartTime = start,
    WorkingSetBytes = 999, WorkingSetAvailable = true, PrivateWorkingSetBytes = 888, PrivateWorkingSetAvailable = true,
    PrivateBytes = 7000, PrivateCommitAvailable = true, CpuPercent = 23, CpuSampleAvailable = true,
    CommandLine = "fixture --secret=preserved", IsDeepInspected = true, TcpListeningCount = 2 };
var live = new LiveMemorySample(42, start, 400, true, 200, true, 900, true, 100, start.AddSeconds(100));
foreach (string culture in new[] { "it-IT", "en-US" })
{
    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
    var updated = live.Apply(original);
    Check(updated.WorkingSetBytes == 400 && updated.PrivateWorkingSetBytes == 200 && updated.PrivateBytes == 900,
        "resident, private resident and commit stay distinct [" + culture + "]");
    Check(updated.CpuPercent == 23 && updated.CpuSampleAvailable && updated.SemanticIdentityKey == original.SemanticIdentityKey &&
        updated.CommandLine == original.CommandLine && original.WorkingSetBytes == 999,
        "memory update preserves CPU, evidence and original snapshot [" + culture + "]");
    Check(updated.MemorySampledUtc == live.CapturedUtc && updated.MemorySampledAtMonotonicSeconds == 100, "exact sample time retained [" + culture + "]");
    Check(live.IsFresh(100) && live.IsFresh(106) && !live.IsFresh(106.001) && !live.IsFresh(99) &&
        !live.IsFresh(double.NaN) && !(live with { CapturedAtMonotonicSeconds = double.NaN }).IsFresh(100), "stale and invalid clocks rejected [" + culture + "]");
    foreach (var other in new[] { original with { Pid = 43 }, original with { StartTime = start.AddSeconds(1) }, original with { StartTime = null } })
        Check(!live.Apply(other).WorkingSetAvailable && !live.Apply(other).PrivateCommitAvailable, "PID and known creation time required [" + culture + "]");
    Check(!(live with { StartUtc = null }).Apply(original).WorkingSetAvailable, "unknown sample identity cannot attach [" + culture + "]");
    Check(ReferenceEquals((live with { CapturedAtMonotonicSeconds = 99 }).Apply(updated), updated), "older fast sample cannot overwrite newer inventory read [" + culture + "]");
    Check((live with { CapturedAtMonotonicSeconds = 101, PrivateWorkingSetBytes = 100 }).Apply(updated).PrivateWorkingSetBytes == 100,
        "newer fast sample supersedes slow inventory [" + culture + "]");
    var missing = LiveMemorySample.Unavailable(updated);
    Check(missing.MemoryDisplayText == "N/D" && !missing.PrivateCommitAvailable && missing.MemorySampledUtc is null,
        "failed or suspended acquisition clears old data without measured zero [" + culture + "]");
    var zero = (live with { WorkingSetBytes = 0, PrivateWorkingSetBytes = 0, PrivateCommitBytes = 0 }).Apply(original);
    Check(zero.PrivateWorkingSetAvailable && zero.MemoryDisplayText != "N/D" && zero.MemoryDisplayMb == 0, "genuine zero retained [" + culture + "]");
    var bad = (live with { PrivateWorkingSetBytes = 401, PrivateCommitBytes = -1 }).Apply(original);
    Check(!bad.PrivateWorkingSetAvailable && bad.WorkingSetAvailable && !bad.PrivateCommitAvailable, "invalid private value falls back to explicitly total WS [" + culture + "]");
}
Check(MonitorRefreshPolicy.LiveMetricsInterval == TimeSpan.FromSeconds(1) && MonitorRefreshPolicy.NormalProcessInterval == TimeSpan.FromSeconds(15) &&
    MonitorRefreshPolicy.HighLoadProcessInterval == TimeSpan.FromSeconds(45), "one-second counters independent of bounded metadata scheduling");
var history = new MemoryObservationBuffer();
history.Add(start, 1); history.Add(start, 999); history.Add(start.AddSeconds(-1), 999); history.Add(start.AddSeconds(1), -1);
Check(history.Count == 1 && history.ToArray()[0].Bytes == 1, "duplicate, out-of-order and invalid trend inputs ignored");
for (int i = 1; i <= 10000; i++) history.Add(start.AddSeconds(i), i);
Check(history.Count == 160 && history.ToArray()[0].Bytes == 9841, "one-second history remains bounded at 160 samples");
var callbacks = new Queue<Action>(); int minimized = 0, restored = 0;
using (var actions = new DeferredWindowActions(a => callbacks.Enqueue(a), () => minimized++, () => restored++))
{
    actions.Minimize(); actions.Minimize();
    Check(minimized == 0 && callbacks.Count == 1, "Closing only queues one minimize after returning");
    callbacks.Dequeue()(); Check(minimized == 1, "queued minimize executes once");
    actions.Minimize(); actions.Restore(); callbacks.Dequeue()();
    Check(minimized == 1 && restored == 1, "restore supersedes pending minimize without losing reopen");
    actions.Restore(); actions.Minimize(); callbacks.Dequeue()();
    Check(minimized == 2 && restored == 1, "latest explicit minimize wins a pending restore");
    actions.Minimize(); actions.Dispose(); callbacks.Dequeue()(); actions.Restore();
    Check(minimized == 2 && callbacks.Count == 0, "real exit or disposal cancels queued changes");
}
Check(WindowLifetimePolicy.Close(false, true) == WindowCloseAction.MinimizeToTaskbar &&
    WindowLifetimePolicy.Close(false, false) == WindowCloseAction.MinimizeToTaskbar &&
    WindowLifetimePolicy.Close(true, false) == WindowCloseAction.Exit, "X keeps taskbar fallback and explicit exit remains possible");
// SDK layout is an independent fixture, not computed from the structure under test.
if (IntPtr.Size == 8)
    Check(Marshal.SizeOf<WindowsMemorySampler.ProcessMemoryCountersEx2>() == 96 &&
        Marshal.OffsetOf<WindowsMemorySampler.ProcessMemoryCountersEx2>("WorkingSetSize").ToInt32() == 16 &&
        Marshal.OffsetOf<WindowsMemorySampler.ProcessMemoryCountersEx2>("PrivateUsage").ToInt32() == 72 &&
        Marshal.OffsetOf<WindowsMemorySampler.ProcessMemoryCountersEx2>("PrivateWorkingSetSize").ToInt32() == 80,
        "PSAPI EX2 x64 size and offsets match Windows SDK fixture");
var handle = new IntPtr(123); int calls = 0;
bool Full(IntPtr h, ref WindowsMemorySampler.ProcessMemoryCountersEx2 c, uint size)
{
    calls++; Check(h == handle && size == Marshal.SizeOf<WindowsMemorySampler.ProcessMemoryCountersEx2>(), "same borrowed handle and explicit EX2 size");
    c.WorkingSetSize = 400; c.PrivateWorkingSetSize = 200; c.PrivateUsage = 900; return true;
}
var sample = WindowsMemorySampler.Capture(handle, Full);
Check(calls == 1 && sample.WorkingSetBytes == 400 && sample.PrivateWorkingSetBytes == 200 && sample.PrivateCommitBytes == 900,
    "one read yields all three native counters without another process open");
bool PrefixOnly(IntPtr h, ref WindowsMemorySampler.ProcessMemoryCountersEx2 c, uint size)
{ c.WorkingSetSize = 400; c.PrivateUsage = 900; return true; }
sample = WindowsMemorySampler.Capture(handle, PrefixOnly);
Check(sample.WorkingSetAvailable && !sample.PrivateWorkingSetAvailable && sample.PrivateCommitBytes == 900,
    "success with only EX prefix cannot invent zero private WS");
calls = 0;
bool Fallback(IntPtr h, ref WindowsMemorySampler.ProcessMemoryCountersEx2 c, uint size)
{
    calls++; if (calls == 1) return false;
    Check(size == (IntPtr.Size == 8 ? 80 : 44), "fallback uses SDK EX byte count");
    c.WorkingSetSize = 123; c.PrivateUsage = 456; c.PrivateWorkingSetSize = 0; return true;
}
sample = WindowsMemorySampler.Capture(handle, Fallback);
Check(calls == 2 && sample.WorkingSetBytes == 123 && !sample.PrivateWorkingSetAvailable && sample.PrivateCommitBytes == 456,
    "EX fallback retains total WS and commit without false private zero");
bool Denied(IntPtr h, ref WindowsMemorySampler.ProcessMemoryCountersEx2 c, uint size) => false;
sample = WindowsMemorySampler.Capture(handle, Denied);
Check(!sample.WorkingSetAvailable && !sample.PrivateWorkingSetAvailable && !sample.PrivateCommitAvailable && sample.SampledUtc != default,
    "denied acquisition stays unavailable with its attempted sample time");
sample = WindowsMemorySampler.Capture(IntPtr.Zero, Full);
Check(!sample.WorkingSetAvailable && calls == 2, "invalid handle cannot reach the counter reader");
bool NativeZero(IntPtr h, ref WindowsMemorySampler.ProcessMemoryCountersEx2 c, uint size)
{ c.WorkingSetSize = c.PrivateWorkingSetSize = c.PrivateUsage = 0; return true; }
sample = WindowsMemorySampler.Capture(handle, NativeZero);
Check(sample.WorkingSetAvailable && sample.PrivateWorkingSetAvailable && sample.PrivateWorkingSetBytes == 0, "native explicit zero is measured");
bool NativeInvalid(IntPtr h, ref WindowsMemorySampler.ProcessMemoryCountersEx2 c, uint size)
{ c.WorkingSetSize = 100; c.PrivateWorkingSetSize = 101; c.PrivateUsage = 900; return true; }
sample = WindowsMemorySampler.Capture(handle, NativeInvalid);
Check(sample.WorkingSetAvailable && !sample.PrivateWorkingSetAvailable && sample.PrivateCommitBytes == 900,
    "native private WS above total cannot become a valid private counter");
if (IntPtr.Size == 8)
{
    bool Overflow(IntPtr h, ref WindowsMemorySampler.ProcessMemoryCountersEx2 c, uint size)
    { c.WorkingSetSize = nuint.MaxValue; c.PrivateWorkingSetSize = 1; c.PrivateUsage = 900; return true; }
    sample = WindowsMemorySampler.Capture(handle, Overflow);
    Check(!sample.WorkingSetAvailable && !sample.PrivateWorkingSetAvailable && sample.PrivateCommitAvailable && sample.PrivateCommitBytes == 900,
        "unsigned overflow invalidates resident counters without losing valid commit");
}
if (OperatingSystem.IsWindows())
{
    var own = System.Diagnostics.Process.GetCurrentProcess();
    var batch = new WindowsLiveCpuSampler().Capture(new[] { own.Id });
    Check(batch.Memory.TryGetValue(own.Id, out var m) && m.StartUtc == own.StartTime.ToUniversalTime() &&
        m.WorkingSetAvailable && m.WorkingSetBytes > 0 && m.CapturedUtc != default,
        "WINDOWS owned-process native CPU/RAM batch identifies exact creation and samples memory");
}
else Console.WriteLine("NOT RUN: real Windows handles, PSAPI, tray, WPF and Task Manager comparison (Linux host).");
Console.WriteLine("R24 live metrics and window command regressions complete.");
