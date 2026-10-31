using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using ProcessLens.Core;

namespace ProcessLens.Windows;

/// <summary>
/// Expensive, evidence-oriented inspection for one selected process only.
/// It deliberately does not run across the whole process list.
///
/// Network evidence is ownership-oriented: Windows socket tables identify the
/// concrete PID that owns each endpoint. For multi-process applications we keep
/// direct ownership separate from the related process family and expose which
/// sibling/helper actually owns the sockets.
/// </summary>
public sealed class WindowsDeepProcessInspector : IDeepProcessInspector
{
    public ProcessSnapshot Inspect(ProcessSnapshot process)
    {
        var identity = ReadIdentity(process.Pid, process.ParentPid, process.Name);
        if (identity is null || !ProcessGrouping.IdentityMatches(process, identity))
            return ClearUnverifiedDetails(ProcessNetworkEvidence.Unavailable(process, "PID selezionato non verificabile prima dell'analisi; dati N/D."));
        var modules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var windowTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var windowClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var notes = new List<string>();

        string? aumid = null;
        bool hasVisibleWindow = false;

        try
        {
            using var p = Process.GetProcessById(process.Pid);
            try
            {
                foreach (ProcessModule module in p.Modules)
                {
                    if (!string.IsNullOrWhiteSpace(module.ModuleName))
                        modules.Add(module.ModuleName);
                    if (modules.Count >= 160)
                        break;
                }
            }
            catch (Exception ex)
            {
                notes.Add($"moduli non accessibili: {ex.GetType().Name}");
            }
        }
        catch (Exception ex)
        {
            notes.Add($"processo non riapribile: {ex.GetType().Name}");
        }

        IntPtr handle = IntPtr.Zero;
        try
        {
            handle = NativeMethods.OpenProcess(
                NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION,
                false,
                (uint)process.Pid);

            if (handle != IntPtr.Zero)
                aumid = TryGetApplicationUserModelId(handle);
        }
        catch (Exception ex)
        {
            notes.Add($"AUMID non accessibile: {ex.GetType().Name}");
        }
        finally
        {
            if (handle != IntPtr.Zero)
                NativeMethods.CloseHandle(handle);
        }

        try
        {
            NativeMethods.EnumWindows((hWnd, _) =>
            {
                NativeMethods.GetWindowThreadProcessId(hWnd, out uint pid);
                if (pid != (uint)process.Pid)
                    return true;

                bool visible = NativeMethods.IsWindowVisible(hWnd);
                hasVisibleWindow |= visible;

                int length = NativeMethods.GetWindowTextLengthW(hWnd);
                if (length > 0 && length < 8192)
                {
                    var title = new StringBuilder(length + 1);
                    if (NativeMethods.GetWindowTextW(hWnd, title, title.Capacity) > 0 && !string.IsNullOrWhiteSpace(title.ToString()))
                        windowTitles.Add(title.ToString().Trim());
                }

                var cls = new StringBuilder(512);
                if (NativeMethods.GetClassNameW(hWnd, cls, cls.Capacity) > 0 && !string.IsNullOrWhiteSpace(cls.ToString()))
                    windowClasses.Add(cls.ToString().Trim());

                return true;
            }, IntPtr.Zero);
        }
        catch (Exception ex)
        {
            notes.Add($"finestre non enumerabili: {ex.GetType().Name}");
        }

        var baseSnapshot = process with
        {
            IsDeepInspected = true, ApplicationUserModelId = aumid, HasVisibleWindow = hasVisibleWindow,
            WindowTitles = windowTitles.Take(12).ToArray(), WindowClasses = windowClasses.Take(20).ToArray(),
            ModuleNames = modules.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(),
            DeepInspectionNote = notes.Count == 0 ? "Analisi Windows completata" : string.Join("; ", notes)
        };
        var result = RefreshNetwork(baseSnapshot);
        return result.DirectNetworkIdentityVerified == false ? ClearUnverifiedDetails(result) : result;
    }

    public ProcessSnapshot RefreshNetwork(ProcessSnapshot process)
    {
        var anchor = ReadIdentity(process.Pid, process.ParentPid, process.Name);
        if (anchor is null || !ProcessGrouping.IdentityMatches(process, anchor))
            return ProcessNetworkEvidence.Unavailable(process, "Identità del PID selezionato cambiata o non leggibile: nessun socket attribuito al vecchio gruppo.");
        var notes = new List<string>();
        var tree = new Dictionary<int, TreeEntry>();
        var candidates = new List<ProcessSnapshot>();
        try
        {
            tree = ReadProcessTreeSnapshot();
            var matching = tree.Values.Where(x => string.Equals(x.Name, process.Name, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matching.Length > 512) notes.Add("Gruppo limitato ai primi 512 candidati omonimi; copertura parziale.");
            foreach (var entry in matching.Take(512))
            {
                var identity = ReadIdentity(entry.Pid, entry.ParentPid, entry.Name);
                if (identity is not null) candidates.Add(identity);
                else notes.Add($"PID {entry.Pid}: identità del candidato non disponibile; escluso dal gruppo.");
            }
        }
        catch (Exception ex) { notes.Add($"Gruppo: {DescribeError(ex)}"); }
        // The selected row's identity is authoritative; a newly reused PID cannot replace it.
        candidates.RemoveAll(x => x.Pid == process.Pid); candidates.Add(process);
        var family = ProcessGrouping.Build(candidates).FirstOrDefault(x => x.Members.Any(p => p.Pid == process.Pid));
        int familyRootPid = family?.Root.Pid ?? process.Pid;
        var expected = (family?.Members ?? new[] { process }).Take(512).ToDictionary(x => x.Pid);
        if (tree.Count > 0 && expected.Count < 512)
        {
            var childrenByParent = tree.Values
                .Where(x => x.Pid > 0 && x.ParentPid > 0 && x.Pid != x.ParentPid)
                .GroupBy(x => x.ParentPid)
                .ToDictionary(g => g.Key, g => g.ToArray());
            var frontier = new Queue<(int Pid, DateTime? StartUtc)>(
                expected.Values.Select(m => (m.Pid, m.StartTime?.ToUniversalTime())));
            int addedDescendants = 0;
            while (frontier.Count > 0 && addedDescendants < 48 && expected.Count < 512)
            {
                var (parentPid, parentStartUtc) = frontier.Dequeue();
                if (!childrenByParent.TryGetValue(parentPid, out var children))
                    continue;
                foreach (var childEntry in children)
                {
                    if (expected.ContainsKey(childEntry.Pid))
                        continue;
                    var childIdentity = ReadIdentity(childEntry.Pid, childEntry.ParentPid, childEntry.Name);
                    if (childIdentity?.StartTime is { } childStart &&
                        parentStartUtc.HasValue &&
                        childStart.ToUniversalTime() >= parentStartUtc.Value)
                    {
                        expected[childEntry.Pid] = childIdentity;
                        frontier.Enqueue((childEntry.Pid, childStart.ToUniversalTime()));
                        addedDescendants++;
                        if (addedDescendants >= 48 || expected.Count >= 512)
                            break;
                    }
                }
            }
        }
        var familyPids = new HashSet<int>();
        foreach (var member in expected.Values)
        {
            var current = ReadIdentity(member.Pid, member.ParentPid, member.Name);
            if (current is not null && ProcessGrouping.IdentityMatches(member, current)) familyPids.Add(member.Pid);
            else notes.Add($"PID {member.Pid}: identità non verificabile prima della lettura; escluso.");
        }
        var familyCommandLines = familyPids.ToDictionary(pid => pid,
            pid => pid == process.Pid ? process.CommandLine : TryGetCommandLineByPid(pid));
        var network = familyPids.ToDictionary(pid => pid, _ => new NetworkAccumulator());
        foreach (var (label, addressFamily, tcp) in new[]
        {
            ("TCP IPv4", NativeMethods.AF_INET, true), ("TCP IPv6", NativeMethods.AF_INET6, true),
            ("UDP IPv4", NativeMethods.AF_INET, false), ("UDP IPv6", NativeMethods.AF_INET6, false)
        })
        {
            try
            {
                if (tcp) ReadTcpForPids(familyPids, network, addressFamily);
                else ReadUdpForPids(familyPids, network, addressFamily);
            }
            catch (Exception ex) { notes.Add($"{label}: {DescribeError(ex)}"); }
        }
        foreach (int pid in familyPids.ToArray())
        {
            var current = ReadIdentity(pid, expected[pid].ParentPid, expected[pid].Name);
            if (current is null || !ProcessGrouping.IdentityMatches(expected[pid], current))
            {
                network.Remove(pid); familyPids.Remove(pid);
                notes.Add($"PID {pid}: terminato o identità cambiata durante la lettura; socket esclusi.");
            }
        }
        if (!familyPids.Contains(process.Pid))
            return ProcessNetworkEvidence.Unavailable(process, string.Join("; ", notes));
        network.TryGetValue(process.Pid, out var direct);
        direct ??= new NetworkAccumulator();

        int familyListening = network.Values.Sum(x => x.TcpListening);
        int familyEstablished = network.Values.Sum(x => x.TcpEstablished);
        int familyUdp = network.Values.Sum(x => x.UdpEndpoints);
        int familyPublic = network.Values.Sum(x => x.PublicRemoteConnections);
        int familyPrivate = network.Values.Sum(x => x.PrivateRemoteConnections);
        int familyLoopback = network.Values.Sum(x => x.LoopbackRemoteConnections);
        int familyWithSockets = network.Values.Count(x => x.HasAnySocket);
        var familyListenPorts = network.Values
            .SelectMany(x => x.ListeningTcpPorts)
            .Distinct()
            .OrderBy(x => x)
            .Take(80)
            .ToArray();

        var relatedNetworkProcesses = network
            .Where(kvp => kvp.Value.HasAnySocket)
            .Select(kvp =>
            {
                int pid = kvp.Key;
                var n = kvp.Value;
                string name = tree.TryGetValue(pid, out var entry) && !string.IsNullOrWhiteSpace(entry.Name)
                    ? entry.Name
                    : process.Name;
                string role = InferFamilyRole(pid, familyRootPid, familyCommandLines.GetValueOrDefault(pid));
                return new RelatedNetworkProcessSnapshot(
                    pid,
                    name,
                    role,
                    n.TcpListening,
                    n.TcpEstablished,
                    n.UdpEndpoints,
                    n.PublicRemoteConnections,
                    n.Endpoints.Take(12).ToArray());
            })
            .OrderByDescending(x => IsNetworkServiceRole(x.RoleHint))
            .ThenByDescending(x => x.PublicRemoteConnectionCount)
            .ThenByDescending(x => x.TcpEstablishedCount)
            .ThenBy(x => x.Pid)
            .Take(32)
            .ToArray();

        return process with
        {
            DeepInspectionNote = (process.DeepInspectionNote?.Split(" | Rete: ")[0] ?? "") +
                (notes.Count == 0 ? "" : " | Rete: " + string.Join("; ", notes)),
            DirectNetworkIdentityVerified = familyPids.Contains(process.Pid),
            NetworkVerifiedProcessCount = familyPids.Count,
            NetworkCapturedUtc = DateTime.UtcNow,
            NetworkAcquisitionComplete = notes.Count == 0,
            NetworkAcquisitionNote = notes.Count == 0 ? "Quattro tabelle Windows lette; identità dei PID confrontate prima e dopo." : string.Join("; ", notes),
            TcpListeningCount = direct.TcpListening,
            TcpEstablishedCount = direct.TcpEstablished,
            UdpEndpointCount = direct.UdpEndpoints,
            ListeningTcpPorts = direct.ListeningTcpPorts.OrderBy(x => x).Take(40).ToArray(),
            UdpPorts = direct.UdpPorts.OrderBy(x => x).Take(40).ToArray(),
            PublicRemoteConnectionCount = direct.PublicRemoteConnections,
            PrivateRemoteConnectionCount = direct.PrivateRemoteConnections,
            LoopbackRemoteConnectionCount = direct.LoopbackRemoteConnections,
            NetworkEndpoints = direct.Endpoints.Take(40).ToArray(),

            NetworkFamilyRootPid = familyRootPid,
            NetworkFamilyProcessCount = expected.Count,
            NetworkFamilyProcessesWithSockets = familyWithSockets,
            NetworkFamilyTcpListeningCount = familyListening,
            NetworkFamilyTcpEstablishedCount = familyEstablished,
            NetworkFamilyUdpEndpointCount = familyUdp,
            NetworkFamilyPublicRemoteConnectionCount = familyPublic,
            NetworkFamilyPrivateRemoteConnectionCount = familyPrivate,
            NetworkFamilyLoopbackRemoteConnectionCount = familyLoopback,
            NetworkFamilyListeningTcpPorts = familyListenPorts,
            RelatedNetworkProcesses = relatedNetworkProcesses,

        };
    }

    private static ProcessSnapshot ClearUnverifiedDetails(ProcessSnapshot p) => p with
    {
        IsDeepInspected = true, ModuleNames = Array.Empty<string>(), WindowTitles = Array.Empty<string>(),
        WindowClasses = Array.Empty<string>(), HasVisibleWindow = false, ApplicationUserModelId = null
    };

    private static string DescribeError(Exception ex) => ex is System.ComponentModel.Win32Exception w
        ? $"Win32 {w.NativeErrorCode}: {w.Message}" : $"{ex.GetType().Name}: {ex.Message}";

    private static ProcessSnapshot? ReadIdentity(int pid, int parent, string name)
    {
        IntPtr handle = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)pid);
        if (handle == IntPtr.Zero) return null;
        try
        {
            if (!GetProcessTimes(handle, out long creation, out long exit, out _, out _) || exit != 0) return null;
            var buffer = new StringBuilder(32768); uint length = (uint)buffer.Capacity;
            if (!NativeMethods.QueryFullProcessImageNameW(handle, 0, buffer, ref length)) return null;
            return new ProcessSnapshot { Pid = pid, ParentPid = parent, Name = name,
                StartTime = DateTime.FromFileTimeUtc(creation), ExecutablePath = buffer.ToString() };
        }
        catch { return null; }
        finally { NativeMethods.CloseHandle(handle); }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessTimes(IntPtr process, out long creation, out long exit, out long kernel, out long user);

    private static string? TryGetApplicationUserModelId(IntPtr processHandle)
    {
        uint length = 0;
        int rc = NativeMethods.GetApplicationUserModelId(processHandle, ref length, null);
        if (length == 0)
            return null;

        if (rc != NativeMethods.ERROR_INSUFFICIENT_BUFFER && rc != 0)
            return null;

        var buffer = new StringBuilder((int)length);
        rc = NativeMethods.GetApplicationUserModelId(processHandle, ref length, buffer);
        return rc == 0 && buffer.Length > 0 ? buffer.ToString() : null;
    }

    private static Dictionary<int, TreeEntry> ReadProcessTreeSnapshot()
    {
        var result = new Dictionary<int, TreeEntry>();
        IntPtr snapshot = NativeMethods.CreateToolhelp32Snapshot(NativeMethods.TH32CS_SNAPPROCESS, 0);
        if (snapshot == NativeMethods.INVALID_HANDLE_VALUE)
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());

        try
        {
            var pe = new NativeMethods.PROCESSENTRY32
            {
                dwSize = (uint)Marshal.SizeOf<NativeMethods.PROCESSENTRY32>()
            };

            if (!NativeMethods.Process32FirstW(snapshot, ref pe))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());

            do
            {
                int pid = unchecked((int)pe.th32ProcessID);
                result[pid] = new TreeEntry(
                    pid,
                    unchecked((int)pe.th32ParentProcessID),
                    pe.szExeFile ?? string.Empty);
                pe.dwSize = (uint)Marshal.SizeOf<NativeMethods.PROCESSENTRY32>();
            }
            while (NativeMethods.Process32NextW(snapshot, ref pe));
            int error = Marshal.GetLastWin32Error();
            if (error != 18) throw new System.ComponentModel.Win32Exception(error);
        }
        finally
        {
            NativeMethods.CloseHandle(snapshot);
        }

        return result;
    }

    private static string? TryGetCommandLineByPid(int pid)
    {
        if (pid <= 0)
            return null;

        IntPtr handle = NativeMethods.OpenProcess(
            NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION | NativeMethods.PROCESS_VM_READ,
            false,
            (uint)pid);

        if (handle == IntPtr.Zero)
            handle = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)pid);

        if (handle == IntPtr.Zero)
            return null;

        try
        {
            _ = NativeMethods.NtQueryInformationProcess(
                handle,
                NativeMethods.ProcessCommandLineInformation,
                IntPtr.Zero,
                0,
                out int requiredLength);

            if (requiredLength <= 0)
                return null;

            IntPtr buffer = Marshal.AllocHGlobal(requiredLength);
            try
            {
                int status = NativeMethods.NtQueryInformationProcess(
                    handle,
                    NativeMethods.ProcessCommandLineInformation,
                    buffer,
                    requiredLength,
                    out _);

                if (status != 0)
                    return null;

                var value = Marshal.PtrToStructure<NativeMethods.UNICODE_STRING>(buffer);
                if (value.Buffer == IntPtr.Zero || value.Length == 0)
                    return null;

                return Marshal.PtrToStringUni(value.Buffer, value.Length / 2);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch
        {
            return null;
        }
        finally
        {
            NativeMethods.CloseHandle(handle);
        }
    }

    private static string InferFamilyRole(int pid, int rootPid, string? commandLine)
    {
        string cmd = commandLine ?? string.Empty;

        if (Contains(cmd, "--type=renderer"))
            return "Renderer Chromium/Electron";
        if (Contains(cmd, "--type=gpu-process"))
            return "Processo GPU Chromium/Electron";
        if (Contains(cmd, "NetworkService") || Contains(cmd, "network.mojom.NetworkService"))
            return "Servizio di rete Chromium/Electron (dichiarato)";
        if (Contains(cmd, "AudioService") || Contains(cmd, "audio.mojom.AudioService"))
            return "Servizio audio Chromium/Electron (dichiarato)";
        if (Contains(cmd, "--type=utility"))
            return "Processo utility Chromium/Electron";
        if (Contains(cmd, "crashpad-handler") || Contains(cmd, "crashpad_handler"))
            return "Crash handler";
        if (pid == rootPid)
            return "Processo principale/coordinatore";

        return "Sottoprocesso correlato";
    }

    private static bool IsNetworkServiceRole(string role) =>
        role.Contains("Servizio di rete", StringComparison.OrdinalIgnoreCase);

    private static bool Contains(string source, string value) =>
        source.Contains(value, StringComparison.OrdinalIgnoreCase);

    private static void ReadTcpForPids(HashSet<int> targetPids, Dictionary<int, NetworkAccumulator> stats, int addressFamily)
    {
        int stride = addressFamily == NativeMethods.AF_INET ? Marshal.SizeOf<NativeMethods.MIB_TCPROW_OWNER_PID>() : Marshal.SizeOf<NativeMethods.MIB_TCP6ROW_OWNER_PID>();
        uint Query(IntPtr buffer, ref int size) => NativeMethods.GetExtendedTcpTable(buffer, ref size, false, addressFamily, NativeMethods.TCP_TABLE_OWNER_PID_ALL, 0);
        OwnedNetworkTableReader.Read(stride, Query, (rowPtr, count) =>
        {
            if (addressFamily == NativeMethods.AF_INET)
            {
                int rowSize = stride;
                for (int i = 0; i < count; i++)
                {
                    var row = Marshal.PtrToStructure<NativeMethods.MIB_TCPROW_OWNER_PID>(IntPtr.Add(rowPtr, i * rowSize));
                    AccumulateTcp(
                        unchecked((int)row.dwOwningPid),
                        row.dwState,
                        IPv4(row.dwLocalAddr),
                        PortFromNetworkOrder(row.dwLocalPort),
                        IPv4(row.dwRemoteAddr),
                        PortFromNetworkOrder(row.dwRemotePort),
                        targetPids,
                        stats);
                }
            }
            else if (addressFamily == NativeMethods.AF_INET6)
            {
                int rowSize = stride;
                for (int i = 0; i < count; i++)
                {
                    var row = Marshal.PtrToStructure<NativeMethods.MIB_TCP6ROW_OWNER_PID>(IntPtr.Add(rowPtr, i * rowSize));
                    AccumulateTcp(
                        unchecked((int)row.dwOwningPid),
                        row.dwState,
                        IPv6(row.ucLocalAddr, row.dwLocalScopeId),
                        PortFromNetworkOrder(row.dwLocalPort),
                        IPv6(row.ucRemoteAddr, row.dwRemoteScopeId),
                        PortFromNetworkOrder(row.dwRemotePort),
                        targetPids,
                        stats);
                }
            }
        });
    }

    private static void AccumulateTcp(
        int pid,
        uint state,
        IPAddress localAddress,
        int localPort,
        IPAddress remoteAddress,
        int remotePort,
        HashSet<int> targetPids,
        Dictionary<int, NetworkAccumulator> stats)
    {
        if (!targetPids.Contains(pid) || !stats.TryGetValue(pid, out var item))
            return;

        if (state == NativeMethods.MIB_TCP_STATE_LISTEN)
        {
            item.TcpListening++;
            item.ListeningTcpPorts.Add(localPort);
            if (item.Endpoints.Count < 128) item.Endpoints.Add(new NetworkEndpointSnapshot(
                "TCP",
                "LISTEN",
                localAddress.ToString(),
                localPort,
                null,
                null,
                ClassifyLocalBinding(localAddress)));
        }
        else if (state == NativeMethods.MIB_TCP_STATE_ESTAB)
        {
            item.TcpEstablished++;
            string scope = ClassifyRemote(remoteAddress);
            IncrementScope(item, scope);
            if (item.Endpoints.Count < 128) item.Endpoints.Add(new NetworkEndpointSnapshot(
                "TCP",
                "ESTABLISHED",
                localAddress.ToString(),
                localPort,
                remoteAddress.ToString(),
                remotePort,
                scope));
        }
        else if (item.Endpoints.Count < 128)
        {
            string stateName = state switch { 1 => "CLOSED", 3 => "SYN_SENT", 4 => "SYN_RECEIVED", 6 => "FIN_WAIT_1",
                7 => "FIN_WAIT_2", 8 => "CLOSE_WAIT", 9 => "CLOSING", 10 => "LAST_ACK", 11 => "TIME_WAIT", 12 => "DELETE_TCB", _ => $"STATE_{state}" };
            item.Endpoints.Add(new NetworkEndpointSnapshot("TCP", stateName, localAddress.ToString(), localPort,
                remoteAddress.ToString(), remotePort, ClassifyRemote(remoteAddress)));
        }
    }

    private static void ReadUdpForPids(HashSet<int> targetPids, Dictionary<int, NetworkAccumulator> stats, int addressFamily)
    {
        int stride = addressFamily == NativeMethods.AF_INET ? Marshal.SizeOf<NativeMethods.MIB_UDPROW_OWNER_PID>() : Marshal.SizeOf<NativeMethods.MIB_UDP6ROW_OWNER_PID>();
        uint Query(IntPtr buffer, ref int size) => NativeMethods.GetExtendedUdpTable(buffer, ref size, false, addressFamily, NativeMethods.UDP_TABLE_OWNER_PID, 0);
        OwnedNetworkTableReader.Read(stride, Query, (rowPtr, count) =>
        {
            if (addressFamily == NativeMethods.AF_INET)
            {
                int rowSize = stride;
                for (int i = 0; i < count; i++)
                {
                    var row = Marshal.PtrToStructure<NativeMethods.MIB_UDPROW_OWNER_PID>(IntPtr.Add(rowPtr, i * rowSize));
                    AccumulateUdp(
                        unchecked((int)row.dwOwningPid),
                        IPv4(row.dwLocalAddr),
                        PortFromNetworkOrder(row.dwLocalPort),
                        targetPids,
                        stats);
                }
            }
            else if (addressFamily == NativeMethods.AF_INET6)
            {
                int rowSize = stride;
                for (int i = 0; i < count; i++)
                {
                    var row = Marshal.PtrToStructure<NativeMethods.MIB_UDP6ROW_OWNER_PID>(IntPtr.Add(rowPtr, i * rowSize));
                    AccumulateUdp(
                        unchecked((int)row.dwOwningPid),
                        IPv6(row.ucLocalAddr, row.dwLocalScopeId),
                        PortFromNetworkOrder(row.dwLocalPort),
                        targetPids,
                        stats);
                }
            }
        });
    }

    private static void AccumulateUdp(
        int pid,
        IPAddress localAddress,
        int localPort,
        HashSet<int> targetPids,
        Dictionary<int, NetworkAccumulator> stats)
    {
        if (!targetPids.Contains(pid) || !stats.TryGetValue(pid, out var item))
            return;

        item.UdpEndpoints++;
        item.UdpPorts.Add(localPort);
        if (item.Endpoints.Count < 128) item.Endpoints.Add(new NetworkEndpointSnapshot(
            "UDP",
            "BOUND",
            localAddress.ToString(),
            localPort,
            null,
            null,
            ClassifyLocalBinding(localAddress)));
    }

    private static void IncrementScope(NetworkAccumulator item, string scope)
    {
        if (scope == "PUBBLICO") item.PublicRemoteConnections++;
        else if (scope is "PRIVATO/LAN" or "LINK-LOCAL") item.PrivateRemoteConnections++;
        else if (scope == "LOOPBACK") item.LoopbackRemoteConnections++;
    }

    private static IPAddress IPv4(uint value) => new(BitConverter.GetBytes(value));

    private static IPAddress IPv6(byte[]? value, uint scopeId)
    {
        if (value is not { Length: 16 })
            return IPAddress.IPv6Any;
        return new IPAddress(value, scopeId);
    }

    private static string ClassifyRemote(IPAddress address)
    {
        if (IPAddress.IsLoopback(address)) return "LOOPBACK";
        if (address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any)) return "NON SPECIFICATO";

        byte[] b = address.GetAddressBytes();
        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            if (b[0] == 10 ||
                (b[0] == 172 && b[1] >= 16 && b[1] <= 31) ||
                (b[0] == 192 && b[1] == 168) ||
                (b[0] == 100 && b[1] >= 64 && b[1] <= 127))
                return "PRIVATO/LAN";
            if (b[0] == 169 && b[1] == 254)
                return "LINK-LOCAL";
            if (b[0] >= 224)
                return "MULTICAST/RESERVED";
            return "PUBBLICO";
        }

        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            if ((b[0] & 0xFE) == 0xFC) return "PRIVATO/LAN"; // fc00::/7
            if (b[0] == 0xFE && (b[1] & 0xC0) == 0x80) return "LINK-LOCAL"; // fe80::/10
            if (b[0] == 0xFF) return "MULTICAST/RESERVED";
            return "PUBBLICO";
        }

        return "ALTRO";
    }

    private static string ClassifyLocalBinding(IPAddress address)
    {
        if (IPAddress.IsLoopback(address)) return "SOLO LOCALE";
        if (address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any)) return "TUTTE LE INTERFACCE";
        string remoteClass = ClassifyRemote(address);
        return remoteClass switch
        {
            "PRIVATO/LAN" or "LINK-LOCAL" => "INTERFACCIA LOCALE/LAN",
            "PUBBLICO" => "INTERFACCIA CON INDIRIZZO PUBBLICO",
            _ => remoteClass
        };
    }

    private static int PortFromNetworkOrder(uint value)
    {
        ushort raw = unchecked((ushort)(value & 0xFFFF));
        return unchecked((ushort)IPAddress.NetworkToHostOrder((short)raw));
    }

    private sealed class NetworkAccumulator
    {
        public int TcpListening { get; set; }
        public int TcpEstablished { get; set; }
        public int UdpEndpoints { get; set; }
        public int PublicRemoteConnections { get; set; }
        public int PrivateRemoteConnections { get; set; }
        public int LoopbackRemoteConnections { get; set; }
        public HashSet<int> ListeningTcpPorts { get; } = new();
        public HashSet<int> UdpPorts { get; } = new();
        public List<NetworkEndpointSnapshot> Endpoints { get; } = new();
        public bool HasAnySocket => TcpListening > 0 || TcpEstablished > 0 || UdpEndpoints > 0 || Endpoints.Count > 0;
    }

    private sealed record TreeEntry(int Pid, int ParentPid, string Name);
}
