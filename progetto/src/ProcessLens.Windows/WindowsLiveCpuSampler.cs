using System.Diagnostics;
using System.Runtime.InteropServices;
using ProcessLens.Core;

namespace ProcessLens.Windows;

/// <summary>Cheap CPU and memory reads on one handle per PID. Not thread-safe; callers serialize capture/reset.</summary>
public sealed class WindowsLiveCpuSampler
{
    public sealed record Batch(IReadOnlyDictionary<int, LiveCpuSample> Processes, CpuTelemetry System)
    {
        public IReadOnlyDictionary<int, LiveMemorySample> Memory { get; init; } = new Dictionary<int, LiveMemorySample>();
        public IReadOnlyDictionary<int, LiveNetworkSample> Network { get; init; } = new Dictionary<int, LiveNetworkSample>();
    }
    private readonly Dictionary<int, CpuIntervalTracker> _processes = new();
    private readonly CpuIntervalTracker _system = new();
    private readonly WindowsLiveNetworkSampler _network = new();
    private (ulong Idle, ulong Total)? _oldSystem;
    private static readonly DateTime SystemIdentity = DateTime.UnixEpoch;

    public void Reset() { _processes.Clear(); _system.Reset(); _network.Reset(); _oldSystem = null; }

    public Batch Capture(IReadOnlyCollection<int> pids)
    {
        int cpus = checked((int)GetActiveProcessorCount(0xffff));
        var requested = pids.Where(x => x > 0).ToHashSet();
        foreach (int pid in _processes.Keys.Where(x => !requested.Contains(x)).ToArray()) _processes.Remove(pid);
        var result = new Dictionary<int, LiveCpuSample>(requested.Count);
        var memory = new Dictionary<int, LiveMemorySample>(requested.Count);
        var processIo = new Dictionary<int, (DateTime? StartUtc, ulong OtherIoBytes, bool IoAvailable)>(requested.Count);
        foreach (int pid in requested)
        {
            IntPtr handle = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)pid);
            try
            {
                if (handle != IntPtr.Zero && GetProcessTimes(handle, out ulong creation, out _, out ulong kernel, out ulong user))
                {
                    if (!_processes.TryGetValue(pid, out var tracker)) _processes[pid] = tracker = new();
                    var startUtc = DateTime.FromFileTimeUtc(checked((long)creation));
                    // Timestamp adjacent to this PID's read; no shared denominator from another scan.
                    result[pid] = tracker.Sample(pid, startUtc,
                        checked((long)(kernel + user)), Clock(), cpus);
                    var m = WindowsMemorySampler.Capture(handle);
                    memory[pid] = new(pid, result[pid].StartUtc, m.WorkingSetBytes, m.WorkingSetAvailable,
                        m.PrivateWorkingSetBytes, m.PrivateWorkingSetAvailable, m.PrivateCommitBytes,
                        m.PrivateCommitAvailable, m.SampledAt, m.SampledUtc);
                    bool ioOk = GetProcessIoCounters(handle, out var ioCounters);
                    processIo[pid] = (startUtc, ioOk ? ioCounters.OtherTransferCount : 0UL, ioOk);
                    continue;
                }
                memory[pid] = new(pid, null, 0, false, 0, false, 0, false, Clock(), DateTime.UtcNow);
                processIo[pid] = (null, 0UL, false);
                _processes.Remove(pid); // A failed read breaks continuity, never becomes zero CPU.
                result[pid] = new(pid, null, false, 0, 0, 0, 0, 0, false, cpus);
            }
            finally { if (handle != IntPtr.Zero) NativeMethods.CloseHandle(handle); }
        }

        var network = _network.Capture(requested, processIo, Clock(), DateTime.UtcNow);

        LiveCpuSample? system = null;
        // GetSystemTimes covers only the calling processor group above 64 logical CPUs.
        // Do not label that partial result as whole-machine CPU.
        if (cpus is > 0 and <= 64 && NativeMethods.GetSystemTimes(out var idle, out var kernelTime, out var userTime))
        {
            ulong total = kernelTime.AsUInt64 + userTime.AsUInt64;
            ulong idleTicks = idle.AsUInt64;
            if (total >= idleTicks && (_oldSystem is not { } old ||
                (total >= old.Total && idleTicks >= old.Idle && total - old.Total >= idleTicks - old.Idle)))
                system = _system.Sample(0, SystemIdentity, checked((long)(total - idleTicks)), Clock(), cpus);
            else _system.Reset();
            _oldSystem = (idleTicks, total);
        }
        else { _oldSystem = null; _system.Reset(); }
        int sampled = result.Values.Count(x => x.Available);
        return new(result, new(system?.Available == true, system?.Percent ?? 0,
            system?.Average ?? 0, system?.Peak ?? 0, system?.IntervalSeconds ?? 0,
            cpus, sampled, result.Count - sampled)) { Memory = memory, Network = network };
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    private static double Clock() => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint GetActiveProcessorCount(ushort group);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessTimes(IntPtr process, out ulong creation, out ulong exit, out ulong kernel, out ulong user);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessIoCounters(IntPtr hProcess, out IO_COUNTERS lpIoCounters);
}
