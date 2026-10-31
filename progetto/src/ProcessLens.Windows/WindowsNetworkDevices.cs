using System.ComponentModel;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using ProcessLens.Core;

namespace ProcessLens.Windows;

// Passive local cache reader. No probes, name lookups, packet capture or shell process.
public sealed class WindowsNetworkDevices : INetworkDeviceSource
{
    private const int MaximumRows = 4096;
    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    private static extern uint GetIpNetTable2(ushort family, out IntPtr table);
    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    private static extern void FreeMibTable(IntPtr table);

    [StructLayout(LayoutKind.Explicit, Size = 28)]
    internal struct NativeAddress
    {
        [FieldOffset(0)] public ushort Family;
        [FieldOffset(4)] public uint V4;
        [FieldOffset(8)] public uint V6A;
        [FieldOffset(12)] public uint V6B;
        [FieldOffset(16)] public uint V6C;
        [FieldOffset(20)] public uint V6D;
        [FieldOffset(24)] public uint Scope;
        public IPAddress? ToAddress()
        {
            if (Family == 2) return new IPAddress(BitConverter.GetBytes(V4));
            if (Family != 23) return null;
            byte[] bytes = new byte[16];
            BitConverter.GetBytes(V6A).CopyTo(bytes, 0);
            BitConverter.GetBytes(V6B).CopyTo(bytes, 4);
            BitConverter.GetBytes(V6C).CopyTo(bytes, 8);
            BitConverter.GetBytes(V6D).CopyTo(bytes, 12);
            return new IPAddress(bytes, Scope);
        }
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeRow
    {
        public NativeAddress Address;
        public uint InterfaceIndex;
        public ulong InterfaceLuid;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] PhysicalAddress;
        public uint PhysicalAddressLength;
        public uint State;
        public byte Flags;
        public uint ReachabilityTime;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeHeader { public uint Count; public NativeRow First; }
    internal sealed record RawNeighbor(uint Index, IPAddress Address, string Mac, NeighborState State, bool Router);

    internal static IReadOnlyList<RawNeighbor> ReadTable(IntPtr table, out bool truncated)
    {
        truncated = false;
        if (table == IntPtr.Zero) return Array.Empty<RawNeighbor>();
        uint count = unchecked((uint)Marshal.ReadInt32(table));
        truncated = count > MaximumRows;
        int offset = Marshal.OffsetOf<NativeHeader>(nameof(NativeHeader.First)).ToInt32();
        int stride = Marshal.SizeOf<NativeRow>();
        var rows = new List<RawNeighbor>();
        for (int i = 0; i < Math.Min(count, (uint)MaximumRows); i++)
        {
            var row = Marshal.PtrToStructure<NativeRow>(IntPtr.Add(table, checked(offset + i * stride)));
            var ip = row.Address.ToAddress();
            if (ip is null || row.State > (uint)NeighborState.Permanent) { truncated = true; continue; }
            string mac = row.PhysicalAddressLength == 6
                ? string.Join("-", row.PhysicalAddress.Take(6).Select(b => b.ToString("X2"))) : "";
            // An explicit unreachable flag must not become recently reachable.
            var state = (row.Flags & 2) != 0 ? NeighborState.Unreachable : (NeighborState)row.State;
            rows.Add(new(row.InterfaceIndex, ip, NetworkDeviceRules.NormalizeMac(mac), state, (row.Flags & 1) != 0));
        }
        return rows;
    }

    private sealed record Adapter(string Id, string Name, uint[] Indices, IPAddress[] Local, IPAddress[] Broadcast,
        IPAddress[] Gateways)
    {
        public string Signature => Id + "|" + string.Join(",", Indices) + "|" +
            string.Join(",", Local.Select(x => x.ToString()).Order()) + "|" + string.Join(",", Gateways.Select(x => x.ToString()).Order());
    }
    private static List<Adapter> Adapters(out bool partial, out string note)
    {
        partial = false;
        var errors = new List<string>();
        var result = new List<Adapter>();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up ||
                nic.NetworkInterfaceType is not (NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211)) continue;
            try
            {
                var p = nic.GetIPProperties();
                var indices = new List<uint>();
                if (nic.Supports(NetworkInterfaceComponent.IPv4)) indices.Add((uint)p.GetIPv4Properties().Index);
                if (nic.Supports(NetworkInterfaceComponent.IPv6)) indices.Add((uint)p.GetIPv6Properties().Index);
                var broadcast = new List<IPAddress>();
                foreach (var u in p.UnicastAddresses.Where(u => u.Address.AddressFamily == AddressFamily.InterNetwork && u.PrefixLength < 31))
                {
                    byte[] a = u.Address.GetAddressBytes(), mask = u.IPv4Mask.GetAddressBytes();
                    broadcast.Add(new IPAddress(a.Select((v, i) => (byte)(v | (byte)~mask[i])).ToArray()));
                }
                result.Add(new(nic.Id, nic.Name, indices.Distinct().ToArray(),
                    p.UnicastAddresses.Select(u => u.Address).ToArray(), broadcast.ToArray(),
                    p.GatewayAddresses.Select(g => g.Address).Where(g => NetworkDeviceRules.IsUnicastAddress(g.ToString())).ToArray()));
            }
            catch (NetworkInformationException ex) { partial = true; errors.Add(nic.Name + ": " + ex.Message); }
        }
        note = string.Join("\n", errors.Take(8));
        if (errors.Count > 8) note += $"\nAltre schede con errore: {errors.Count - 8}.";
        return result;
    }

    private static bool SameAddress(IPAddress a, IPAddress b) => a.GetAddressBytes().SequenceEqual(b.GetAddressBytes());
    public NetworkDeviceSnapshot Capture()
    {
        IntPtr table = IntPtr.Zero;
        try
        {
            if (!OperatingSystem.IsWindows()) return new(DateTime.UtcNow, false, false, "Lettura disponibile solo su Windows.", 0, Array.Empty<NetworkNeighbor>());
            var adapters = Adapters(out bool partial, out string adapterNote);
            uint error = GetIpNetTable2(0, out table); // AF_UNSPEC: IPv4 + IPv6
            if (error is not (0 or 1168)) throw new Win32Exception((int)error);
            var native = ReadTable(table, out bool truncated);
            var after = Adapters(out bool afterPartial, out string afterNote);
            bool changed = !adapters.Select(a => a.Signature).Order().SequenceEqual(after.Select(a => a.Signature).Order());
            partial |= afterPartial || truncated || changed;
            var neighbors = new List<NetworkNeighbor>();
            foreach (var adapter in adapters)
            {
                var rows = native.Where(n => adapter.Indices.Contains(n.Index)).ToArray();
                string scope = changed ? "" : NetworkDeviceRules.NetworkKey(adapter.Id, adapter.Gateways.Select(g =>
                {
                    var match = rows.FirstOrDefault(n => SameAddress(n.Address, g) && n.State is not (NeighborState.Incomplete or NeighborState.Unreachable));
                    return (g.ToString(), match?.Mac ?? "");
                }));
                foreach (var n in rows)
                {
                    if (!NetworkDeviceRules.IsUnicastAddress(n.Address.ToString()) || adapter.Local.Any(a => SameAddress(a, n.Address)) ||
                        adapter.Broadcast.Any(a => SameAddress(a, n.Address))) continue;
                    neighbors.Add(new(adapter.Id, adapter.Name, scope, n.Address.ToString(), n.Mac, n.State,
                        adapter.Gateways.Any(g => SameAddress(g, n.Address))));
                }
            }
            string note = adapters.Count == 0 ? (partial ? "Schede Ethernet/Wi-Fi: lettura incompleta." : "Nessuna scheda Ethernet o Wi-Fi attiva.") :
                partial ? "Lettura parziale: elenco limitato, scheda non leggibile o rete cambiata durante il controllo." : "";
            note = string.Join("\n", new[] { note, adapterNote, afterNote }.Where(n => n.Length > 0).Distinct());
            return new(DateTime.UtcNow, true, partial, note, adapters.Count, neighbors);
        }
        catch (Exception ex) when (ex is Win32Exception or NetworkInformationException or DllNotFoundException or EntryPointNotFoundException)
        { return new(DateTime.UtcNow, false, false, "Lettura rete non riuscita: " + ex.Message, 0, Array.Empty<NetworkNeighbor>()); }
        finally { if (table != IntPtr.Zero) FreeMibTable(table); }
    }
}
