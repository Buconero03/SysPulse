using System.Net;
using System.Runtime.InteropServices;
using ProcessLens.Core;

namespace ProcessLens.Windows;

/// <summary>
/// Fast system-wide socket and per-PID network throughput sampler.
/// Reads extended TCP/UDP owner tables once per tick and queries TCP EStats data counters
/// when available, with per-PID I/O transfer delta fallback for active UDP/QUIC or non-elevated sockets.
/// </summary>
public sealed class WindowsLiveNetworkSampler
{
    private const int TcpConnectionEstatsData = 1;

    private sealed class PidNetworkAccumulator
    {
        public int TcpEstablished;
        public int TcpListening;
        public int UdpEndpoints;
        public int PublicRemoteConnections;
        public ulong EStatsBytesOutDelta;
        public ulong EStatsBytesInDelta;
        public bool EStatsMeasured;
    }

    private readonly Dictionary<string, (ulong BytesOut, ulong BytesIn, double Timestamp)> _connHistory = new(StringComparer.Ordinal);
    private readonly HashSet<string> _estatsEnabled = new(StringComparer.Ordinal);
    private readonly Dictionary<int, (DateTime StartUtc, ulong OtherIoBytes, double Timestamp)> _ioHistory = new();
    private double _lastSampleMonotonic;
    private bool _estatsSetDenied;

    public void Reset()
    {
        _connHistory.Clear();
        _estatsEnabled.Clear();
        _ioHistory.Clear();
        _lastSampleMonotonic = 0;
    }

    public IReadOnlyDictionary<int, LiveNetworkSample> Capture(
        IReadOnlyCollection<int> pids,
        IReadOnlyDictionary<int, (DateTime? StartUtc, ulong OtherIoBytes, bool IoAvailable)> processIo,
        double nowMonotonic,
        DateTime nowUtc)
    {
        var requested = pids.Where(x => x > 0).ToHashSet();
        var stats = new Dictionary<int, PidNetworkAccumulator>(requested.Count);
        foreach (int pid in requested)
            stats[pid] = new PidNetworkAccumulator();

        double elapsed = _lastSampleMonotonic > 0 ? nowMonotonic - _lastSampleMonotonic : 0;
        bool validInterval = elapsed is > 0.05 and <= CpuIntervalTracker.MaximumGapSeconds;
        _lastSampleMonotonic = nowMonotonic;

        var activeConnections = new HashSet<string>(StringComparer.Ordinal);
        bool anyTableRead = false;

        try { ReadTcpIPv4(requested, stats, activeConnections, nowMonotonic, validInterval); anyTableRead = true; } catch { }
        try { ReadTcpIPv6(requested, stats, activeConnections, nowMonotonic, validInterval); anyTableRead = true; } catch { }
        try { ReadUdpIPv4(requested, stats); anyTableRead = true; } catch { }
        try { ReadUdpIPv6(requested, stats); anyTableRead = true; } catch { }

        foreach (string staleKey in _connHistory.Keys.Where(k => !activeConnections.Contains(k)).ToArray())
        {
            _connHistory.Remove(staleKey);
            _estatsEnabled.Remove(staleKey);
        }
        if (_connHistory.Count > 4096)
        {
            _connHistory.Clear();
            _estatsEnabled.Clear();
        }

        foreach (int stalePid in _ioHistory.Keys.Where(p => !requested.Contains(p)).ToArray())
            _ioHistory.Remove(stalePid);

        var result = new Dictionary<int, LiveNetworkSample>(requested.Count);
        foreach (int pid in requested)
        {
            var acc = stats[pid];
            processIo.TryGetValue(pid, out var info);
            double sendRate = 0;
            double recvRate = 0;
            double totalRate = 0;
            bool bandwidthAvailable = false;

            if (acc.EStatsMeasured && validInterval)
            {
                sendRate = acc.EStatsBytesOutDelta / elapsed;
                recvRate = acc.EStatsBytesInDelta / elapsed;
                totalRate = sendRate + recvRate;
                bandwidthAvailable = true;
            }

            if (info.IoAvailable && info.StartUtc.HasValue)
            {
                if (_ioHistory.TryGetValue(pid, out var prevIo) &&
                    prevIo.StartUtc == info.StartUtc.Value &&
                    info.OtherIoBytes >= prevIo.OtherIoBytes &&
                    nowMonotonic - prevIo.Timestamp is > 0.05 and <= CpuIntervalTracker.MaximumGapSeconds)
                {
                    double ioInterval = nowMonotonic - prevIo.Timestamp;
                    double otherRate = (info.OtherIoBytes - prevIo.OtherIoBytes) / ioInterval;
                    // When a process has active network sockets (e.g. QUIC/UDP or non-elevated TCP),
                    // use OtherTransferCount delta if EStats did not report higher TCP traffic.
                    if ((acc.TcpEstablished > 0 || acc.UdpEndpoints > 0) && otherRate > totalRate)
                    {
                        totalRate = otherRate;
                        if (sendRate + recvRate < totalRate)
                        {
                            recvRate = Math.Max(recvRate, totalRate - sendRate);
                        }
                        bandwidthAvailable = true;
                    }
                    else if (!bandwidthAvailable)
                    {
                        bandwidthAvailable = anyTableRead;
                    }
                }
                else if (anyTableRead)
                {
                    bandwidthAvailable = true;
                }
                _ioHistory[pid] = (info.StartUtc.Value, info.OtherIoBytes, nowMonotonic);
            }
            else if (anyTableRead)
            {
                bandwidthAvailable = true;
            }

            result[pid] = new LiveNetworkSample(
                pid,
                info.StartUtc,
                acc.TcpEstablished,
                acc.TcpListening,
                acc.UdpEndpoints,
                acc.PublicRemoteConnections,
                totalRate,
                sendRate,
                recvRate,
                bandwidthAvailable,
                anyTableRead,
                nowMonotonic,
                nowUtc);
        }

        return result;
    }

    private void ReadTcpIPv4(
        HashSet<int> targetPids,
        Dictionary<int, PidNetworkAccumulator> stats,
        HashSet<string> activeConnections,
        double now,
        bool validInterval)
    {
        int stride = Marshal.SizeOf<NativeMethods.MIB_TCPROW_OWNER_PID>();
        uint Query(IntPtr buffer, ref int size) =>
            NativeMethods.GetExtendedTcpTable(buffer, ref size, false, NativeMethods.AF_INET, NativeMethods.TCP_TABLE_OWNER_PID_ALL, 0);

        OwnedNetworkTableReader.Read(stride, Query, (rowPtr, count) =>
        {
            for (int i = 0; i < count; i++)
            {
                var row = Marshal.PtrToStructure<NativeMethods.MIB_TCPROW_OWNER_PID>(IntPtr.Add(rowPtr, i * stride));
                int pid = unchecked((int)row.dwOwningPid);
                if (!targetPids.Contains(pid) || !stats.TryGetValue(pid, out var acc))
                    continue;

                if (row.dwState == NativeMethods.MIB_TCP_STATE_LISTEN)
                {
                    acc.TcpListening++;
                }
                else if (row.dwState == NativeMethods.MIB_TCP_STATE_ESTAB)
                {
                    acc.TcpEstablished++;
                    if (IsPublicIPv4(row.dwRemoteAddr))
                        acc.PublicRemoteConnections++;

                    string key = $"4:{pid}:{row.dwLocalAddr:X8}:{row.dwLocalPort:X4}:{row.dwRemoteAddr:X8}:{row.dwRemotePort:X4}";
                    activeConnections.Add(key);
                    TrySampleTcp4EStats(row, key, acc, now, validInterval);
                }
            }
        });
    }

    private void ReadTcpIPv6(
        HashSet<int> targetPids,
        Dictionary<int, PidNetworkAccumulator> stats,
        HashSet<string> activeConnections,
        double now,
        bool validInterval)
    {
        int stride = Marshal.SizeOf<NativeMethods.MIB_TCP6ROW_OWNER_PID>();
        uint Query(IntPtr buffer, ref int size) =>
            NativeMethods.GetExtendedTcpTable(buffer, ref size, false, NativeMethods.AF_INET6, NativeMethods.TCP_TABLE_OWNER_PID_ALL, 0);

        OwnedNetworkTableReader.Read(stride, Query, (rowPtr, count) =>
        {
            for (int i = 0; i < count; i++)
            {
                var row = Marshal.PtrToStructure<NativeMethods.MIB_TCP6ROW_OWNER_PID>(IntPtr.Add(rowPtr, i * stride));
                int pid = unchecked((int)row.dwOwningPid);
                if (!targetPids.Contains(pid) || !stats.TryGetValue(pid, out var acc))
                    continue;

                if (row.dwState == NativeMethods.MIB_TCP_STATE_LISTEN)
                {
                    acc.TcpListening++;
                }
                else if (row.dwState == NativeMethods.MIB_TCP_STATE_ESTAB)
                {
                    acc.TcpEstablished++;
                    if (row.ucRemoteAddr is { Length: 16 } && IsPublicIPv6(row.ucRemoteAddr))
                        acc.PublicRemoteConnections++;

                    if (row.ucLocalAddr is { Length: 16 } && row.ucRemoteAddr is { Length: 16 })
                    {
                        string key = $"6:{pid}:{Convert.ToHexString(row.ucLocalAddr)}:{row.dwLocalPort:X4}:{Convert.ToHexString(row.ucRemoteAddr)}:{row.dwRemotePort:X4}";
                        activeConnections.Add(key);
                        TrySampleTcp6EStats(row, key, acc, now, validInterval);
                    }
                }
            }
        });
    }

    private static void ReadUdpIPv4(HashSet<int> targetPids, Dictionary<int, PidNetworkAccumulator> stats)
    {
        int stride = Marshal.SizeOf<NativeMethods.MIB_UDPROW_OWNER_PID>();
        uint Query(IntPtr buffer, ref int size) =>
            NativeMethods.GetExtendedUdpTable(buffer, ref size, false, NativeMethods.AF_INET, NativeMethods.UDP_TABLE_OWNER_PID, 0);

        OwnedNetworkTableReader.Read(stride, Query, (rowPtr, count) =>
        {
            for (int i = 0; i < count; i++)
            {
                var row = Marshal.PtrToStructure<NativeMethods.MIB_UDPROW_OWNER_PID>(IntPtr.Add(rowPtr, i * stride));
                int pid = unchecked((int)row.dwOwningPid);
                if (targetPids.Contains(pid) && stats.TryGetValue(pid, out var acc))
                    acc.UdpEndpoints++;
            }
        });
    }

    private static void ReadUdpIPv6(HashSet<int> targetPids, Dictionary<int, PidNetworkAccumulator> stats)
    {
        int stride = Marshal.SizeOf<NativeMethods.MIB_UDP6ROW_OWNER_PID>();
        uint Query(IntPtr buffer, ref int size) =>
            NativeMethods.GetExtendedUdpTable(buffer, ref size, false, NativeMethods.AF_INET6, NativeMethods.UDP_TABLE_OWNER_PID, 0);

        OwnedNetworkTableReader.Read(stride, Query, (rowPtr, count) =>
        {
            for (int i = 0; i < count; i++)
            {
                var row = Marshal.PtrToStructure<NativeMethods.MIB_UDP6ROW_OWNER_PID>(IntPtr.Add(rowPtr, i * stride));
                int pid = unchecked((int)row.dwOwningPid);
                if (targetPids.Contains(pid) && stats.TryGetValue(pid, out var acc))
                    acc.UdpEndpoints++;
            }
        });
    }

    private void TrySampleTcp4EStats(
        NativeMethods.MIB_TCPROW_OWNER_PID ownerRow,
        string key,
        PidNetworkAccumulator acc,
        double now,
        bool validInterval)
    {
        var tcpRow = new MIB_TCPROW
        {
            dwState = ownerRow.dwState,
            dwLocalAddr = ownerRow.dwLocalAddr,
            dwLocalPort = ownerRow.dwLocalPort,
            dwRemoteAddr = ownerRow.dwRemoteAddr,
            dwRemotePort = ownerRow.dwRemotePort
        };

        if (!_estatsSetDenied && _estatsEnabled.Add(key))
        {
            byte rw = 1;
            uint setRc = SetPerTcpConnectionEStats(ref tcpRow, TcpConnectionEstatsData, ref rw, 0, 1, 0);
            if (setRc == 5) // ERROR_ACCESS_DENIED
                _estatsSetDenied = true;
        }

        uint getRc = GetPerTcpConnectionEStats(
            ref tcpRow,
            TcpConnectionEstatsData,
            out byte enabled,
            0,
            1,
            IntPtr.Zero,
            0,
            0,
            out TCP_ESTATS_DATA_ROD_v0 rod,
            0,
            (uint)Marshal.SizeOf<TCP_ESTATS_DATA_ROD_v0>());

        if (getRc == 0 && enabled != 0)
        {
            acc.EStatsMeasured = true;
            if (validInterval && _connHistory.TryGetValue(key, out var prev))
            {
                if (rod.DataBytesOut >= prev.BytesOut)
                    acc.EStatsBytesOutDelta += rod.DataBytesOut - prev.BytesOut;
                if (rod.DataBytesIn >= prev.BytesIn)
                    acc.EStatsBytesInDelta += rod.DataBytesIn - prev.BytesIn;
            }
            _connHistory[key] = (rod.DataBytesOut, rod.DataBytesIn, now);
        }
    }

    private void TrySampleTcp6EStats(
        NativeMethods.MIB_TCP6ROW_OWNER_PID ownerRow,
        string key,
        PidNetworkAccumulator acc,
        double now,
        bool validInterval)
    {
        var tcp6Row = new MIB_TCP6ROW
        {
            State = ownerRow.dwState,
            LocalAddr = ownerRow.ucLocalAddr!,
            LocalScopeId = ownerRow.dwLocalScopeId,
            LocalPort = ownerRow.dwLocalPort,
            RemoteAddr = ownerRow.ucRemoteAddr!,
            RemoteScopeId = ownerRow.dwRemoteScopeId,
            RemotePort = ownerRow.dwRemotePort
        };

        if (!_estatsSetDenied && _estatsEnabled.Add(key))
        {
            byte rw = 1;
            uint setRc = SetPerTcp6ConnectionEStats(ref tcp6Row, TcpConnectionEstatsData, ref rw, 0, 1, 0);
            if (setRc == 5)
                _estatsSetDenied = true;
        }

        uint getRc = GetPerTcp6ConnectionEStats(
            ref tcp6Row,
            TcpConnectionEstatsData,
            out byte enabled,
            0,
            1,
            IntPtr.Zero,
            0,
            0,
            out TCP_ESTATS_DATA_ROD_v0 rod,
            0,
            (uint)Marshal.SizeOf<TCP_ESTATS_DATA_ROD_v0>());

        if (getRc == 0 && enabled != 0)
        {
            acc.EStatsMeasured = true;
            if (validInterval && _connHistory.TryGetValue(key, out var prev))
            {
                if (rod.DataBytesOut >= prev.BytesOut)
                    acc.EStatsBytesOutDelta += rod.DataBytesOut - prev.BytesOut;
                if (rod.DataBytesIn >= prev.BytesIn)
                    acc.EStatsBytesInDelta += rod.DataBytesIn - prev.BytesIn;
            }
            _connHistory[key] = (rod.DataBytesOut, rod.DataBytesIn, now);
        }
    }

    private static bool IsPublicIPv4(uint addr)
    {
        byte b0 = (byte)(addr & 0xFF);
        byte b1 = (byte)((addr >> 8) & 0xFF);
        if (b0 is 0 or 10 or 127 || b0 >= 224) return false;
        if (b0 == 172 && b1 is >= 16 and <= 31) return false;
        if (b0 == 192 && b1 == 168) return false;
        if (b0 == 169 && b1 == 254) return false;
        if (b0 == 100 && b1 is >= 64 and <= 127) return false;
        return true;
    }

    private static bool IsPublicIPv6(byte[] b)
    {
        bool allZeroExceptLast = true;
        for (int i = 0; i < 15; i++)
        {
            if (b[i] != 0) { allZeroExceptLast = false; break; }
        }
        if (allZeroExceptLast && b[15] is 0 or 1) return false;
        if ((b[0] & 0xFE) == 0xFC) return false;
        if (b[0] == 0xFE && (b[1] & 0xC0) == 0x80) return false;
        if (b[0] == 0xFF) return false;
        return true;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_TCPROW
    {
        public uint dwState;
        public uint dwLocalAddr;
        public uint dwLocalPort;
        public uint dwRemoteAddr;
        public uint dwRemotePort;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_TCP6ROW
    {
        public uint State;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] LocalAddr;
        public uint LocalScopeId;
        public uint LocalPort;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] RemoteAddr;
        public uint RemoteScopeId;
        public uint RemotePort;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TCP_ESTATS_DATA_ROD_v0
    {
        public ulong DataBytesOut;
        public ulong DataSegsOut;
        public ulong DataBytesIn;
        public ulong DataSegsIn;
        public ulong SegsOut;
        public ulong SegsIn;
        public uint ThruBytesAcked;
        public uint ThruBytesReceived;
    }

    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    private static extern uint GetPerTcpConnectionEStats(
        ref MIB_TCPROW Row,
        int EstatsType,
        out byte Rw,
        uint RwVersion,
        uint RwSize,
        IntPtr Ros,
        uint RosVersion,
        uint RosSize,
        out TCP_ESTATS_DATA_ROD_v0 Rod,
        uint RodVersion,
        uint RodSize);

    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    private static extern uint SetPerTcpConnectionEStats(
        ref MIB_TCPROW Row,
        int EstatsType,
        ref byte Rw,
        uint RwVersion,
        uint RwSize,
        uint Offset);

    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    private static extern uint GetPerTcp6ConnectionEStats(
        ref MIB_TCP6ROW Row,
        int EstatsType,
        out byte Rw,
        uint RwVersion,
        uint RwSize,
        IntPtr Ros,
        uint RosVersion,
        uint RosSize,
        out TCP_ESTATS_DATA_ROD_v0 Rod,
        uint RodVersion,
        uint RodSize);

    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    private static extern uint SetPerTcp6ConnectionEStats(
        ref MIB_TCP6ROW Row,
        int EstatsType,
        ref byte Rw,
        uint RwVersion,
        uint RwSize,
        uint Offset);
}
