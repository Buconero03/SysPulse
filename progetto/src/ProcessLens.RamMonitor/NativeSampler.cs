using System.Runtime.InteropServices;
using System.Text;

namespace ProcessLens.RamMonitor;

public sealed record CaptureResult(DateTime BeginUtc, DateTime EndUtc, bool EnumerationComplete,
    string EnumerationError, IReadOnlyList<ProcessReading> Processes, ProcessReading Monitor);

public sealed class NativeSampler
{
    private sealed record Entry(int Pid, int ParentPid, string Name);
    private static readonly QueryBasicDelegate? QueryBasic = LoadQueryBasic();
    private readonly MonitorScope _scope;
    private Dictionary<int, ProcessReading> _tracked = new();
    public NativeSampler(MonitorScope scope) => _scope = scope;

    public CaptureResult Capture()
    {
        DateTime begin = DateTime.UtcNow;
        var entries = Enumerate(out bool complete, out string error);
        // The meter and all its observed children are excluded, even if launched by the GUI.
        var excluded = new HashSet<int> { Environment.ProcessId };
        bool changed;
        do
        {
            changed = false;
            foreach (var entry in entries)
                if (excluded.Contains(entry.ParentPid) && excluded.Add(entry.Pid)) changed = true;
        } while (changed);
        var reads = new Dictionary<int, ProcessReading>();
        ProcessReading Read(Entry e)
        {
            if (!reads.TryGetValue(e.Pid, out var reading)) reads[e.Pid] = reading = Probe(e);
            return reading;
        }
        var included = new Dictionary<int, ProcessReading>();
        var reported = new Dictionary<int, ProcessReading>();
        foreach (var e in entries.Where(e => !excluded.Contains(e.Pid)))
        {
            bool candidate = _scope.IsCandidateName(e.Name);
            if (!candidate && !_tracked.ContainsKey(e.Pid)) continue;
            var reading = Read(e);
            bool prior = _tracked.TryGetValue(e.Pid, out var previous) && reading.StartUtcTicks.HasValue &&
                previous.StartUtcTicks == reading.StartUtcTicks;
            if (prior || (reading.StartUtcTicks.HasValue && _scope.Contains(reading.Path)))
            {
                reading = reading with { Included = true, Association = prior ? previous!.Association : "ExactExecutablePath" };
                included[e.Pid] = reported[e.Pid] = reading;
            }
            else if (candidate || (previous is not null && !reading.StartUtcTicks.HasValue))
                reported[e.Pid] = reading with { Association = reading.StartUtcTicks.HasValue && reading.Path is not null ? "OutsideScope" : "Unresolved" };
        }
        // Same snapshot parent PID plus both creation times. Never join by an executable name alone.
        do
        {
            changed = false;
            foreach (var e in entries)
            {
                if (excluded.Contains(e.Pid) || included.ContainsKey(e.Pid) || !included.TryGetValue(e.ParentPid, out var parent)) continue;
                var reading = Read(e);
                if (MonitorScope.VerifiedChild(reading, parent))
                {
                    reading = reading with { Included = true, Association = "ObservedDescendant:" + parent.Pid + ":" + parent.StartUtcTicks };
                    included[e.Pid] = reported[e.Pid] = reading;
                    changed = true;
                }
                else reported[e.Pid] = reading with { Association = "Unresolved" };
            }
        } while (changed);
        var nextTracked = complete ? new Dictionary<int, ProcessReading>() : new Dictionary<int, ProcessReading>(_tracked);
        foreach (var row in reported.Values)
            if (row.Association == "Unresolved" && _tracked.TryGetValue(row.Pid, out var old)) nextTracked[row.Pid] = old;
        foreach (var row in included.Values) nextTracked[row.Pid] = row;
        _tracked = nextTracked; // Failed reads do not erase an already observed surviving child.
        var own = Probe(new(Environment.ProcessId, 0, System.IO.Path.GetFileName(Environment.ProcessPath) ?? "")) with { Association = "MonitorExcluded", Included = false };
        return new(begin, DateTime.UtcNow, complete, error, reported.Values.OrderBy(r => r.Pid).ToArray(), own);
    }

    private static ProcessReading Probe(Entry entry)
    {
        IntPtr handle = OpenProcess(0x1000, false, (uint)entry.Pid);
        if (handle == IntPtr.Zero) return Missing("OpenProcess=" + Marshal.GetLastWin32Error());
        try
        {
            if (!GetProcessTimes(handle, out ulong created, out _, out _, out _)) return Missing("GetProcessTimes=" + Marshal.GetLastWin32Error());
            long start = DateTime.FromFileTimeUtc(checked((long)created)).Ticks;
            var errors = new List<string>();
            int? handleParent = null;
            if (QueryBasic is not null)
            {
                int status = QueryBasic(handle, 0, out var basic, Marshal.SizeOf<BasicInformation>(), out int returned);
                if (status >= 0 && returned >= Marshal.SizeOf<BasicInformation>() && basic.Pid.ToUInt64() == (uint)entry.Pid && basic.ParentPid.ToUInt64() <= int.MaxValue)
                    handleParent = (int)basic.ParentPid.ToUInt64();
                else errors.Add("ParentQueryStatus=" + status);
            }
            else errors.Add("ParentQueryUnavailable");
            if (entry.ParentPid != 0 && handleParent != entry.ParentPid)
            {
                errors.Add("ParentSnapshotNotVerified");
                handleParent = null;
            }
            var pathBuffer = new StringBuilder(32768); uint length = (uint)pathBuffer.Capacity;
            string? path = QueryFullProcessImageNameW(handle, 0, pathBuffer, ref length) ? pathBuffer.ToString() : null;
            if (path is null) errors.Add("QueryFullProcessImageName=" + Marshal.GetLastWin32Error());
            // Reject an enumerated name that no longer matches the object behind this handle.
            if (path is not null && !System.IO.Path.GetFileName(path).Equals(entry.Name, StringComparison.OrdinalIgnoreCase))
                return Missing("EnumerationIdentityChanged");
            var counters = new Counters { Size = (uint)Marshal.SizeOf<Counters>(), PrivateWorkingSet = UIntPtr.MaxValue };
            bool ex2 = GetProcessMemoryInfo(handle, ref counters, counters.Size);
            bool memory = ex2;
            if (!ex2)
            {
                errors.Add("EX2=" + Marshal.GetLastWin32Error());
                counters = new Counters { Size = (uint)Marshal.OffsetOf<Counters>(nameof(Counters.PrivateWorkingSet)).ToInt32(), PrivateWorkingSet = UIntPtr.MaxValue };
                memory = GetProcessMemoryInfo(handle, ref counters, counters.Size);
                if (!memory) errors.Add("EX=" + Marshal.GetLastWin32Error());
            }
            long? resident = memory ? checked((long)counters.WorkingSet.ToUInt64()) : null;
            long? commit = memory ? checked((long)counters.PrivateUsage.ToUInt64()) : null;
            long? privateResident = memory && ex2 && counters.PrivateWorkingSet != UIntPtr.MaxValue &&
                counters.PrivateWorkingSet.ToUInt64() <= counters.WorkingSet.ToUInt64()
                ? checked((long)counters.PrivateWorkingSet.ToUInt64()) : null;
            if (memory && !privateResident.HasValue) errors.Add("PrivateWorkingSetUnavailable");
            return new(entry.Pid, entry.ParentPid, entry.Name, path, start, DateTime.UtcNow, "Unclassified", false,
                privateResident, resident, commit, string.Join(";", errors)) { HandleParentPid = handleParent };
        }
        finally { CloseHandle(handle); }
        ProcessReading Missing(string reason) => new(entry.Pid, entry.ParentPid, entry.Name, null, null, DateTime.UtcNow,
            "Unresolved", false, null, null, null, reason);
    }

    private static List<Entry> Enumerate(out bool complete, out string error)
    {
        complete = false; error = ""; var result = new List<Entry>();
        IntPtr snapshot = CreateToolhelp32Snapshot(2, 0);
        if (snapshot == new IntPtr(-1)) { error = "CreateToolhelp32Snapshot=" + Marshal.GetLastWin32Error(); return result; }
        try
        {
            var entry = new ProcessEntry { Size = (uint)Marshal.SizeOf<ProcessEntry>(), Exe = "" };
            if (!Process32FirstW(snapshot, ref entry))
            {
                int code = Marshal.GetLastWin32Error(); complete = code == 18;
                if (!complete) error = "Process32First=" + code;
                return result;
            }
            do
            {
                if (entry.Pid > 0) result.Add(new(checked((int)entry.Pid), checked((int)entry.ParentPid), entry.Exe));
                entry.Size = (uint)Marshal.SizeOf<ProcessEntry>();
            } while (Process32NextW(snapshot, ref entry));
            int last = Marshal.GetLastWin32Error(); complete = last == 18;
            if (!complete) error = "Process32Next=" + last;
            return result;
        }
        finally { CloseHandle(snapshot); }
    }

    // The native query is optional and dynamically resolved, as required by its Windows contract.
    // If unavailable, exact-path roots remain measurable but descendant edges are not asserted.
    private static QueryBasicDelegate? LoadQueryBasic()
    {
        if (!OperatingSystem.IsWindows() || !NativeLibrary.TryLoad("ntdll.dll", out var module)) return null;
        if (NativeLibrary.TryGetExport(module, "NtQueryInformationProcess", out var address))
            return Marshal.GetDelegateForFunctionPointer<QueryBasicDelegate>(address);
        NativeLibrary.Free(module);
        return null;
    }
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int QueryBasicDelegate(IntPtr process, int kind, out BasicInformation information, int size, out int returned);
    [StructLayout(LayoutKind.Sequential)]
    private struct BasicInformation
    {
        public int ExitStatus; public IntPtr Peb; public UIntPtr Affinity; public int Priority; public UIntPtr Pid, ParentPid;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry
    {
        public uint Size, Usage, Pid; public UIntPtr DefaultHeapId; public uint ModuleId, Threads, ParentPid;
        public int Priority; public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string Exe;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct Counters
    {
        public uint Size, PageFaults;
        public UIntPtr PeakWorkingSet, WorkingSet, PeakPaged, Paged, PeakNonPaged, NonPaged, Pagefile, PeakPagefile, PrivateUsage, PrivateWorkingSet;
        public ulong SharedCommit;
    }
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint pid);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true)] private static extern bool Process32FirstW(IntPtr snapshot, ref ProcessEntry entry);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true)] private static extern bool Process32NextW(IntPtr snapshot, ref ProcessEntry entry);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetProcessTimes(IntPtr process, out ulong creation, out ulong exit, out ulong kernel, out ulong user);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern bool QueryFullProcessImageNameW(IntPtr process, uint flags, StringBuilder path, ref uint size);
    [DllImport("psapi.dll", SetLastError = true)] private static extern bool GetProcessMemoryInfo(IntPtr process, ref Counters counters, uint size);
}
