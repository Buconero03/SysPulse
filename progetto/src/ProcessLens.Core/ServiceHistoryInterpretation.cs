namespace ProcessLens.Core;

/// <summary>
/// Interprets structured SCM event payloads. The log does not inherently identify
/// the executable that requested the service start. Do not claim it does.
/// </summary>
public static class ServiceHistoryInterpretation
{
    public static ServiceHistoryEvent? Decode(
        string name, string displayName, int eventId, DateTime timestampLocal,
        IReadOnlyList<string?> parameters, long? recordId)
    {
        if (parameters.Count == 0) return null;
        // SCM 7035/7036/7040 generally put the display name in parameter 0.
        // SCM 7045 puts the installed service name in parameter 0.
        var recordedName = parameters[0]?.Trim().Trim('"') ?? "";
        if (!string.Equals(recordedName, name, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(recordedName, displayName, StringComparison.OrdinalIgnoreCase))
            return null;

        var p1 = parameters.Count > 1 ? parameters[1] : null;
        var p2 = parameters.Count > 2 ? parameters[2] : null;
        string title, detail;
        switch (eventId)
        {
            case 7035:
                title = "Controllo inviato al servizio";
                detail = $"Controllo: {Clean(p1)}. Richiedente sconosciuto.";
                break;
            case 7036:
                title = "Cambio di stato del servizio";
                detail = $"Stato: {Clean(p1)}. Richiedente sconosciuto.";
                break;
            case 7040:
                title = "Modalità di avvio modificata";
                detail = $"Avvio: {Clean(p1)} -> {Clean(p2)}.";
                break;
            case 7045:
                title = "Installazione del servizio";
                detail = $"Servizio installato. Percorso dichiarato: {Clean(p1)}.";
                break;
            default:
                return null;
        }
        return new ServiceHistoryEvent(recordId, eventId, timestampLocal,
            name, title, detail, $"Registro Windows: System · provider Service Control Manager · evento {eventId}");
    }

    private static string Clean(string? value) => string.IsNullOrWhiteSpace(value)
        ? "non disponibile" : value.Trim();

    public static string Explain(ServiceSnapshot service, IReadOnlyList<ServiceHistoryEvent> events, bool available)
    {
        if (!available) return "Cronologia SCM: N/D. Motivo dell’avvio sconosciuto.";
        if (events.Count == 0) return "Nessun evento trovato nel periodo. Il registro può essere incompleto o cancellato.";
        var latest = events.OrderByDescending(e => e.TimestampLocal).First();
        return $"Ultimo evento SCM trovato: {latest.Title.ToLowerInvariant()} il {latest.WhenLabel} (ID {latest.EventId}). " +
            "Richiedente sconosciuto. " +
            "Il registro SCM non identifica l’app o l’utente che ha richiesto l’avvio.";
    }
}
