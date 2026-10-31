using System.Runtime.InteropServices;

namespace ProcessLens.Guardian.Service;

// Snapshot of parent-PIDs, not sufficient by itself to prove ancestry after PID reuse.
internal static class ProcessTreeSampler
{
    private const uint Th32csSnapprocess = 0x00000002;
    private static readonly IntPtr InvalidHandle = new(-1);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PROCESSENTRY32
    {
        public uint dwSize; public uint cntUsage; public uint th32ProcessID; public IntPtr th32DefaultHeapID;
        public uint th32ModuleID; public uint cntThreads; public uint th32ParentProcessID;
        public int pcPriClassBase; public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szExeFile;
    }
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint pid);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool Process32FirstW(IntPtr snap, ref PROCESSENTRY32 entry);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool Process32NextW(IntPtr snap, ref PROCESSENTRY32 entry);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
    public static IReadOnlyDictionary<int, int> Parents()
    {
        var result = new Dictionary<int, int>();
        var snap = CreateToolhelp32Snapshot(Th32csSnapprocess, 0);
        if (snap == InvalidHandle) return result;
        try
        {
            var row = new PROCESSENTRY32 { dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32>(), szExeFile = "" };
            if (!Process32FirstW(snap, ref row)) return result;
            do { result[(int)row.th32ProcessID] = (int)row.th32ParentProcessID; }
            while (Process32NextW(snap, ref row));
        }
        finally { CloseHandle(snap); }
        return result;
    }
}
