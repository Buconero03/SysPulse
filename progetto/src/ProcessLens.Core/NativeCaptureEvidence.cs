using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Text;

namespace ProcessLens.Core;

public sealed record NativeCaptureSource(string Key, string Name);
public sealed record NativeCaptureDiscovery(bool Available, IReadOnlyList<NativeCaptureSource> Sources, string Note);
public sealed record NativePacketRow(string Time, string Protocol, string Source, string Destination, string Direction,
    int Bytes, ushort Component, uint DropReason)
{
    public bool HasComponent { get; init; } = true;
    public bool HasDropReason { get; init; } = true;
    public string ComponentText => HasComponent ? Component.ToString(CultureInfo.CurrentCulture) : "N/D";
    public string DropReasonText => HasDropReason ? DropReason.ToString(CultureInfo.CurrentCulture) : "N/D";
}
public sealed record NativeCaptureResult(bool Available, bool Partial, IReadOnlyList<NativePacketRow> Rows,
    ulong MissedWrites, ulong MissedReads, string Note)
{
    public bool LossCountsAvailable { get; init; } = true;
    public string SourceName { get; init; } = "N/D";
    public DateTimeOffset? StartedUtc { get; init; }
    public DateTimeOffset? EndedUtc { get; init; }
}
public sealed record WifiBssRow(string Adapter, string Ssid, string Bssid, int Rssi, uint FrequencyKhz, string BssType)
{
    public string Frequency => (FrequencyKhz / 1000d).ToString("0.###", CultureInfo.CurrentCulture) + " MHz";
}
public sealed record NativeWifiResult(bool Available, bool Partial, IReadOnlyList<WifiBssRow> Rows, string Note);
public interface INativeCaptureClient
{
    NativeCaptureDiscovery Discover(CancellationToken cancellation);
    NativeCaptureResult Capture(NativeCaptureSource source, CancellationToken cancellation);
    NativeWifiResult ReadWifi(CancellationToken cancellation);
    void Release();
}

public static class NativeCaptureEvidence
{
    public const int MaximumRows = 2048, SnapshotBytes = 128, DurationSeconds = 20;
    public const int MaximumNativeBuffer = 1048576, MaximumSources = 256, BssEntrySize = 360, MaximumBssRows = 512;
    public const string Limits = "Traffico visibile al PC, non all’intera rete. Le righe sono osservazioni: lo stesso pacchetto può comparire più volte. Nessun conteggio dei client Wi-Fi, controllo di intrusioni o verifica di chi osserva l’IP.";
    public const string WifiLimits = "Cache radio Windows: punti di accesso o peer ad hoc rilevati durante le scansioni del sistema, non client connessi. I dati possono essere precedenti alla lettura. Nessuna scansione forzata, cambio canale o modalità monitor. Il nome della rete non identifica un hotspot telefonico.";
    public static string Clean(string? s, int limit = 160) => new((s ?? "").Take(limit).Select(c => char.IsControl(c) || char.GetUnicodeCategory(c) is UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator ? ' ' : c).ToArray());
    public static NativePacketRow Decode(ReadOnlySpan<byte> bytes, ushort packetType, ushort direction, ushort component,
        uint drop, long fileTime)
    {
        string time;
        try { time = DateTime.FromFileTimeUtc(fileTime).ToLocalTime().ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture); }
        catch (ArgumentOutOfRangeException) { time = "N/D"; }
        string protocol = packetType == 2 ? "802.11 · non decodificato" : "Tipo " + packetType;
        string src = "N/D", dst = "N/D";
        ReadOnlySpan<byte> ip = default;
        if (packetType == 1 && bytes.Length >= 14)
        {
            ushort ether = BinaryPrimitives.ReadUInt16BigEndian(bytes[12..]); int start = 14;
            for (int i = 0; i < 2 && ether is 0x8100 or 0x88A8; i++)
            { if (bytes.Length < start + 4) { ether = 0; break; } ether = BinaryPrimitives.ReadUInt16BigEndian(bytes[(start + 2)..]); start += 4; }
            protocol = ether switch { 0x0800 => "IPv4", 0x86DD => "IPv6", 0x0806 => "ARP", _ => "Ethernet" };
            if (ether is 0x0800 or 0x86DD) ip = bytes[start..];
        }
        else if (packetType == 3) ip = bytes;
        if (ip.Length >= 20 && ip[0] >> 4 == 4)
        {
            int h = (ip[0] & 15) * 4, total = BinaryPrimitives.ReadUInt16BigEndian(ip[2..]);
            if (h >= 20 && h <= ip.Length && total >= h)
            {
                src = new IPAddress(ip.Slice(12, 4)).ToString(); dst = new IPAddress(ip.Slice(16, 4)).ToString();
                protocol = ip[9] switch { 6 => "TCP/IPv4", 17 => "UDP/IPv4", 1 => "ICMP/IPv4", _ => "IPv4 · protocollo " + ip[9] };
                // A fragmented datagram is not used to infer transport ports.
                if ((BinaryPrimitives.ReadUInt16BigEndian(ip[6..]) & 0x3fff) == 0 && ip[9] is 6 or 17 && total >= h + 4 && ip.Length >= h + 4)
                { src += ":" + BinaryPrimitives.ReadUInt16BigEndian(ip[h..]); dst += ":" + BinaryPrimitives.ReadUInt16BigEndian(ip[(h + 2)..]); }
            }
            else protocol = "IPv4 · intestazione incompleta";
        }
        else if (ip.Length >= 40 && ip[0] >> 4 == 6)
        {
            src = new IPAddress(ip.Slice(8, 16)).ToString(); dst = new IPAddress(ip.Slice(24, 16)).ToString();
            protocol = "IPv6 · next header " + ip[6];
            // No port claim after extension headers or fragments.
            if (ip[6] is 6 or 17 && BinaryPrimitives.ReadUInt16BigEndian(ip[4..]) >= 4 && ip.Length >= 44)
            { protocol = ip[6] == 6 ? "TCP/IPv6" : "UDP/IPv6"; src = "[" + src + "]:" + BinaryPrimitives.ReadUInt16BigEndian(ip[40..]); dst = "[" + dst + "]:" + BinaryPrimitives.ReadUInt16BigEndian(ip[42..]); }
        }
        string dir = direction switch { 1 => "In", 2 => "Out", 3 => "Rx", 4 => "Tx", 5 => "Ingress", 6 => "Egress", _ => "N/D" };
        return new(time, protocol, src, dst, dir, bytes.Length, component, drop);
    }
    // Windows SDK WLAN_BSS_ENTRY: size 360, alignment 8. Information elements are never parsed.
    public static IReadOnlyList<WifiBssRow> ParseBss(ReadOnlySpan<byte> buffer, string adapter)
    {
        if (buffer.Length < 8) throw new InvalidDataException("Tabella BSS incompleta.");
        uint size = BinaryPrimitives.ReadUInt32LittleEndian(buffer), count = BinaryPrimitives.ReadUInt32LittleEndian(buffer[4..]);
        if (size != buffer.Length || count > MaximumBssRows || 8L + count * BssEntrySize > size)
            throw new InvalidDataException("Dimensione o conteggio BSS non valido.");
        var rows = new List<WifiBssRow>((int)count);
        for (int i = 0; i < count; i++)
        {
            var b = buffer.Slice(8 + i * BssEntrySize, BssEntrySize); uint len = BinaryPrimitives.ReadUInt32LittleEndian(b);
            uint kind = BinaryPrimitives.ReadUInt32LittleEndian(b[48..]);
            if (len > 32 || kind is not (1 or 2)) throw new InvalidDataException("SSID o tipo BSS non valido.");
            string ssid;
            try { ssid = new UTF8Encoding(false, true).GetString(b.Slice(4, (int)len)); }
            catch (DecoderFallbackException) { ssid = "SSID hex: " + Convert.ToHexString(b.Slice(4, (int)len)); }
            string mac = string.Join("-", b.Slice(40, 6).ToArray().Select(x => x.ToString("X2", CultureInfo.InvariantCulture)));
            rows.Add(new(Clean(adapter), len == 0 ? "SSID non trasmesso" : Clean(ssid, 96), mac,
                BinaryPrimitives.ReadInt32LittleEndian(b[56..]), BinaryPrimitives.ReadUInt32LittleEndian(b[92..]), kind == 1 ? "Punto di accesso" : "Peer ad hoc"));
        }
        return rows;
    }
    public static string Report(NativeCaptureResult result) => "CATTURA WINDOWS\nSorgente: " + Clean(result.SourceName) +
        "\nInizio UTC: " + (result.StartedUtc?.ToString("O", CultureInfo.InvariantCulture) ?? "N/D") +
        "\nFine UTC: " + (result.EndedUtc?.ToString("O", CultureInfo.InvariantCulture) ?? "N/D") + "\n" + result.Note + "\n" + Limits +
        $"\nOsservazioni: {(result.Available ? result.Rows.Count.ToString(CultureInfo.CurrentCulture) : "N/D")} · dati " + (result.Partial ? "parziali" : "nel limite di raccolta") +
        (result.Available && result.LossCountsAvailable ? $"\nPerdite segnalate: scrittura {result.MissedWrites}, lettura {result.MissedReads}\n" : "\nPerdite segnalate: N/D\n") +
        "Ora\tProtocollo\tSorgente\tDestinazione\tDirezione\tByte campionati\tComponente\tScarto\n" +
        string.Join("\n", result.Rows.Select(x => $"{x.Time}\t{x.Protocol}\t{x.Source}\t{x.Destination}\t{x.Direction}\t{x.Bytes}\t{x.ComponentText}\t{x.DropReasonText}"));
    public static string Report(NativeWifiResult result) => "WI-FI · CACHE BSS WINDOWS\n" + result.Note + "\n" + WifiLimits +
        "\nScheda\tSSID\tBSSID\tRSSI dBm\tFrequenza\tTipo\n" + string.Join("\n", result.Rows.Select(x => $"{x.Adapter}\t{x.Ssid}\t{x.Bssid}\t{x.Rssi}\t{x.Frequency}\t{x.BssType}"));
}
