using System.Globalization;
using System.Net;
using System.Xml;
using System.Xml.Linq;

namespace ProcessLens.Core;

public sealed record NetworkAuditSetting(string Category, bool? FailuresEnabled, string Note)
{
    public string Text => Category + ": " + (FailuresEnabled is true ? "raccolta errori abilitata adesso" :
        FailuresEnabled is false ? "raccolta errori disabilitata adesso" : "configurazione N/D") +
        (Note.Length == 0 ? "" : " · " + Note);
}
public sealed record NativeNetworkEvent(DateTime Utc, long? RecordId, int EventId, string Event,
    string SourceAddress, string SourcePort, string DestinationAddress, string DestinationPort,
    string Direction, string Protocol, int? Pid, string Application, string Status)
{
    public string Time => Utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture);
    public string PidText => Pid?.ToString(CultureInfo.CurrentCulture) ?? "N/D";
    public string Meaning => EventId switch {
        5157 => "Windows Filtering Platform ha bloccato una connessione. La riga non misura dati trasferiti.",
        5155 => "Windows Filtering Platform ha impedito al programma di mettersi in ascolto su una porta locale. Non prova una richiesta remota.",
        5159 => "Windows Filtering Platform ha negato l'associazione del programma a una porta locale (bind). Non indica una connessione remota.",
        5152 => "Windows Filtering Platform ha scartato un pacchetto. Più righe possono appartenere alla stessa connessione; non sono altrettanti dispositivi.",
        4625 => "Windows ha registrato un accesso di rete o remoto fallito. Non è un accesso riuscito.",
        _ => "Significato non disponibile per questo ID." };
    public string FilterId { get; init; } = "N/D";
    public string LayerId { get; init; } = "N/D";
    public string Details => $"SIGNIFICATO\n{Meaning}\n\nDATI REGISTRATI\n" +
        $"[Security:{RecordId?.ToString(CultureInfo.InvariantCulture) ?? "N/D"}] Evento {EventId} · {Event}\nOra UTC: {Utc:O}\n" +
        (EventId is 5155 or 5159 ? $"Indirizzo locale: {SourceAddress} · porta locale: {SourcePort}\n" :
            $"Sorgente: {SourceAddress} · porta {SourcePort}\nDestinazione: {DestinationAddress} · porta {DestinationPort}\n") +
        $"Direzione: {Direction} · protocollo: {Protocol}\nPID riportato all'epoca: {PidText} · programma riportato: {Application}\n" +
        $"Stato riportato: {Status}\nFiltro WFP riportato: {FilterId} · livello WFP: {LayerId}\n\nCOME VERIFICARE\n" +
        "Confronta ora, programma e indirizzi con l'operazione che stavi eseguendo. Un ID filtro storico non identifica necessariamente la regola attuale.\n" +
        "È un evento storico del PC. Il PID può essere stato riutilizzato; non identifica l'istanza attuale. " +
        "Un blocco o un accesso fallito non dimostra un'intrusione o un ascolto del traffico.";
}
public sealed record NativeNetworkSnapshot(DateTime CapturedUtc, DateTime SinceUtc, bool Available,
    bool Partial, IReadOnlyList<NetworkAuditSetting> Audit, IReadOnlyList<NativeNetworkEvent> Events, string Note)
{
    public NetworkEventReadOptions Options { get; init; } = new();
    public int ScannedEvents { get; init; }
    public int MalformedEvents { get; init; }
    public int XmlCharacters { get; init; }
}
public sealed record NetworkEventReadOptions(int Minutes = 15, bool IncludePacketDrops = false)
{
    public void Validate() { if (Minutes is not (15 or 60)) throw new ArgumentOutOfRangeException(nameof(Minutes)); }
}
public interface INativeNetworkEvidenceQuerySource : INativeNetworkEvidenceSource
{
    NativeNetworkSnapshot Capture(NetworkEventReadOptions options, CancellationToken cancellationToken);
}
public interface INativeNetworkEvidenceSource
{
    NativeNetworkSnapshot Capture(CancellationToken cancellationToken);
}
public static class NativeNetworkEvidence
{
    public const int MaximumEvents = 256, MaximumXmlChars = 32768, MaximumTotalXmlChars = 1048576;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);
    private static readonly XNamespace Ns = "http://schemas.microsoft.com/win/2004/08/events/event";
    public static NativeNetworkEvent? Decode(string xml, DateTime sinceUtc, DateTime untilUtc)
    {
        if (xml.Length == 0 || xml.Length > MaximumXmlChars) throw new InvalidDataException("XML evento oltre il limite o vuoto.");
        using var text = new StringReader(xml);
        using var reader = XmlReader.Create(text, new XmlReaderSettings {
            DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaximumXmlChars });
        var root = XDocument.Load(reader).Root;
        if (root?.Name != Ns + "Event") throw new InvalidDataException("Schema evento non riconosciuto.");
        var system = root.Element(Ns + "System") ?? throw new InvalidDataException("Intestazione evento assente.");
        if ((string?)system.Element(Ns + "Provider")?.Attribute("Name") != "Microsoft-Windows-Security-Auditing")
            throw new InvalidDataException("Provider evento inatteso.");
        if (!int.TryParse((string?)system.Element(Ns + "EventID"), NumberStyles.None, CultureInfo.InvariantCulture, out int id))
            throw new InvalidDataException("ID evento non valido.");
        if (id is not (5157 or 5155 or 5159 or 5152 or 4625)) return null;
        string stamp = (string?)system.Element(Ns + "TimeCreated")?.Attribute("SystemTime") ?? "";
        if (!stamp.EndsWith('Z') || !DateTimeOffset.TryParse(stamp, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
            throw new InvalidDataException("Ora evento UTC non valida.");
        if (time.UtcDateTime < sinceUtc || time.UtcDateTime > untilUtc) return null;
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var e in root.Element(Ns + "EventData")?.Elements(Ns + "Data") ?? [])
        {
            string name = (string?)e.Attribute("Name") ?? "";
            if (name.Length == 0 || !fields.TryAdd(name, e.Value)) throw new InvalidDataException("Campi evento ambigui.");
        }
        string Get(string key) => fields.TryGetValue(key, out string? value) ? value : "";
        string logon = Get("LogonType");
        if (id == 4625 && logon is not ("3" or "10")) return null; // Network / RemoteInteractive only.
        long? record = long.TryParse((string?)system.Element(Ns + "EventRecordID"), NumberStyles.None,
            CultureInfo.InvariantCulture, out long recordId) && recordId > 0 ? recordId : null;
        string source = Address(Get(id == 4625 ? "IpAddress" : "SourceAddress"));
        string direction = id == 4625 ? "Accesso alla macchina" : id is 5155 or 5159 ? "Operazione locale" : Get("Direction") switch {
            "%%14592" => "Ingresso", "%%14593" => "Uscita", _ => "N/D" };
        string protocol = id == 4625 ? "N/D" : Get("Protocol") switch { "6" => "TCP", "17" => "UDP", "1" => "ICMP", "58" => "ICMPv6", _ => Clean(Get("Protocol"), 16) };
        return new(time.UtcDateTime, record, id,
            id switch { 5157 => "Connessione bloccata da WFP", 5155 => "Ascolto su porta locale bloccato", 5159 => "Bind su porta locale bloccato",
                5152 => "Pacchetto scartato da WFP", _ => logon == "10" ? "Accesso interattivo remoto fallito" : "Accesso di rete fallito" },
            source, Port(Get(id == 4625 ? "IpPort" : "SourcePort")),
            id is 5155 or 5159 ? "N/D" : id == 4625 ? "Questo PC" : Address(Get("DestAddress")),
            id is 5157 or 5152 ? Port(Get("DestPort")) : "N/D", direction, protocol,
            Pid(Get(id == 5157 ? "ProcessID" : "ProcessId")), Basename(Get(id == 4625 ? "ProcessName" : "Application")),
            id == 4625 ? Clean(Get("Status"), 32) : "Blocco registrato")
            { FilterId = Clean(Get("FilterRTID"), 32), LayerId = Clean(Get("LayerRTID"), 32) };
    }
    private static string Address(string value) => IPAddress.TryParse(value, out var ip) ? ip.ToString() : "N/D";
    private static string Port(string value) => ushort.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var port)
        ? port.ToString(CultureInfo.InvariantCulture) : "N/D";
    private static int? Pid(string value)
    {
        bool hex = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
        return int.TryParse(hex ? value[2..] : value, hex ? NumberStyles.AllowHexSpecifier : NumberStyles.None,
            CultureInfo.InvariantCulture, out int pid) && pid >= 0 ? pid : null;
    }
    private static string Basename(string value) => Clean(value.Replace('/', '\\').Split('\\').LastOrDefault() ?? "", 128);
    private static string Clean(string value, int cap)
    {
        if (string.IsNullOrWhiteSpace(value) || value == "-") return "N/D";
        return new string(value.Take(cap).Select(c => char.IsControl(c) || char.GetUnicodeCategory(c) is UnicodeCategory.Format or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator ? ' ' : c).ToArray());
    }
    public static string Report(NativeNetworkSnapshot s) =>
        $"[Security] Campione: {s.CapturedUtc:O}\nIntervallo UTC: {s.SinceUtc:O} → {s.CapturedUtc:O}\n" +
        (s.Available ? $"Eventi nel campione: {s.Events.Count} · blocchi WFP: {s.Events.Count(e => e.EventId == 5157)} · accessi di rete/remoti falliti: {s.Events.Count(e => e.EventId == 4625)}" : "Registro Security: non disponibile; conteggi N/D") +
        "\n" + NetworkReadPresentation.EventCounts(s) +
        $"\nRecord esaminati: {s.ScannedEvents} · non interpretabili: {s.MalformedEvents} · caratteri XML: {s.XmlCharacters}" +
        (s.Partial ? "\nLettura parziale: non rappresenta tutti gli eventi dell'intervallo." : "") + "\n" +
        string.Join("\n", s.Audit.Select(a => a.Text)) + "\n" + s.Note + "\n" +
        "La configurazione attuale non ricostruisce quella passata o le eccezioni per utente. Zero eventi non certifica assenza di tentativi. " +
        "Questa vista non conta i client del router e non rileva con certezza un ascolto passivo.\n" +
        string.Join("\n\n", s.Events.Select(e => e.Details));
}
