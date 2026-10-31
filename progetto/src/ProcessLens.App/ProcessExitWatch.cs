using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ProcessLens.App;

// Read-only observation of one process object, not inference from a missing/filtered row.
// The same handle verifies creation time and receives the Windows exit signal.
internal sealed class ProcessExitWatch : IDisposable
{
    private const uint Synchronize = 0x00100000;
    private const uint QueryLimitedInformation = 0x1000;
    private sealed class ProcessWaitHandle : WaitHandle
    {
        public ProcessWaitHandle(SafeWaitHandle handle) { SafeWaitHandle = handle; }
    }

    private readonly ProcessWaitHandle _handle;
    private RegisteredWaitHandle? _registration;
    private int _disposed;
    public ProcessExitIdentity Identity { get; }

    private ProcessExitWatch(ProcessWaitHandle handle, ProcessExitIdentity identity)
    { _handle = handle; Identity = identity; }

    public static ProcessExitWatch? TryCreate(ProcessExitIdentity identity, Action<ProcessExitIdentity> exited)
    {
        IntPtr raw = OpenProcess(Synchronize | QueryLimitedInformation, false, (uint)identity.Pid);
        if (raw == IntPtr.Zero) return null;
        var handle = new ProcessWaitHandle(new SafeWaitHandle(raw, ownsHandle: true));
        ProcessExitWatch? watch = null;
        try
        {
            if (!GetProcessTimes(handle.SafeWaitHandle, out long created, out _, out _, out _) ||
                DateTime.FromFileTimeUtc(created).Ticks != identity.StartUtcTicks)
            { handle.Dispose(); return null; }
            var activeWatch = new ProcessExitWatch(handle, identity);
            watch = activeWatch;
            activeWatch._registration = ThreadPool.RegisterWaitForSingleObject(handle, (_, timedOut) =>
            {
                if (!timedOut && Volatile.Read(ref activeWatch._disposed) == 0) exited(identity);
            }, null, Timeout.Infinite, executeOnlyOnce: true);
            return watch;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or
            System.ComponentModel.Win32Exception or NotSupportedException)
        {
            if (watch is not null) watch.Dispose(); else handle.Dispose();
            return null;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _registration?.Unregister(null);
        _handle.Dispose();
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint pid);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessTimes(SafeWaitHandle handle, out long creation, out long exit,
        out long kernel, out long user);
}
