using System.Net;
using System.Runtime.InteropServices;
using ProcessLens.Guardian.Core;

namespace ProcessLens.Guardian.Service;

// IP Helper tables are snapshots, NOT a lossless network event stream. The collector
// never claims to observe every ephemeral connection between scans.
public static class WindowsPortSampler
{
    private const int AfInet = 2, AfInet6 = 23, ErrorInsufficientBuffer = 122;
    private const int TcpOwnerPidAll = 5, UdpOwnerPid = 1;
    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(IntPtr buffer, ref uint size, bool order, int family, int tableClass, uint reserved);
    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedUdpTable(IntPtr buffer, ref uint size, bool order, int family, int tableClass, uint reserved);

    public static IReadOnlyCollection<PortIdentity> Capture(bool includeRemoteAddresses)
    {
        var output = new HashSet<PortIdentity>();
        CaptureTable(output, false, false, includeRemoteAddresses);
        CaptureTable(output, false, true, includeRemoteAddresses);
        CaptureTable(output, true, false, includeRemoteAddresses);
        CaptureTable(output, true, true, includeRemoteAddresses);
        return output;
    }

    private static void CaptureTable(HashSet<PortIdentity> result, bool udp, bool ipv6, bool retainRemote)
    {
        int af = ipv6 ? AfInet6 : AfInet;
        uint bytes = 0;
        uint rc = udp ? GetExtendedUdpTable(IntPtr.Zero, ref bytes, false, af, UdpOwnerPid, 0)
            : GetExtendedTcpTable(IntPtr.Zero, ref bytes, false, af, TcpOwnerPidAll, 0);
        if (rc != ErrorInsufficientBuffer || bytes < 4 || bytes > 16_000_000) return;
        IntPtr ptr = Marshal.AllocHGlobal(checked((int)bytes));
        try
        {
            rc = udp ? GetExtendedUdpTable(ptr, ref bytes, false, af, UdpOwnerPid, 0)
                : GetExtendedTcpTable(ptr, ref bytes, false, af, TcpOwnerPidAll, 0);
            if (rc != 0 || bytes < 4) return;
            int count = Marshal.ReadInt32(ptr);
            int stride = udp ? (ipv6 ? 28 : 12) : (ipv6 ? 56 : 24);
            if (count < 0 || count > (bytes - 4) / stride) return;
            for (int i = 0; i < count; i++)
            {
                int row = 4 + i * stride;
                byte[] localIp; byte[] remoteIp = Array.Empty<byte>();
                int localPort; int remotePort = 0; int pid; int state = 0;
                if (!ipv6)
                {
                    state = udp ? 0 : Marshal.ReadInt32(ptr, row);
                    int off = row + (udp ? 0 : 4);
                    localIp = BitConverter.GetBytes(Marshal.ReadInt32(ptr, off));
                    localPort = ReadPort(ptr, off + 4);
                    if (!udp)
                    {
                        remoteIp = BitConverter.GetBytes(Marshal.ReadInt32(ptr, off + 8));
                        remotePort = ReadPort(ptr, off + 12);
                        pid = Marshal.ReadInt32(ptr, off + 16);
                    }
                    else pid = Marshal.ReadInt32(ptr, off + 8);
                }
                else
                {
                    localIp = new byte[16]; Marshal.Copy(IntPtr.Add(ptr, row), localIp, 0, 16);
                    localPort = ReadPort(ptr, row + 20);
                    if (!udp)
                    {
                        remoteIp = new byte[16]; Marshal.Copy(IntPtr.Add(ptr, row + 24), remoteIp, 0, 16);
                        remotePort = ReadPort(ptr, row + 44);
                        state = Marshal.ReadInt32(ptr, row + 48);
                        pid = Marshal.ReadInt32(ptr, row + 52);
                    }
                    else pid = Marshal.ReadInt32(ptr, row + 24);
                }
                if (pid <= 0) continue;
                // Exclude CLOSED/TIME_WAIT/DELETE_TCB: their PID association is
                // less reliable and they can flood the SQLite queue.
                if (!udp && (state == 1 || state == 11 || state == 12)) continue;
                string local = new IPAddress(localIp).ToString();
                string remote = !udp && retainRemote ? new IPAddress(remoteIp).ToString() : "";
                string status = udp ? "BOUND" : state switch
                {
                    2 => "LISTEN", 3 => "SYN_SENT", 4 => "SYN_RECEIVED", 5 => "ESTABLISHED",
                    6 => "FIN_WAIT1", 7 => "FIN_WAIT2", 8 => "CLOSE_WAIT", 9 => "CLOSING",
                    10 => "LAST_ACK", _ => $"STATE_{state}"
                };
                result.Add(new(pid, (udp ? "UDP" : "TCP") + (ipv6 ? "6" : "4"), local, localPort,
                    remote, remotePort, status));
            }
        }
        finally { Marshal.FreeHGlobal(ptr); }
    }
    // Native TCP/UDP port is stored in the first two bytes in network (big endian) order.
    private static int ReadPort(IntPtr buffer, int index) => Marshal.ReadByte(buffer, index) * 256 + Marshal.ReadByte(buffer, index + 1);
}
