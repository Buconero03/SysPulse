using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ProcessLens.Windows;

/// <summary>Physical RAM from GlobalMemoryStatusEx and full process/category RAM census via PSAPI EX2.</summary>
public static class WindowsPhysicalMemoryReader
{
    private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong ExtendedVirtual;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessMemoryCountersEx2
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

    [DllImport("kernel32.dll", EntryPoint = "GlobalMemoryStatusEx", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx status);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("psapi.dll", EntryPoint = "GetProcessMemoryInfo", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessMemoryInfo(
        IntPtr process, ref ProcessMemoryCountersEx2 counters, uint size);

    public sealed record PhysicalMemory(
        ulong TotalBytes, ulong AvailableBytes,
        ulong TotalPageFileBytes = 0, ulong AvailablePageFileBytes = 0)
    {
        public ulong UsedBytes => TotalBytes >= AvailableBytes ? TotalBytes - AvailableBytes : 0;
        public double UsedPercent => TotalBytes == 0 ? 0 : Math.Clamp(
            UsedBytes * 100.0 / TotalBytes, 0, 100);
        public ulong UsedPageFileBytes => TotalPageFileBytes >= AvailablePageFileBytes
            ? TotalPageFileBytes - AvailablePageFileBytes : 0;
    }

    public sealed record ProcessMemory(
        int Pid, string Name, long WorkingSetBytes, long PrivateCommitBytes,
        long PrivateWorkingSetBytes = 0, bool PrivateWorkingSetAvailable = false,
        string Category = "Applicazioni", string FriendlyName = "", long PeakWorkingSetBytes = 0)
    {
        public long SharedWorkingSetBytes => PrivateWorkingSetAvailable && WorkingSetBytes >= PrivateWorkingSetBytes
            ? WorkingSetBytes - PrivateWorkingSetBytes : 0;
        public long EffectiveResidentBytes => PrivateWorkingSetAvailable ? PrivateWorkingSetBytes : WorkingSetBytes;
    }

    public sealed record AppMemoryGroup(
        string GroupName, string ExecutableName, string Category, int InstanceCount, int LargestPid,
        long TotalPrivateWorkingSetBytes, long TotalWorkingSetBytes, long TotalPrivateCommitBytes,
        long MaxPeakWorkingSetBytes, bool PrivateAvailable)
    {
        public long EffectiveResidentBytes => PrivateAvailable ? TotalPrivateWorkingSetBytes : TotalWorkingSetBytes;
        public long SharedWorkingSetBytes => PrivateAvailable && TotalWorkingSetBytes >= TotalPrivateWorkingSetBytes
            ? TotalWorkingSetBytes - TotalPrivateWorkingSetBytes : 0;
    }

    public sealed record CategoryMemorySummary(
        string Category, int ProcessCount, int GroupCount,
        long PrivateWorkingSetBytes, long WorkingSetBytes, long PrivateCommitBytes);

    public sealed record FullMemoryAnalysisSnapshot(
        PhysicalMemory Memory,
        IReadOnlyList<ProcessMemory> Processes,
        IReadOnlyList<AppMemoryGroup> Groups,
        IReadOnlyList<CategoryMemorySummary> Categories,
        long TotalProcessPrivateWorkingSetBytes,
        long TotalProcessPrivateCommitBytes,
        long KernelAndSharedPhysicalBytes,
        int TotalAccessibleProcesses,
        int UnreadableProcessesCount);

    private static readonly HashSet<string> DevNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "dotnet", "vbcscompiler", "msbuild", "csc", "vbc", "devenv", "rider64", "code", "cursor",
        "windsurf", "python", "pythonw", "python3", "conda", "pip", "uv", "node", "deno", "bun",
        "java", "javaw", "gradle", "mvn", "rustc", "cargo", "go", "gcc", "clang", "cmake", "ninja",
        "git", "bash", "sh", "wsl", "wslservice", "wslhost", "vmmem", "vmmemwsl", "docker",
        "com.docker.backend", "powershell", "pwsh", "processlensprofiler", "adb", "emulator"
    };

    private static readonly HashSet<string> WindowsCoreNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "system", "registry", "smss", "csrss", "wininit", "winlogon", "services", "lsass", "lsaiso",
        "fontdrvhost", "dwm", "explorer", "sihost", "taskhostw", "runtimebroker", "applicationframehost",
        "startmenuexperiencehost", "shellexperiencehost", "searchhost", "textinputhost", "ctfmon",
        "conhost", "openconsole", "lockapp", "logonui", "smartscreen", "securityhealthservice",
        "securityhealthsystray", "msmpeng", "mpdefendercoreservice", "nissrv", "audiodg",
        "systemsettings", "taskmgr", "mmc", "dllhost", "rundll32", "werfault", "wermgr",
        "tiworker", "trustedinstaller", "gamebar", "gamebarftserver"
    };

    private static readonly HashSet<string> ServiceBgNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "svchost", "spoolsv", "wmiprvse", "unsecapp", "dashost", "wlanext", "vds", "vssvc",
        "msiexec", "wuauclt", "usoclient", "mousocoreworker", "compattelrunner", "backgroundtaskhost",
        "settingsynchost", "sgrmbroker", "searchindexer", "searchprotocolhost", "searchfilterhost",
        "searchapp", "widgets", "widgetservice", "phoneexperiencehost", "crossdeviceservice",
        "filecoauth", "processlens.memoryagent", "processlens.guardian.service"
    };

    private static readonly Dictionary<string, string> KnownFriendlyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["chrome"] = "Google Chrome",
        ["msedge"] = "Microsoft Edge",
        ["msedgewebview2"] = "Microsoft Edge WebView2",
        ["firefox"] = "Mozilla Firefox",
        ["brave"] = "Brave Browser",
        ["opera"] = "Opera Browser",
        ["code"] = "Visual Studio Code",
        ["devenv"] = "Visual Studio",
        ["dotnet"] = "Runtime / Compilatore .NET",
        ["powershell"] = "Windows PowerShell",
        ["pwsh"] = "PowerShell 7",
        ["python"] = "Interprete Python",
        ["node"] = "Runtime Node.js",
        ["java"] = "Java Virtual Machine",
        ["javaw"] = "Java Virtual Machine",
        ["wsl"] = "Windows Subsystem for Linux",
        ["vmmem"] = "Macchina virtuale WSL / Hyper-V",
        ["vmmemwsl"] = "Macchina virtuale WSL 2",
        ["dwm"] = "Gestione finestre desktop (DWM)",
        ["explorer"] = "Esplora risorse Windows",
        ["svchost"] = "Host servizi di Windows (svchost)",
        ["msmpeng"] = "Antivirus Microsoft Defender",
        ["searchhost"] = "Ricerca di Windows (SearchHost)",
        ["searchindexer"] = "Indicizzatore Windows Search",
        ["runtimebroker"] = "Runtime Broker di Windows",
        ["sihost"] = "Host infrastruttura shell",
        ["taskhostw"] = "Host attività di Windows",
        ["conhost"] = "Host finestra console",
        ["dllhost"] = "Host componenti COM (dllhost)",
        ["wmiprvse"] = "Provider WMI di Windows",
        ["audiodg"] = "Motore audio di Windows",
        ["spoolsv"] = "Spooler di stampa Windows",
        ["ctfmon"] = "Servizio input e tastiera (CTF)",
        ["textinputhost"] = "Servizio input di testo Windows",
        ["windowsterminal"] = "Terminale Windows",
        ["processlens"] = "ProcessLens (Interfaccia)",
        ["processlens.memoryagent"] = "ProcessLens — Monitor Avvisi RAM",
        ["processlens.guardian.service"] = "ProcessLens — Servizio Guardian",
        ["processlensprofiler"] = "ProcessLens — Profiler Heap .NET"
    };

    public static PhysicalMemory Read()
    {
        var state = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (!GlobalMemoryStatusEx(ref state) || state.TotalPhysical == 0)
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),
                "GlobalMemoryStatusEx non disponibile");
        return new PhysicalMemory(state.TotalPhysical, state.AvailablePhysical,
            state.TotalPageFile, state.AvailablePageFile);
    }

    /// <summary>Bounded top process inventory for backwards compatibility.</summary>
    public static IReadOnlyList<ProcessMemory> ReadTopProcesses(int count = 20)
    {
        count = Math.Clamp(count, 1, 50);
        var full = ReadFullAnalysis(count);
        return full.Processes.Take(count).ToArray();
    }

    /// <summary>Full system RAM census: classifies all readable processes, groups multi-process apps, and computes category totals.</summary>
    public static FullMemoryAnalysisSnapshot ReadFullAnalysis(int maxRows = 60)
    {
        maxRows = Math.Clamp(maxRows, 10, 150);
        var memory = Read();
        var rawList = new List<(int Pid, string Name, long Ws, long Commit, int SessionId, bool HasWindow)>(256);
        var namesWithWindows = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int unreadable = 0;

        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    int pid = process.Id;
                    if (pid <= 0) continue;
                    long ws = process.WorkingSet64;
                    if (ws <= 0) continue;
                    string name = process.ProcessName;
                    long commit = process.PrivateMemorySize64;
                    int sessionId = 1;
                    try { sessionId = process.SessionId; } catch { }
                    bool hasWin = false;
                    try { hasWin = process.MainWindowHandle != IntPtr.Zero; } catch { }
                    if (hasWin) namesWithWindows.Add(name);
                    rawList.Add((pid, name, ws, commit, sessionId, hasWin));
                }
                catch (Exception ex) when (ex is InvalidOperationException or
                    System.ComponentModel.Win32Exception or System.UnauthorizedAccessException)
                {
                    unreadable++;
                }
            }
        }

        var allProcesses = new List<ProcessMemory>(rawList.Count);
        long totalPrivWs = 0;
        long totalPrivCommit = 0;

        foreach (var item in rawList)
        {
            string category = ClassifyCategory(item.Name, item.SessionId, item.HasWindow || namesWithWindows.Contains(item.Name));
            string friendly = ResolveFriendlyGroupName(item.Name);
            if (TryReadEx2(item.Pid, out long ws, out long privWs, out long commit, out long peakWs))
            {
                long finalWs = ws > 0 ? ws : item.Ws;
                long finalCommit = commit > 0 ? commit : item.Commit;
                totalPrivWs = checked(totalPrivWs + privWs);
                totalPrivCommit = checked(totalPrivCommit + finalCommit);
                allProcesses.Add(new ProcessMemory(
                    item.Pid, item.Name, finalWs, finalCommit,
                    privWs, true, category, friendly, peakWs));
            }
            else
            {
                totalPrivCommit = checked(totalPrivCommit + item.Commit);
                allProcesses.Add(new ProcessMemory(
                    item.Pid, item.Name, item.Ws, item.Commit,
                    0, false, category, friendly, 0));
            }
        }

        var groups = allProcesses
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var items = g.ToArray();
                var largest = items.OrderByDescending(x => x.EffectiveResidentBytes).First();
                bool anyPriv = items.Any(x => x.PrivateWorkingSetAvailable);
                long sumPrivWs = items.Where(x => x.PrivateWorkingSetAvailable).Sum(x => x.PrivateWorkingSetBytes);
                long sumWs = items.Sum(x => x.WorkingSetBytes);
                long sumCommit = items.Sum(x => x.PrivateCommitBytes);
                long maxPeak = items.Max(x => x.PeakWorkingSetBytes);
                return new AppMemoryGroup(
                    largest.FriendlyName,
                    largest.Name,
                    largest.Category,
                    items.Length,
                    largest.Pid,
                    sumPrivWs,
                    sumWs,
                    sumCommit,
                    maxPeak,
                    anyPriv);
            })
            .OrderByDescending(g => g.EffectiveResidentBytes)
            .ToArray();

        string[] categoryOrder = new[] { "Applicazioni", "Sviluppo", "Windows", "Secondario" };
        var categories = categoryOrder
            .Select(cat =>
            {
                var catProcs = allProcesses.Where(p => p.Category == cat).ToArray();
                int grpCount = groups.Count(g => g.Category == cat);
                return new CategoryMemorySummary(
                    cat,
                    catProcs.Length,
                    grpCount,
                    catProcs.Where(p => p.PrivateWorkingSetAvailable).Sum(p => p.PrivateWorkingSetBytes),
                    catProcs.Sum(p => p.WorkingSetBytes),
                    catProcs.Sum(p => p.PrivateCommitBytes));
            })
            .ToArray();

        long usedPhysical = (long)Math.Min(memory.UsedBytes, (ulong)long.MaxValue);
        long kernelAndShared = Math.Max(0L, usedPhysical - totalPrivWs);

        var sortedProcesses = allProcesses
            .OrderByDescending(p => p.EffectiveResidentBytes)
            .Take(maxRows)
            .ToArray();

        return new FullMemoryAnalysisSnapshot(
            memory,
            sortedProcesses,
            groups.Take(maxRows).ToArray(),
            categories,
            totalPrivWs,
            totalPrivCommit,
            kernelAndShared,
            allProcesses.Count,
            unreadable);
    }

    private static string ClassifyCategory(string processName, int sessionId, bool appHasVisibleWindow)
    {
        string clean = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? processName[..^4] : processName;

        if (DevNames.Contains(clean)) return "Sviluppo";
        if (WindowsCoreNames.Contains(clean)) return "Windows";
        if (ServiceBgNames.Contains(clean)) return "Secondario";

        if (clean.Contains("updater", StringComparison.OrdinalIgnoreCase) ||
            clean.Contains("crashpad", StringComparison.OrdinalIgnoreCase) ||
            clean.EndsWith("service", StringComparison.OrdinalIgnoreCase) ||
            clean.EndsWith("svc", StringComparison.OrdinalIgnoreCase))
            return "Secondario";

        if (appHasVisibleWindow) return "Applicazioni";
        if (sessionId == 0) return "Secondario";
        return "Applicazioni";
    }

    private static string ResolveFriendlyGroupName(string processName)
    {
        string clean = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? processName[..^4] : processName;
        if (KnownFriendlyNames.TryGetValue(clean, out string? friendly))
            return $"{friendly} ({clean})";
        return clean;
    }

    private static bool TryReadEx2(int pid, out long ws, out long privateWs, out long commit, out long peakWs)
    {
        ws = 0;
        privateWs = 0;
        commit = 0;
        peakWs = 0;
        if (pid <= 0) return false;
        IntPtr handle = IntPtr.Zero;
        try
        {
            handle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)pid);
            if (handle == IntPtr.Zero) return false;
            var counters = new ProcessMemoryCountersEx2
            {
                cb = (uint)Marshal.SizeOf<ProcessMemoryCountersEx2>()
            };
            if (!GetProcessMemoryInfo(handle, ref counters, counters.cb)) return false;
            ws = checked((long)counters.WorkingSetSize);
            privateWs = checked((long)counters.PrivateWorkingSetSize);
            commit = checked((long)counters.PrivateUsage);
            peakWs = checked((long)counters.PeakWorkingSetSize);
            return privateWs >= 0 && privateWs <= ws;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (handle != IntPtr.Zero) CloseHandle(handle);
        }
    }
}
