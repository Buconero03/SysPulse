using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ProcessLens.Core;

// Windows NL_NEIGHBOR_STATE values. Reachable means recently confirmed, not live traffic.
public enum NeighborState { Unreachable, Incomplete, Probe, Delay, Stale, Reachable, Permanent }
public sealed record NetworkNeighbor(string InterfaceId, string InterfaceName, string NetworkKey,
    string Address, string Mac, NeighborState State, bool IsGateway);
public sealed record NetworkDeviceSnapshot(DateTime CapturedUtc, bool Available, bool Partial,
    string Note, int Interfaces, IReadOnlyList<NetworkNeighbor> Neighbors);
public interface INetworkDeviceSource { NetworkDeviceSnapshot Capture(); }
public sealed record DeviceRecognition(string NetworkKey, string Mac, string Name);
public sealed record RecognitionCatalog(IReadOnlyList<DeviceRecognition> Devices, string Error)
{
    public bool Available => Error.Length == 0;
}
public sealed record NetworkDeviceRow(string Key, string NetworkKey, string InterfaceId,
    string InterfaceName, string Name, string Addresses, string Mac, string Presence,
    string Recognition, bool Recognized, bool CanRecognize, bool Gateway);
public sealed record NetworkDeviceView(IReadOnlyList<NetworkDeviceRow> Rows, string Summary,
    string Indicator, string Color, string Note);

public static class NetworkDeviceRules
{
    public static string NormalizeMac(string value)
    {
        string compact = value.Replace("-", "", StringComparison.Ordinal).Replace(":", "", StringComparison.Ordinal);
        if (compact.Length != 12 || compact.Any(c => !Uri.IsHexDigit(c))) return "";
        byte[] bytes = Convert.FromHexString(compact);
        if (bytes.All(b => b == 0) || (bytes[0] & 1) != 0) return ""; // no unresolved, multicast or broadcast MACs
        return string.Join("-", bytes.Select(b => b.ToString("X2")));
    }

    public static bool IsUnicastAddress(string value)
    {
        if (!IPAddress.TryParse(value, out var ip) || IPAddress.IsLoopback(ip)) return false;
        byte[] bytes = ip.GetAddressBytes();
        return ip.AddressFamily == AddressFamily.InterNetwork
            ? bytes[0] is > 0 and < 224 && !bytes.All(b => b == 255)
            : ip.AddressFamily == AddressFamily.InterNetworkV6 && !ip.IsIPv6Multicast && bytes.Any(b => b != 0);
    }

    // Local grouping hint only: not authentication of a router, network or device.
    public static string NetworkKey(string adapterId, IEnumerable<(string Address, string Mac)> gateways)
    {
        var values = gateways.OrderBy(g => g.Address, StringComparer.Ordinal).ToArray();
        if (string.IsNullOrWhiteSpace(adapterId) || values.Length == 0 ||
            values.Any(g => !IsUnicastAddress(g.Address) || NormalizeMac(g.Mac).Length == 0)) return "";
        // IPv6 scope IDs are interface indexes and may change after reconnecting.
        var canonical = values.Select(g => new IPAddress(IPAddress.Parse(g.Address).GetAddressBytes()) + "=" + NormalizeMac(g.Mac));
        string source = adapterId + "\n" + string.Join("\n", canonical.OrderBy(g => g, StringComparer.Ordinal));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
    }

    public static bool ValidKey(string key) => key.Length == 64 && key.All(Uri.IsHexDigit);
    public static string RecognitionKey(string networkKey, string mac) => networkKey + "/" + NormalizeMac(mac);

    public static NetworkDeviceView Present(NetworkDeviceSnapshot capture, RecognitionCatalog catalog)
    {
        if (!capture.Available)
            return new(Array.Empty<NetworkDeviceRow>(), "Dispositivi: N/D", "Controllo non disponibile", "#64748B", capture.Note);
        var candidates = capture.Neighbors.Where(n => IsUnicastAddress(n.Address)).Take(4096).ToArray();
        var known = catalog.Devices.Where(d => ValidKey(d.NetworkKey) && NormalizeMac(d.Mac).Length > 0)
            .GroupBy(d => RecognitionKey(d.NetworkKey, d.Mac), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var rows = candidates.GroupBy(n => n.InterfaceId + "/" + n.NetworkKey + "/" +
            (NormalizeMac(n.Mac) is { Length: > 0 } mac ? mac : "IP=" + n.Address), StringComparer.Ordinal)
            .Select(group =>
            {
                var n = group.First(); string mac = NormalizeMac(n.Mac);
                bool eligible = mac.Length > 0 && ValidKey(n.NetworkKey) && catalog.Available;
                DeviceRecognition? recognition = null;
                bool recognized = eligible && known.TryGetValue(RecognitionKey(n.NetworkKey, mac), out recognition);
                string presence = group.Any(x => x.State == NeighborState.Reachable) ? "Raggiungibile di recente" :
                    group.All(x => x.State == NeighborState.Unreachable) ? "Non raggiungibile" :
                    group.Any(x => x.State == NeighborState.Permanent) ? "Voce statica · presenza non confermata" :
                    "In cache · presenza non confermata";
                return new NetworkDeviceRow(group.Key, n.NetworkKey, n.InterfaceId, n.InterfaceName,
                    recognized ? recognition!.Name : "Nome non disponibile",
                    string.Join(", ", group.Select(x => x.Address).Distinct().OrderBy(x => x, StringComparer.Ordinal)),
                    mac.Length > 0 ? mac : "N/D", presence,
                    recognized ? "Riconosciuto da te" : eligible ? "Da riconoscere" : "Riconoscimento non disponibile",
                    recognized, eligible, group.Any(x => x.IsGateway));
            }).OrderBy(r => r.Recognized).ThenBy(r => r.InterfaceName).ThenBy(r => r.Mac).ToArray();
        int devices = rows.Count(r => r.Mac != "N/D"), unknown = rows.Count(r => r.CanRecognize && !r.Recognized);
        bool limited = capture.Partial || capture.Neighbors.Count > 4096 || !catalog.Available ||
            rows.Any(r => !r.CanRecognize) || devices == 0;
        bool recognitionComplete = catalog.Available && rows.Where(r => r.Mac != "N/D").All(r => r.CanRecognize);
        string summary = (devices == 1 ? "1 dispositivo rilevato" : $"{devices} dispositivi rilevati") +
            (recognitionComplete ? $" · {unknown} da riconoscere" : " · riconoscimento incompleto");
        int unresolved = rows.Count(r => r.Mac == "N/D");
        if (unresolved > 0) summary += unresolved == 1 ? " · 1 indirizzo senza MAC" : $" · {unresolved} indirizzi senza MAC";
        string note = string.Join("\n", new[] { capture.Note, catalog.Error }.Where(s => s.Length > 0));
        return new(rows, summary, limited ? "Dati incompleti" : unknown > 0 ? "Dispositivi da riconoscere" :
            "Tutti i dispositivi rilevati riconosciuti", limited ? "#64748B" : unknown > 0 ? "#A16207" : "#047857", note);
    }
}

// Per-session novelty only. First complete observation of each scope is the baseline.
public sealed class NetworkDeviceTracker
{
    private readonly Dictionary<string, HashSet<string>> _seen = new(StringComparer.Ordinal);
    private readonly Queue<string> _order = new();
    public int Observe(NetworkDeviceSnapshot snapshot, NetworkDeviceView view)
    {
        if (!snapshot.Available || snapshot.Partial || snapshot.Neighbors.Count > 4096) return 0;
        int added = 0;
        foreach (var group in view.Rows.Where(r => NetworkDeviceRules.ValidKey(r.NetworkKey) && r.Mac != "N/D")
                     .GroupBy(r => r.NetworkKey, StringComparer.Ordinal))
        {
            var current = group.Select(r => r.Mac).ToHashSet(StringComparer.Ordinal);
            if (current.Count > 1024) continue;
            if (!_seen.TryGetValue(group.Key, out var seen))
            {
                if (_seen.Count == 16) _seen.Remove(_order.Dequeue());
                _order.Enqueue(group.Key); _seen[group.Key] = current;
                continue;
            }
            // Reset a bounded baseline rather than re-alerting evicted addresses.
            if (seen.Count + current.Count > 1024) { _seen[group.Key] = current; continue; }
            foreach (var row in group)
                if (seen.Add(row.Mac) && !row.Recognized && row.CanRecognize) added++;
        }
        return added;
    }
}

// User labels only, scoped by a local network fingerprint + MAC. Never a security allowlist.
public sealed class DeviceRecognitionStore
{
    public const int MaximumDevices = 512;
    public const int MaximumBytes = 524288;
    private readonly string _path;
    public DeviceRecognitionStore(string path) => _path = path;
    public RecognitionCatalog Load()
    {
        try
        {
            using var stream = File.OpenRead(_path);
            if (stream.Length > MaximumBytes) throw new InvalidDataException("File troppo grande.");
            var document = JsonSerializer.Deserialize<StoredCatalog>(stream);
            if (document is null || document.Version != 1 || document.Devices is null ||
                document.Devices.Length > MaximumDevices || document.Devices.Any(d => !Valid(d)) ||
                document.Devices.Select(d => NetworkDeviceRules.RecognitionKey(d.NetworkKey, d.Mac)).Distinct().Count() != document.Devices.Length)
                throw new InvalidDataException("Formato non valido.");
            return new(document.Devices, "");
        }
        catch (FileNotFoundException) { return new(Array.Empty<DeviceRecognition>(), ""); }
        catch (DirectoryNotFoundException) { return new(Array.Empty<DeviceRecognition>(), ""); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException)
        { return new(Array.Empty<DeviceRecognition>(), "Riconoscimenti non disponibili: " + ex.Message); }
    }

    public string Save(RecognitionCatalog catalog)
    {
        if (!catalog.Available) return catalog.Error;
        var existing = Load();
        if (!existing.Available) return existing.Error; // Never overwrite an unreadable/corrupt catalog.
        if (catalog.Devices.Count > MaximumDevices || catalog.Devices.Any(d => !Valid(d)) ||
            catalog.Devices.Select(d => NetworkDeviceRules.RecognitionKey(d.NetworkKey, d.Mac)).Distinct().Count() != catalog.Devices.Count)
            return "Limite o formato dei riconoscimenti non valido.";
        string temporary = _path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
            string json = JsonSerializer.Serialize(new StoredCatalog(1, catalog.Devices.ToArray()));
            if (Encoding.UTF8.GetByteCount(json) > MaximumBytes) return "Riconoscimenti troppo grandi per il salvataggio.";
            File.WriteAllText(temporary, json);
            File.Move(temporary, _path, true);
            return "";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        { return "Salvataggio non riuscito: " + ex.Message; }
        finally { try { File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }
    private static bool Valid(DeviceRecognition? d) => d is not null && d.NetworkKey is not null &&
        d.Mac is not null && d.Name is not null && NetworkDeviceRules.ValidKey(d.NetworkKey) &&
        NetworkDeviceRules.NormalizeMac(d.Mac) == d.Mac && d.Name.Length is > 0 and <= 80 && !d.Name.Any(char.IsControl);
    private sealed record StoredCatalog(int Version, DeviceRecognition[] Devices);
}
