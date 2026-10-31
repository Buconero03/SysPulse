using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ProcessLens.Core;

public sealed record CaptureInterface(string Name, string Description)
{
    public string Display => Description.Length > 0 ? Description + " · " + Name : Name;
}
public sealed record CaptureTools(string Dumpcap, string Tshark, string Wireshark, string DriverNote);
public sealed record CaptureDiscovery(CaptureTools? Tools, IReadOnlyList<CaptureInterface> Interfaces, string Detail);
public sealed record CaptureCheck(bool Supported, string Detail);
public sealed record CaptureFileResult(string Path, bool Complete, string Detail);
public sealed record PacketObservation(string Kind, string Mac, string Bssid, string Ssid,
    string Frequency, string Signal, int Frames);
public sealed record PacketEvidence(int Packets, int WifiFrames, bool Limited,
    IReadOnlyList<PacketObservation> Rows, string Note);
public interface IPacketCaptureClient
{
    Task<CaptureDiscovery> DiscoverAsync(string? dumpcapPath, CancellationToken token);
    Task<CaptureCheck> CheckMonitorAsync(CaptureTools tools, CaptureInterface adapter, CancellationToken token);
    Task<CaptureFileResult> CaptureAsync(CaptureTools tools, CaptureInterface adapter, bool monitor, string output, CancellationToken token);
    Task<PacketEvidence> AnalyzeAsync(CaptureTools tools, string path, CancellationToken token);
}

public static class PacketCapturePolicy
{
    public const int Seconds = 20, FileKilobytes = 10000, Packets = 10000, Snaplen = 256, AnalysisPackets = 2048;
    public const int MaximumFileBytes = 12 * 1024 * 1024, MaximumTextChars = 512 * 1024;
    public static bool LocalInterface(string name) => name == @"\Device\NPF_Loopback" ||
        (name.StartsWith(@"\Device\NPF_", StringComparison.Ordinal) && name.Length == 50 &&
         Guid.TryParseExact(name[12..], "B", out _));
    public static string[] CaptureArguments(CaptureInterface adapter, bool monitor, string path)
    {
        if (!LocalInterface(adapter.Name)) throw new ArgumentException("Interfaccia locale Npcap non valida.");
        if (!Path.IsPathFullyQualified(path) || path.StartsWith(@"\\", StringComparison.Ordinal) || !path.EndsWith(".pcapng", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Scegli un nuovo file locale .pcapng.");
        var args = new List<string> { "-i", adapter.Name };
        if (monitor) args.Add("-I"); else args.Add("-p");
        args.AddRange(new[] { "-a", "duration:20", "-a", "filesize:10000", "-c", "10000", "-s", "256", "-B", "2", "-q", "-w", path });
        return args.ToArray();
    }
    public static string[] AnalysisArguments(string path)
    {
        var args = new List<string> { "-n", "-r", path, "-c", "2048", "-T", "fields", "-E", "separator=/t", "-E", "quote=n", "-E", "occurrence=f", "-E", "escape=y" };
        foreach (string field in new[] { "frame.number", "wlan.ta", "wlan.ra", "wlan.bssid", "wlan.fc.type", "wlan.fc.subtype", "wlan.ssid", "radiotap.channel.freq", "radiotap.dbm_antsignal" })
            args.AddRange(new[] { "-e", field });
        return args.ToArray();
    }
    public static IReadOnlyList<CaptureInterface> ParseInterfaces(string text) => text.Split('\n').Take(512)
        .Select(line => Regex.Match(line.TrimEnd('\r'), @"^\d+\. (?<name>\S+)(?: \((?<description>.*)\))?$", RegexOptions.CultureInvariant))
        .Where(m => m.Success && LocalInterface(m.Groups["name"].Value))
        .Select(m => new CaptureInterface(m.Groups["name"].Value, m.Groups["description"].Value))
        .DistinctBy(x => x.Name).ToArray();
    public static bool HasMonitorLinkType(string text) => Regex.IsMatch(text, @"(?m)^\s*IEEE802_11(?:_RADIO)?(?:\s|\(|$)", RegexOptions.CultureInvariant);
}

public static class PacketEvidenceParser
{
    private static int? Number(string value)
    {
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return int.TryParse(value.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int hex) ? hex : null;
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) ? number : null;
    }
    private static string Ssid(string raw)
    {
        if (raw.Length == 0) return "N/D";
        string hex = raw.Replace(":", "", StringComparison.Ordinal);
        if (hex.Length > 64 || hex.Length % 2 != 0 || hex.Any(c => !Uri.IsHexDigit(c))) return "SSID non decodificato";
        try
        {
            string value = new UTF8Encoding(false, true).GetString(Convert.FromHexString(hex));
            return string.Concat(value.Select(c => char.IsControl(c) || char.GetUnicodeCategory(c) == UnicodeCategory.Format ? "\\u" + ((int)c).ToString("X4") : c.ToString()));
        }
        catch (DecoderFallbackException) { return "SSID hex: " + hex; }
    }
    public static PacketEvidence Parse(string text)
    {
        bool limited = text.Length > PacketCapturePolicy.MaximumTextChars;
        if (limited) text = text[..PacketCapturePolicy.MaximumTextChars];
        int packets = 0, wifi = 0;
        var rows = new Dictionary<(string Kind, string Mac, string Bssid, string Ssid, string Frequency, string Signal), int>();
        foreach (string line in text.Split('\n'))
        {
            if (line.Length == 0) continue;
            if (packets == PacketCapturePolicy.AnalysisPackets) { limited = true; break; }
            string[] f = line.TrimEnd('\r').Split('\t');
            if (f.Length != 9 || f.Any(v => v.Length > 512) || Number(f[0]) is not > 0) { limited = true; continue; }
            packets++;
            int? type = Number(f[4]), subtype = Number(f[5]);
            if (type is not (>= 0 and <= 3)) { if (f[4].Length > 0) limited = true; continue; }
            wifi++;
            string bssid = NetworkDeviceRules.NormalizeMac(f[3]);
            string ssid = type == 0 && subtype is 5 or 8 ? Ssid(f[6]) : "N/D";
            string frequency = Number(f[7]) is > 0 and < 100000 ? f[7] + " MHz" : "N/D";
            string signal = Number(f[8]) is >= -200 and <= 0 ? f[8] + " dBm" : "N/D";
            void Add(string kind, string mac)
            {
                mac = NetworkDeviceRules.NormalizeMac(mac);
                if (mac.Length == 0) return;
                var key = (kind, mac, bssid.Length > 0 ? bssid : "N/D", ssid, frequency, signal);
                if (rows.TryGetValue(key, out int count)) rows[key] = count + 1;
                else if (rows.Count < 512) rows[key] = 1;
                else limited = true;
            }
            Add("Trasmettitore nel frame", f[1]);
            Add("Destinatario nel frame", f[2]);
            Add(type == 0 && subtype is 5 or 8 ? "Rete annunciata" : "BSSID nel frame", f[3]);
        }
        if (packets == PacketCapturePolicy.AnalysisPackets) limited = true;
        var observations = rows.Select(r => new PacketObservation(r.Key.Kind, r.Key.Mac, r.Key.Bssid, r.Key.Ssid, r.Key.Frequency, r.Key.Signal, r.Value)).ToArray();
        return new(packets, wifi, limited, observations,
            "Campione iniziale del file: massimo 2.048 pacchetti. Indirizzi e SSID sono dichiarati nei frame; non verificano identità, associazione attuale o origine da un telefono/hotspot. " +
            (wifi == 0 ? "Nessun frame 802.11 decodificato nel campione: una cattura normale può avere solo intestazioni Ethernet." :
                "Osservati frame 802.11 nel file; la ricezione non copre tutti i canali o tutti i dispositivi."));
    }
}
