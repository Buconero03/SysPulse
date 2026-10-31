using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ProcessLens.Windows;

/// <summary>Current GUI process only. No agent/service aggregation, GC or working-set trimming.</summary>
public sealed record WindowsSelfResources(int Pid, DateTime CapturedUtc, long? PrivateResident,
    long? Resident, long? PrivateCommit)
{
    public static WindowsSelfResources Capture()
    {
        long? resident = null, commit = null, privateResident = null;
        using var self = Process.GetCurrentProcess();
        try { resident = self.WorkingSet64; } catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
        try { commit = self.PrivateMemorySize64; } catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
        var counters = new Counters { Size = (uint)Marshal.SizeOf<Counters>(), PrivateWorkingSet = nuint.MaxValue };
        // Sentinel detects an older API returning only the EX prefix successfully.
        if (GetProcessMemoryInfo(GetCurrentProcess(), ref counters, counters.Size))
        {
            resident = checked((long)counters.WorkingSet);
            commit = checked((long)counters.PrivateUsage);
            if (counters.PrivateWorkingSet <= counters.WorkingSet)
                privateResident = checked((long)counters.PrivateWorkingSet);
        }
        return new(Environment.ProcessId, DateTime.UtcNow, privateResident, resident, commit);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Counters
    {
        public uint Size, PageFaults;
        public nuint PeakWorkingSet, WorkingSet, PeakPaged, Paged, PeakNonPaged, NonPaged,
            PagefileUsage, PeakPagefileUsage, PrivateUsage, PrivateWorkingSet;
        public ulong SharedCommit;
    }
    [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentProcess();
    [DllImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessMemoryInfo(IntPtr process, ref Counters counters, uint size);
}
