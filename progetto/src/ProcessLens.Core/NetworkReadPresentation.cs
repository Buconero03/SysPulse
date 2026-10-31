using System.Globalization;

namespace ProcessLens.Core;

// UI-owned session diagnostics. No disk writer, timer or acquisition.
public sealed class NetworkActivityJournal
{
    public const int Capacity = 128, MaximumMessageChars = 1200;
    private readonly Queue<string> _entries = new();
    private readonly Func<DateTimeOffset> _utc;
    private long _sequence, _discarded;
    public event Action? Changed;
    public NetworkActivityJournal(Func<DateTimeOffset>? utc = null) => _utc = utc ?? (() => DateTimeOffset.UtcNow);
    public int Count => _entries.Count;
    public long Discarded => _discarded;
    public void Add(string source, string stage, string message)
    {
        if (_entries.Count == Capacity) { _entries.Dequeue(); _discarded++; }
        _entries.Enqueue($"[{_utc().ToUniversalTime():O}] [{Clean(source, 60)}] #{++_sequence} {Clean(stage, 40)}: {Clean(message, MaximumMessageChars)}");
        Changed?.Invoke();
    }
    private static string Clean(string text, int limit) => new(text.Take(limit).Select(c => char.IsControl(c) ||
        char.GetUnicodeCategory(c) is UnicodeCategory.Format or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator ? ' ' : c).ToArray());
    public string Report => $"LOG RETE · SESSIONE LOCALE\n{Count} righe conservate · {_discarded} righe precedenti rilasciate\n" +
        "Orari UTC. Il log descrive le operazioni di ProcessLens, non tutti gli eventi della rete. Messaggi limitati a 1200 caratteri; i dettagli completi della lettura sono nelle rispettive schede. Nessun file scritto automaticamente.\n\n" + string.Join("\n", _entries);
}

public static class NetworkReadPresentation
{
    public static string DeviceEmpty(NetworkDeviceSnapshot? snapshot, bool invalidated = false) => snapshot is null
        ? invalidated ? "Elenco da rileggere\nPremi Aggiorna elenco. I risultati precedenti sono stati rilasciati cambiando scheda." : "Nessuna lettura eseguita\nPremi Aggiorna elenco per leggere la cache locale di Windows."
        : !snapshot.Available ? "Lettura non riuscita\nConsulta il motivo nel riepilogo e nel log. Il numero di dispositivi è N/D."
        : "Nessuna voce utilizzabile nella cache\nWindows non ha restituito indirizzi compatibili con questa vista. Non significa che la rete sia vuota.";
    public static string DeviceCounts(NetworkDeviceSnapshot s, NetworkDeviceView view) => !s.Available
        ? "Voci con MAC: N/D · schede: N/D · nomi salvati: N/D"
        : $"Voci con MAC: {view.Rows.Count(r => r.Mac != "N/D")} · raggiungibili di recente: {view.Rows.Count(r => r.Presence == "Raggiungibile di recente")} · riconosciute da te: {view.Rows.Count(r => r.Recognized)} · schede lette: {s.Interfaces}\n" +
            "Raggruppamento per MAC e scheda: non è il totale dei client connessi al router.";
    public static string EventCounts(NativeNetworkSnapshot s) => !s.Available ? "Eventi: N/D · registro non accessibile" :
        $"Connessioni bloccate: {s.Events.Count(e => e.EventId == 5157)} · porte locali bloccate: {s.Events.Count(e => e.EventId is 5155 or 5159)}\n" +
        $"Accessi falliti: {s.Events.Count(e => e.EventId == 4625)} · pacchetti scartati: {(s.Options.IncludePacketDrops ? s.Events.Count(e => e.EventId == 5152).ToString(CultureInfo.CurrentCulture) : "non richiesti")}\n" +
        "Conteggi del campione, non di tutti i tentativi sulla rete.";
    public static string PacketDetails(NativePacketRow row) =>
        $"OSSERVAZIONE DI TRAFFICO\nOra locale riportata: {row.Time}\nProtocollo: {row.Protocol}\nDa: {row.Source}\nA: {row.Destination}\nDirezione riportata: {row.Direction}\n" +
        $"Byte campionati: {row.Bytes} · componente: {row.ComponentText} · codice scarto: {row.DropReasonText}\n\n" +
        "COME LEGGERLA\nÈ traffico visibile al PC. I byte sono la porzione campionata, non il consumo totale della connessione. Il componente non è un PID.\n" +
        "Lo stesso pacchetto può comparire più volte. Il codice di scarto grezzo non identifica da solo una regola firewall. Nessun contenuto applicativo viene mostrato.";
    public static string WifiDetails(WifiBssRow row) =>
        $"RETE RADIO NELLA CACHE WINDOWS\nSSID: {row.Ssid}\nBSSID: {row.Bssid}\nScheda: {row.Adapter}\nRSSI: {row.Rssi} dBm · frequenza: {row.Frequency}\nTipo: {row.BssType}\n\n" +
        "COME LEGGERLA\nSSID è il nome annunciato; BSSID identifica la voce radio osservata. La stessa rete può avere più BSSID. RSSI descrive il segnale rilevato, non una velocità.\n" +
        "L'ora di lettura non è l'ora di rilevamento radio. Non è l'elenco dei client Wi-Fi e non identifica un hotspot telefonico.";
}
