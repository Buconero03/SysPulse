using System.Runtime.InteropServices;
using System.Diagnostics;

namespace ProcessLens.Windows;

/// <summary>
/// Samples three distinct metrics. Never substitutes committed private bytes for
/// private resident bytes. EX2 is supported on current Windows 10/11 builds.
/// </summary>
internal static class WindowsMemorySampler
{
    internal readonly record struct Sample(
        long WorkingSetBytes, bool WorkingSetAvailable,
        long PrivateWorkingSetBytes, bool PrivateWorkingSetAvailable,
        long PrivateCommitBytes, bool PrivateCommitAvailable)
    {
        public DateTime? StartUtc { get; init; }
        public double SampledAt { get; init; }
        public DateTime SampledUtc { get; init; }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ProcessMemoryCountersEx2
    {
        public uint cb;
        public uint PageFaultCount;
        public nuint PeakWorkingSetSize;
        public nuint WorkingSetSize;
        public nuint QuotaPeakPagedPoolUsage;
        public nuint QuotaPagedPoolUsage;
        public nuint QuotaPeakNonPagedPoolUsage;
        public nuint QuotaNonPagedPoolUsage;
        public nuint PagefileUsage;
        public nuint PeakPagefileUsage;
        public nuint PrivateUsage;
        public nuint PrivateWorkingSetSize;
        public ulong SharedCommitUsage;
    }

    [DllImport("psapi.dll", EntryPoint = "GetProcessMemoryInfo", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessMemoryInfo(
        IntPtr process, ref ProcessMemoryCountersEx2 counters, uint size);

    public static Sample Capture(int pid)
    {
        if (pid <= 0) return default;
        IntPtr handle = IntPtr.Zero;
        try
        {
            handle = NativeMethods.OpenProcess(
                NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)pid);
            if (handle == IntPtr.Zero) return default;
            DateTime? start = GetProcessTimes(handle, out long creation, out _, out _, out _)
                ? DateTime.FromFileTimeUtc(creation) : null;
            return Capture(handle) with { StartUtc = start };
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException
                                       or OverflowException or SEHException)
        {
            return default;
        }
        finally
        {
            if (handle != IntPtr.Zero) NativeMethods.CloseHandle(handle);
        }
    }
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessTimes(IntPtr process, out long creation, out long exit, out long kernel, out long user);

    internal delegate bool CounterReader(IntPtr process, ref ProcessMemoryCountersEx2 counters, uint size);
    // The caller owns the handle. No second OpenProcess and no page-table walk in the live path.
    internal static Sample Capture(IntPtr handle) => Capture(handle, GetProcessMemoryInfo);
    internal static Sample Capture(IntPtr handle, CounterReader reader)
    {
        double at = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
        DateTime utc = DateTime.UtcNow;
        Sample Missing() => new() { SampledAt = at, SampledUtc = utc };
        if (handle == IntPtr.Zero) return Missing();
        try
        {
            var counters = new ProcessMemoryCountersEx2
            {
                cb = (uint)Marshal.SizeOf<ProcessMemoryCountersEx2>(),
                // Older systems may fill only the EX prefix even when the call succeeds.
                PrivateWorkingSetSize = nuint.MaxValue
            };
            if (!reader(handle, ref counters, counters.cb))
            {
                counters = new() { cb = (uint)Marshal.OffsetOf<ProcessMemoryCountersEx2>(nameof(ProcessMemoryCountersEx2.PrivateWorkingSetSize)),
                    PrivateWorkingSetSize = nuint.MaxValue };
                if (!reader(handle, ref counters, counters.cb)) return Missing();
                // EX has no private Working Set member, regardless of the buffer contents.
                counters.PrivateWorkingSetSize = nuint.MaxValue;
            }
            bool ws = counters.WorkingSetSize <= long.MaxValue;
            bool privateWs = ws && counters.PrivateWorkingSetSize != nuint.MaxValue &&
                counters.PrivateWorkingSetSize <= counters.WorkingSetSize;
            bool commit = counters.PrivateUsage <= long.MaxValue;
            return new(ws ? (long)counters.WorkingSetSize : 0, ws,
                privateWs ? (long)counters.PrivateWorkingSetSize : 0, privateWs,
                commit ? (long)counters.PrivateUsage : 0, commit) { SampledAt = at, SampledUtc = utc };
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or OverflowException or SEHException)
        { return Missing(); }
    }

}
