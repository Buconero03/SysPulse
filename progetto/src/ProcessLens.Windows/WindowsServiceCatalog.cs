using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using ProcessLens.Core;

namespace ProcessLens.Windows;

/// <summary>Read-only service catalog from Windows SCM. Runs off the UI thread and only when requested.</summary>
public sealed class WindowsServiceCatalog
{
    private const uint SERVICE_QUERY_CONFIG = 0x0001;
    private const uint SC_MANAGER_CONNECT = 0x0001;
    private const int BufferSize = 256 * 1024;

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "OpenServiceW", SetLastError = true)]
    private static extern IntPtr OpenService(IntPtr scm, string name, uint access);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "QueryServiceConfig2W", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryServiceConfig2(IntPtr service, uint level, IntPtr buffer,
        uint bufferSize, out uint needed);

    [DllImport("shlwapi.dll", EntryPoint = "SHLoadIndirectString", CharSet = CharSet.Unicode)]
    private static extern int SHLoadIndirectString(string source, StringBuilder output, uint size, IntPtr reserved);

    [StructLayout(LayoutKind.Sequential)]
    private struct ServiceDescription { public IntPtr Text; }

    public IReadOnlyList<ServiceSnapshot> Capture()
    {
        var result = new List<ServiceSnapshot>();
        var scm = NativeMethods.OpenSCManagerW(null, null,
            NativeMethods.SC_MANAGER_ENUMERATE_SERVICE | SC_MANAGER_CONNECT);
        if (scm == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Accesso all'elenco servizi Windows non disponibile.");
        try
        {
            uint resume = 0;
            var buffer = Marshal.AllocHGlobal(BufferSize);
            try
            {
                for (int page = 0; page < 32; page++)
                {
                    bool ok = NativeMethods.EnumServicesStatusExW(scm,
                        NativeMethods.SC_ENUM_PROCESS_INFO,
                        NativeMethods.SERVICE_WIN32,
                        NativeMethods.SERVICE_STATE_ALL,
                        buffer, BufferSize, out _, out uint count, ref resume, null);
                    int lastError = ok ? 0 : Marshal.GetLastWin32Error();
                    int itemSize = Marshal.SizeOf<NativeMethods.ENUM_SERVICE_STATUS_PROCESS>();
                    for (int i = 0; i < count; i++)
                    {
                        var ptr = IntPtr.Add(buffer, checked(i * itemSize));
                        var item = Marshal.PtrToStructure<NativeMethods.ENUM_SERVICE_STATUS_PROCESS>(ptr);
                        var name = Marshal.PtrToStringUni(item.lpServiceName);
                        if (string.IsNullOrWhiteSpace(name)) continue;
                        var display = Marshal.PtrToStringUni(item.lpDisplayName);
                        var config = ReadRegistryConfig(name);
                        var description = ReadDescription(scm, name, config.Description);
                        result.Add(new ServiceSnapshot
                        {
                            Name = name,
                            DisplayName = string.IsNullOrWhiteSpace(display) ? name : display,
                            StateCode = item.ServiceStatusProcess.dwCurrentState,
                            ServiceType = item.ServiceStatusProcess.dwServiceType,
                            ProcessId = checked((int)item.ServiceStatusProcess.dwProcessId),
                            StartupCode = config.StartType,
                            DelayedAutoStart = config.Delayed,
                            BinaryPath = config.Path,
                            AccountName = config.Account,
                            Dependencies = config.Dependencies,
                            GroupDependencies = config.GroupDependencies,
                            Description = description
                        });
                    }
                    if (ok) break;
                    if (lastError != NativeMethods.ERROR_MORE_DATA)
                        throw new Win32Exception(lastError, "Enumerazione servizi incompleta.");
                }
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }
        finally { NativeMethods.CloseServiceHandle(scm); }
        return result.GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderByDescending(s => s.IsRunning)
            .ThenBy(s => s.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private sealed record RegistryConfig(int? StartType, bool Delayed, string? Path,
        string? Account, string? Description, string[] Dependencies, string[] GroupDependencies);

    private static RegistryConfig ReadRegistryConfig(string name)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + name);
            if (key is null) return new(null, false, null, null, null, [], []);
            int? start = key.GetValue("Start") is int n ? n : null;
            bool delayed = key.GetValue("DelayedAutoStart") is int d && d == 1;
            string? path = key.GetValue("ImagePath") as string;
            string? account = key.GetValue("ObjectName") as string;
            string? description = key.GetValue("Description", null,
                RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
            var dependencies = GetStringList(key.GetValue("DependOnService"));
            var groups = GetStringList(key.GetValue("DependOnGroup"));
            return new(start, delayed, path, account, description, dependencies, groups);
        }
        catch (Exception) { return new(null, false, null, null, null, [], []); }
    }

    private static string[] GetStringList(object? value) => value switch
    {
        string[] strings => strings.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray(),
        string single when !string.IsNullOrWhiteSpace(single) => single.Split('\0', StringSplitOptions.RemoveEmptyEntries),
        _ => []
    };

    private static string? ResolveIndirect(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        if (!input.StartsWith('@')) return input.Trim();
        // Do not load arbitrary DLL paths supplied by third-party service descriptions.
        // Resolve only well-known Windows System32 resources; otherwise report unavailable.
        string resource = input[1..].TrimStart('\"');
        if (!resource.StartsWith(@"%SystemRoot%\system32\", StringComparison.OrdinalIgnoreCase) &&
            !resource.StartsWith(@"%WinDir%\system32\", StringComparison.OrdinalIgnoreCase))
            return null;
        try
        {
            var buffer = new StringBuilder(4096);
            return SHLoadIndirectString(input, buffer, (uint)buffer.Capacity, IntPtr.Zero) == 0
                && buffer.Length > 0 ? buffer.ToString() : null;
        }
        catch { return null; }
    }

    private static string? ReadDescription(IntPtr scm, string name, string? registryDescription)
    {
        var fromRegistry = ResolveIndirect(registryDescription);
        if (fromRegistry is not null) return fromRegistry;
        // SERVICE_CONFIG_DESCRIPTION = 1. Provider-authored description, not inferred behaviour.
        IntPtr service = OpenService(scm, name, SERVICE_QUERY_CONFIG);
        if (service == IntPtr.Zero) return null;
        try
        {
            QueryServiceConfig2(service, 1, IntPtr.Zero, 0, out uint needed);
            if (needed == 0 || needed > 64 * 1024) return null;
            IntPtr buffer = Marshal.AllocHGlobal(checked((int)needed));
            try
            {
                if (!QueryServiceConfig2(service, 1, buffer, needed, out _)) return null;
                var description = Marshal.PtrToStructure<ServiceDescription>(buffer);
                var raw = description.Text != IntPtr.Zero ? Marshal.PtrToStringUni(description.Text) : null;
                return ResolveIndirect(raw);
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }
        finally { NativeMethods.CloseServiceHandle(service); }
    }
}
